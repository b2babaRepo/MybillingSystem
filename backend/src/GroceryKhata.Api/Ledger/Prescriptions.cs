using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Common;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Ledger;

/// <summary>
/// The prescription a bill was sold on: needed for Schedule H, H1 and X medicines. H1 sales make up the H1 register
/// (doctor, patient, drug, quantity), which the drug rules ask to keep for three years: bills are never deleted, only cancelled.
/// </summary>
public sealed class BillPrescription : Entity, ITenantOwned
{
    public Guid ShopId { get; set; }

    public Guid BillId { get; set; }

    public required string DoctorName { get; set; }

    public string? DoctorAddress { get; set; }

    public string? DoctorRegNo { get; set; }

    public required string PatientName { get; set; }

    public string? PatientAddress { get; set; }

    public DateOnly? PrescribedOn { get; set; }
}

public sealed record H1RegisterRow(
    DateTimeOffset SoldAt, string? BillNo, string PatientName, string? PatientAddress, string DoctorName, string? DoctorAddress, string? DoctorRegNo,
    string Medicine, decimal Quantity, string? Batch, bool Cancelled);

public static class Prescriptions
{
    public static IEndpointRouteBuilder MapPrescriptions(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/pharmacy/h1-register", RegisterAsync).WithTags("Shops").RequireAuthorization(AppPolicies.ShopMember);
        return app;
    }

    /// <summary>
    /// Checks a bill's prescription: none needed without H, H1 or X medicines; otherwise the doctor's and patient's names
    /// (and for H1 and X, the doctor's address too). Returns the record to save, or an error.
    /// </summary>
    public static (BillPrescription? Record, string? Error) Check(PrescriptionRequest? request, IEnumerable<(string Name, DrugSchedule? Schedule)> items, DateOnly today)
    {
        var scheduled = items.Where(i => i.Schedule is DrugSchedule.H or DrugSchedule.H1 or DrugSchedule.X).ToList();
        string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(max, s.Trim().Length)];
        var doctor = Clean(request?.DoctorName, 80);
        var patient = Clean(request?.PatientName, 80);
        if (scheduled.Count == 0)
        {
            return doctor is null || patient is null ? (null, null) : (Build(), null);
        }

        var names = string.Join(", ", scheduled.Select(s => s.Name).Distinct().Take(3));
        if (doctor is null || patient is null)
        {
            return (null, $"{names}: prescription medicine. Enter the doctor's and patient's names.");
        }

        if (scheduled.Any(s => s.Schedule is DrugSchedule.H1 or DrugSchedule.X) && Clean(request!.DoctorAddress, 200) is null)
        {
            return (null, $"{names}: Schedule H1/X. Enter the doctor's address for the register.");
        }

        if (request!.PrescribedOn > today)
        {
            return (null, "The prescription date can't be in the future.");
        }

        return (Build(), null);

        BillPrescription Build() => new()
        {
            DoctorName = doctor!, DoctorAddress = Clean(request!.DoctorAddress, 200), DoctorRegNo = Clean(request.DoctorRegNo, 40), PatientName = patient!,
            PatientAddress = Clean(request.PatientAddress, 200), PrescribedOn = request.PrescribedOn,
        };
    }

    /// <summary>The H1 register for a period (this month by default): every H1 medicine sold, with its prescription, oldest first.</summary>
    private static async Task<IReadOnlyList<H1RegisterRow>> RegisterAsync(DateOnly? from, DateOnly? to, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(IndiaTime.Offset).DateTime);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        var (startAt, endAt) = (new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime(), new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), IndiaTime.Offset).ToUniversalTime());
        var rows = await (from l in db.BillLines.AsNoTracking()
                          join b in db.Bills.AsNoTracking() on l.BillId equals b.Id
                          join p in db.Products.AsNoTracking() on l.ProductId equals (Guid?)p.Id
                          join rx in db.BillPrescriptions.AsNoTracking() on b.Id equals rx.BillId
                          where p.Schedule == DrugSchedule.H1 && b.OccurredAt >= startAt && b.OccurredAt < endAt
                          orderby b.OccurredAt, l.LineNo
                          select new { b.OccurredAt, b.FinancialYear, b.Number, b.CancelledAt, rx, l.Name, l.Quantity, l.Batch }).Take(5000).ToListAsync(ct);
        return rows.Select(r => new H1RegisterRow(r.OccurredAt, r.Number > 0 ? $"{r.FinancialYear}/{r.Number:D4}" : null, r.rx.PatientName, r.rx.PatientAddress, r.rx.DoctorName,
            r.rx.DoctorAddress, r.rx.DoctorRegNo, r.Name, r.Quantity, r.Batch, r.CancelledAt != null)).ToList();
    }
}
