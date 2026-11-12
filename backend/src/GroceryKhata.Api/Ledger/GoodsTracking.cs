using System.Security.Claims;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using static GroceryKhata.Api.Common.Problems;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// Where an open order has got to, as the shopkeeper last heard: placed (saved), sent to the supplier, confirmed by
/// them, or dispatched. Part received, received, closed and cancelled are not stored here: they follow from the goods
/// that came and the order's status.
/// </summary>
public enum OrderStage
{
    Placed,
    Sent,
    Confirmed,
    Dispatched,
}

public enum ContactChannel
{
    Call,
    Whatsapp,
    Visit,
}

public enum FreightPaidBy
{
    Shop,
    Supplier,
}

/// <summary>One lot of an order on its way: the truck, the bilty (LR), the e-way bill and the freight. An order can come in several.</summary>
public sealed class OrderShipment : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid OrderId { get; set; }

    public DateOnly? DispatchedOn { get; set; }

    public DateOnly? ExpectedOn { get; set; }

    public string? Transporter { get; set; }

    public string? LrNo { get; set; }

    public string? VehicleNo { get; set; }

    public string? EwayBillNo { get; set; }

    public DateOnly? EwayValidUntil { get; set; }

    public decimal? Freight { get; set; }

    public FreightPaidBy? FreightPaidBy { get; set; }

    /// <summary>The goods receipt this lot came in on, once it arrived.</summary>
    public Guid? ReceiptId { get; set; }

    /// <summary>A photo of the bilty / LR (the transporter's receipt), shrunk on the phone; under 150 KB.</summary>
    public byte[]? Photo { get; set; }

    public string? PhotoType { get; set; }

    public Guid CreatedByUserId { get; set; }
}

public sealed record ShipmentPhotoRequest(string? PhotoDataUrl);

/// <summary>A chase-up with the supplier: a call, a WhatsApp or a visit, what they said, and any new date they promised.</summary>
public sealed class OrderFollowUp : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid OrderId { get; set; }

    public DateTimeOffset At { get; set; }

    public ContactChannel Channel { get; set; }

    public string? Note { get; set; }

    public DateOnly? NewPromisedDate { get; set; }

    public Guid ByUserId { get; set; }
}

public sealed record StageRequest(OrderStage Stage, ContactChannel? Via = null);

public sealed record ShipmentRequest(Guid Id, DateOnly? DispatchedOn, DateOnly? ExpectedOn, string? Transporter, string? LrNo, string? VehicleNo,
    string? EwayBillNo, DateOnly? EwayValidUntil, decimal? Freight, FreightPaidBy? FreightPaidBy);

public sealed record FollowUpRequest(Guid Id, ContactChannel Channel, string? Note, DateOnly? NewPromisedDate);

public sealed record SnoozeRequest(DateOnly? Until);

public sealed record ShipmentDto(Guid Id, DateOnly? DispatchedOn, DateOnly? ExpectedOn, string? Transporter, string? LrNo, string? VehicleNo, string? EwayBillNo,
    DateOnly? EwayValidUntil, decimal? Freight, FreightPaidBy? FreightPaidBy, bool Arrived, bool HasPhoto = false);

public sealed record FollowUpDto(DateTimeOffset At, ContactChannel Channel, string? Note, DateOnly? NewPromisedDate, string? By);

/// <summary>
/// An open order still waiting for goods, for "Goods to come". <see cref="DaysLate"/>: days past the expected date
/// (0 when due today or not yet due, null with no date). <see cref="PromiseMoves"/>: how often the date was pushed.
/// </summary>
public sealed record ComingOrderDto(Guid Id, int Number, Guid SupplierId, string Supplier, string? Phone, string Stage, DateOnly? ExpectedOn, int? DaysLate,
    int PromiseMoves, decimal Pending, int Items, decimal Value, DateOnly? SnoozedUntil, DateTimeOffset CreatedAt, string? Note,
    IReadOnlyList<ShipmentDto> Shipments, IReadOnlyList<FollowUpDto> FollowUps);

public static class GoodsTracking
{
    public static IEndpointRouteBuilder MapGoodsTracking(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/suppliers/orders").WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        group.MapPost("/{id:guid}/stage", StageAsync);
        group.MapPost("/{id:guid}/shipments", ShipAsync);
        group.MapPost("/{id:guid}/shipments/{shipmentId:guid}/photo", AddPhotoAsync);
        group.MapGet("/{id:guid}/shipments/{shipmentId:guid}/photo", PhotoAsync);
        group.MapPost("/{id:guid}/follow-ups", FollowUpAsync);
        group.MapPost("/{id:guid}/snooze", SnoozeAsync);
        app.MapGet("/api/v1/goods-to-come", ComingAsync).WithTags("Suppliers").RequireAuthorization(AppPolicies.ShopOwner);
        return app;
    }

    /// <summary>Keeps a photo of the bilty on a lot; a new photo replaces the old one, an empty one takes it off.</summary>
    private static async Task<IResult> AddPhotoAsync(Guid id, Guid shipmentId, ShipmentPhotoRequest request, AppDbContext db, CancellationToken ct)
    {
        var lot = await db.OrderShipments.SingleOrDefaultAsync(s => s.Id == shipmentId && s.OrderId == id, ct);
        if (lot is null)
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrEmpty(request.PhotoDataUrl))
        {
            (lot.Photo, lot.PhotoType) = (null, null);
        }
        else
        {
            var parts = request.PhotoDataUrl.Split(',', 2);
            var type = parts is [var head, _] && head.StartsWith("data:", StringComparison.Ordinal) && head.EndsWith(";base64", StringComparison.Ordinal) ? head[5..^7] : null;
            byte[]? photo = null;
            try
            {
                photo = type is "image/webp" or "image/jpeg" or "image/png" ? Convert.FromBase64String(parts[1]) : null;
            }
            catch (FormatException)
            {
            }

            if (photo is not { Length: > 0 and <= 150_000 })
            {
                return Invalid("PhotoDataUrl", "Send the bilty photo as WebP, JPEG or PNG under 150 KB.");
            }

            (lot.Photo, lot.PhotoType) = (photo, type);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> PhotoAsync(Guid id, Guid shipmentId, AppDbContext db, CancellationToken ct) =>
        await db.OrderShipments.AsNoTracking().Where(s => s.Id == shipmentId && s.OrderId == id && s.Photo != null)
            .Select(s => new { s.Photo, s.PhotoType }).SingleOrDefaultAsync(ct) is { } p
            ? TypedResults.File(p.Photo!, p.PhotoType ?? "image/webp")
            : TypedResults.NotFound();

    private static string? Short(string? text, int max) => string.IsNullOrWhiteSpace(text) ? null : text.Trim()[..Math.Min(max, text.Trim().Length)];

    /// <summary>The stage to show: what came beats what was said.</summary>
    internal static string StageOf(PurchaseOrder o) =>
        o.Status == PurchaseOrderStatus.Cancelled ? "cancelled"
        : o.Status == PurchaseOrderStatus.Closed ? (o.Lines.All(l => l.Received >= l.Quantity) ? "received" : "closed")
        : o.Lines.Any(l => l.Received > 0) ? "part"
        : o.Stage switch { OrderStage.Sent => "sent", OrderStage.Confirmed => "confirmed", OrderStage.Dispatched => "dispatched", _ => "placed" };

    /// <summary>Moves the expected date, counting it as a moved promise when there was a date before.</summary>
    private static void Promise(PurchaseOrder o, DateOnly? date)
    {
        if (date is { } d && d != o.ExpectedOn)
        {
            if (o.ExpectedOn is not null)
            {
                o.PromiseMoves++;
            }

            (o.ExpectedOn, o.SnoozedUntil) = (d, null);
        }
    }

    private static async Task<PurchaseOrder?> OpenAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == id, ct);

    private static async Task<IResult> StageAsync(Guid id, StageRequest request, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var order = await OpenAsync(db, id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (!Enum.IsDefined(request.Stage) || (request.Via is { } v && !Enum.IsDefined(v)))
        {
            return Invalid("Stage", "Pick where the order has got to.");
        }

        if (order.Status != PurchaseOrderStatus.Open)
        {
            return TypedResults.NoContent();
        }

        var now = clock.GetUtcNow();
        order.Stage = request.Stage;
        if (request.Stage >= OrderStage.Sent && order.SentAt is null)
        {
            (order.SentAt, order.SentVia) = (now, request.Via ?? order.SentVia);
        }

        if (request.Stage >= OrderStage.Confirmed && order.ConfirmedAt is null)
        {
            order.ConfirmedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ShipAsync(Guid id, ShipmentRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (await db.OrderShipments.AsNoTracking().AnyAsync(s => s.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        var order = await OpenAsync(db, id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Id == Guid.Empty || request.Freight is < 0 or > 10_000_000 || (request.FreightPaidBy is { } f && !Enum.IsDefined(f))
            || (request.DispatchedOn is { } a && request.ExpectedOn is { } b && b < a))
        {
            return Invalid("ExpectedOn", "Check the dates and freight.");
        }

        if (request.EwayBillNo is { Length: > 0 } eway && (eway.Trim().Length != 12 || !eway.Trim().All(char.IsAsciiDigit)))
        {
            return Invalid("EwayBillNo", "An e-way bill number is 12 digits.");
        }

        db.OrderShipments.Add(new OrderShipment
        {
            Id = request.Id, OrderId = id, DispatchedOn = request.DispatchedOn, ExpectedOn = request.ExpectedOn, Transporter = Short(request.Transporter, 80),
            LrNo = Short(request.LrNo, 40), VehicleNo = Short(request.VehicleNo, 20)?.ToUpperInvariant(), EwayBillNo = Short(request.EwayBillNo, 12),
            EwayValidUntil = request.EwayValidUntil, Freight = request.Freight, FreightPaidBy = request.FreightPaidBy, CreatedByUserId = user.UserId()!.Value,
        });
        if (order.Status == PurchaseOrderStatus.Open)
        {
            order.Stage = OrderStage.Dispatched;
            Promise(order, request.ExpectedOn);
        }

        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> FollowUpAsync(Guid id, FollowUpRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.OrderFollowUps.AsNoTracking().AnyAsync(f => f.Id == request.Id, ct))
        {
            return TypedResults.NoContent();
        }

        var order = await OpenAsync(db, id, ct);
        if (order is null)
        {
            return TypedResults.NotFound();
        }

        if (request.Id == Guid.Empty || !Enum.IsDefined(request.Channel) || request.Note?.Trim().Length > 300)
        {
            return Invalid("Note", "Keep the note under 300 letters.");
        }

        db.OrderFollowUps.Add(new OrderFollowUp
        {
            Id = request.Id, OrderId = id, At = clock.GetUtcNow(), Channel = request.Channel, Note = Short(request.Note, 300),
            NewPromisedDate = request.NewPromisedDate, ByUserId = user.UserId()!.Value,
        });
        Promise(order, request.NewPromisedDate);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    /// <summary>No morning reminders for this order until the date (null: remind again).</summary>
    private static async Task<IResult> SnoozeAsync(Guid id, SnoozeRequest request, AppDbContext db, CancellationToken ct)
    {
        var changed = await db.PurchaseOrders.Where(o => o.Id == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.SnoozedUntil, request.Until), ct);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    /// <summary>Open orders with goods still to come, latest first by how late they are.</summary>
    internal static async Task<List<ComingOrderDto>> ComingListAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var orders = await db.PurchaseOrders.AsNoTracking().Include(o => o.Lines).Where(o => o.Status == PurchaseOrderStatus.Open).ToListAsync(ct);
        orders = orders.Where(o => o.Lines.Sum(PurchaseOrders.PendingOf) > 0).ToList();
        var ids = orders.Select(o => o.Id).ToList();
        var shipments = (await db.OrderShipments.AsNoTracking().Where(s => ids.Contains(s.OrderId)).OrderBy(s => s.CreatedAt).ToListAsync(ct)).ToLookup(s => s.OrderId);
        var follows = (await (from f in db.OrderFollowUps.AsNoTracking()
                              where ids.Contains(f.OrderId)
                              join u in db.Users.AsNoTracking() on f.ByUserId equals u.Id into us
                              from u in us.DefaultIfEmpty()
                              orderby f.At descending
                              select new { f, By = u == null ? null : u.FullName }).ToListAsync(ct)).ToLookup(x => x.f.OrderId);
        var supplierIds = orders.Select(o => o.SupplierId).Distinct().ToList();
        var suppliers = await db.Suppliers.AsNoTracking().Where(s => supplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Name, s.Phone }, ct);
        return orders.Select(o => new ComingOrderDto(o.Id, o.Number, o.SupplierId, suppliers.GetValueOrDefault(o.SupplierId)?.Name ?? "", suppliers.GetValueOrDefault(o.SupplierId)?.Phone,
                StageOf(o), o.ExpectedOn, o.ExpectedOn is { } e ? Math.Max(0, today.DayNumber - e.DayNumber) : null, o.PromiseMoves,
                o.Lines.Sum(PurchaseOrders.PendingOf), o.Lines.Count(l => PurchaseOrders.PendingOf(l) > 0), o.Lines.Sum(l => PurchaseOrders.PendingOf(l) * (l.Rate ?? 0)),
                o.SnoozedUntil, o.CreatedAt, o.Note,
                shipments[o.Id].Select(s => new ShipmentDto(s.Id, s.DispatchedOn, s.ExpectedOn, s.Transporter, s.LrNo, s.VehicleNo, s.EwayBillNo, s.EwayValidUntil, s.Freight, s.FreightPaidBy, s.ReceiptId != null, s.Photo != null)).ToList(),
                follows[o.Id].Take(10).Select(x => new FollowUpDto(x.f.At, x.f.Channel, x.f.Note, x.f.NewPromisedDate, x.By)).ToList()))
            .OrderByDescending(o => o.ExpectedOn is { } e && e < today ? 1 : 0).ThenByDescending(o => o.DaysLate ?? -1).ThenBy(o => o.ExpectedOn ?? DateOnly.MaxValue).ThenBy(o => o.Number)
            .ToList();
    }

    private static async Task<IReadOnlyList<ComingOrderDto>> ComingAsync(AppDbContext db, TimeProvider clock, CancellationToken ct) => await ComingListAsync(db, clock, ct);

    /// <summary>
    /// The orders to mention in the morning note: due tomorrow, due today, and late (every other day while late, from
    /// the first day), unless snoozed.
    /// </summary>
    internal static async Task<(int DueTomorrow, int DueToday, int Late)> RemindersAsync(AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = IndiaTime.Today(clock);
        var list = (await ComingListAsync(db, clock, ct)).Where(o => o.ExpectedOn != null && (o.SnoozedUntil is null || o.SnoozedUntil < today)).ToList();
        return (list.Count(o => o.ExpectedOn == today.AddDays(1)), list.Count(o => o.ExpectedOn == today),
            list.Count(o => o.ExpectedOn < today && (today.DayNumber - o.ExpectedOn!.Value.DayNumber) % 2 == 1));
    }

    /// <summary>A receipt against an order fills its oldest lot still on the way.</summary>
    internal static async Task ArrivedAsync(AppDbContext db, Guid orderId, Guid receiptId, CancellationToken ct)
    {
        var lot = await db.OrderShipments.Where(s => s.OrderId == orderId && s.ReceiptId == null).OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        if (lot is not null)
        {
            lot.ReceiptId = receiptId;
        }
    }
}
