using System.Security.Claims;
using System.Text.Json;
using Anthropic;
using GroceryKhata.Api.Catalogue;
using GroceryKhata.Api.Identity;
using GroceryKhata.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Planner;

/// <summary>
/// What the reader proposed for one household input: kept for the audit trail (what the AI said vs what was
/// finally ordered) and for measuring accuracy later. The photo itself is never stored; only that there was one.
/// </summary>
public sealed class AiProposal
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public string? InputText { get; set; }

    public bool HadImage { get; set; }

    /// <summary>"rules" or the model id.</summary>
    public required string Reader { get; set; }

    /// <summary>The checked lines as returned to the household (JSON).</summary>
    public required string Lines { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ProposalSource Source { get; set; }

    /// <summary>When the household checked it (added to a list or set aside); WhatsApp drafts wait until then.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }
}

public enum ProposalSource
{
    /// <summary>Written, spoken or photographed in the app; checked on the spot.</summary>
    App,

    /// <summary>Sent to the WhatsApp number; waits in the app's "From WhatsApp" drafts.</summary>
    WhatsApp,
}

/// <summary>A WhatsApp draft waiting to be checked.</summary>
public sealed record PendingDraftDto(Guid ProposalId, DateTimeOffset ReceivedAt, string? Text, bool HadImage, IReadOnlyList<DraftLineDto> Lines);

public sealed record DraftRequest(string? Text, string? ImageDataUrl);

public sealed record DraftLineDto(string Said, int? CatalogueItemId, string Name, decimal? Quantity, Unit? Unit, ReadConfidence Confidence, string? Reason);

public sealed record DraftDto(Guid ProposalId, string Reader, IReadOnlyList<DraftLineDto> Lines);

/// <summary>
/// AI phase 1: a household types a note or adds a photo/screenshot; the reader turns it into draft lines; the
/// household checks and fixes them before anything is added to their list or sent. The shop then packs as usual
/// and has the final say. Nothing here orders, prices or charges anything.
/// </summary>
public static class OrderDrafts
{
    private const int MaxTextLength = 2000;
    private const int MaxImageBytes = 1_500_000;
    private const int MaxLines = 60;

    public static IServiceCollection AddOrderReading(this IServiceCollection services, IConfiguration configuration)
    {
        // With an Anthropic API key (Ai:ApiKey or ANTHROPIC_API_KEY), Claude reads notes and photos; without one,
        // typed notes are read by rules and photos are refused. Ai:Enabled=false forces the rules (tests).
        var key = configuration["Ai:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var enabled = configuration.GetValue("Ai:Enabled", true);
        var model = configuration["Ai:Model"] ?? "claude-opus-5-5";
        services.AddSingleton<IOrderReader>(enabled && !string.IsNullOrWhiteSpace(key)
            ? new ClaudeOrderReader(new AnthropicClient { ApiKey = key }, model)
            : new RuleOrderReader());
        return services;
    }

    public static IEndpointRouteBuilder MapOrderDrafts(this IEndpointRouteBuilder app)
    {
        // A list photo (up to 1.5 MB) arrives as base64 in JSON, about a third larger: above the 1 MB default for other requests.
        app.MapPost("/api/v1/me/orders/draft", DraftAsync).WithTags("Household planner").RequireAuthorization()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2_200_000));
        app.MapGet("/api/v1/me/drafts", PendingAsync).WithTags("Household planner").RequireAuthorization();
        app.MapPost("/api/v1/me/drafts/{id:guid}/done", DoneAsync).WithTags("Household planner").RequireAuthorization();
        return app;
    }

    private static async Task<IResult> DraftAsync(
        DraftRequest request, ClaimsPrincipal user, IOrderReader reader, AppDbContext db, TimeProvider clock, ILogger<AiProposal> logger, CancellationToken ct)
    {
        if (!await AiPrivacy.HasConsentedAsync(db, user, ct))
        {
            return AiPrivacy.NeedsConsent();
        }

        var text = string.IsNullOrWhiteSpace(request.Text) ? null : request.Text.Trim();
        var (mediaType, base64) = ParseImage(request.ImageDataUrl);
        var errors = new Dictionary<string, string[]>();
        if (text is null && base64 is null)
        {
            errors["Text"] = ["Write what you need, or add a photo of your list."];
        }

        if (text?.Length > MaxTextLength)
        {
            errors["Text"] = ["Keep the note under 2000 characters."];
        }

        if (request.ImageDataUrl is not null && base64 is null)
        {
            errors["ImageDataUrl"] = ["Send a JPEG, PNG or WebP photo under 1.5 MB."];
        }

        if (base64 is not null && !reader.CanReadImages)
        {
            errors["ImageDataUrl"] = ["Reading photos needs the AI switched on (an API key on the server). Type the list instead."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        IReadOnlyList<ReadLine> read;
        try
        {
            read = await reader.ReadAsync(new OrderInput(text, mediaType, base64), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Order reading failed ({Reader})", reader.Name);
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Couldn't read the order right now. Try again, or type it.");
        }

        var lines = read.Select(Check).OfType<DraftLineDto>().Take(MaxLines).ToList();
        var proposal = new AiProposal
        {
            UserId = user.UserId()!.Value,
            InputText = text,
            HadImage = base64 is not null,
            Reader = reader.Name,
            Lines = JsonSerializer.Serialize(lines),
            CreatedAt = clock.GetUtcNow(),
            ReviewedAt = clock.GetUtcNow(), // checked on the spot, in the app
        };
        db.AiProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new DraftDto(proposal.Id, reader.Name, lines));
    }

    /// <summary>Orders that came in on WhatsApp and are waiting for the household to check them, newest first.</summary>
    private static async Task<IReadOnlyList<PendingDraftDto>> PendingAsync(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        var pending = await db.AiProposals.AsNoTracking()
            .Where(p => p.UserId == userId && p.Source == ProposalSource.WhatsApp && p.ReviewedAt == null)
            .OrderByDescending(p => p.CreatedAt).Take(10).ToListAsync(ct);
        return pending.Select(p => new PendingDraftDto(p.Id, p.CreatedAt, p.InputText, p.HadImage,
            JsonSerializer.Deserialize<List<DraftLineDto>>(p.Lines) ?? [])).ToList();
    }

    /// <summary>The household has checked a WhatsApp draft (added it to a list, or set it aside).</summary>
    private static async Task<IResult> DoneAsync(Guid id, ClaimsPrincipal user, AppDbContext db, TimeProvider clock, CancellationToken ct)
    {
        var userId = user.UserId()!.Value;
        await db.AiProposals.Where(p => p.Id == id && p.UserId == userId && p.ReviewedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReviewedAt, clock.GetUtcNow()), ct);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// The server's check of every line, whatever read it: ids must be on the common list, quantities sensible, the
    /// unit known. Anything doubtful drops to low confidence for the household to fix; nothing is trusted blindly.
    /// </summary>
    public static DraftLineDto? Check(ReadLine line)
    {
        var said = line.Said.Trim();
        if (said.Length == 0)
        {
            return null;
        }

        var item = line.CatalogueItemId is { } id ? CommonCatalogue.Items.SingleOrDefault(i => i.Id == id) : null;
        var quantity = line.Quantity is > 0 and <= 1000 ? decimal.Round(line.Quantity.Value, 3) : (decimal?)null;
        var confidence = line.Confidence;
        var reason = line.Reason;
        if (line.CatalogueItemId is not null && item is null)
        {
            (confidence, reason) = (ReadConfidence.Low, "Not on the common list.");
        }
        else if (line.Quantity is not null && quantity is null)
        {
            (confidence, reason) = (ReadConfidence.Low, "Quantity looks wrong.");
        }

        var name = item?.Name ?? (string.IsNullOrWhiteSpace(line.Name) ? said : line.Name.Trim());
        return new DraftLineDto(said[..Math.Min(said.Length, 200)], item?.Id, name[..Math.Min(name.Length, 120)], quantity,
            line.Unit ?? item?.Unit, confidence, reason);
    }

    private static (string? MediaType, string? Base64) ParseImage(string? dataUrl)
    {
        var parts = dataUrl?.Split(',', 2);
        if (parts is not [var head, var data] || !head.StartsWith("data:", StringComparison.Ordinal) || !head.EndsWith(";base64", StringComparison.Ordinal))
        {
            return (null, null);
        }

        var type = head[5..^7];
        if (type is not ("image/jpeg" or "image/png" or "image/webp") || data.Length * 3 / 4 > MaxImageBytes)
        {
            return (null, null);
        }

        try
        {
            Convert.FromBase64String(data);
            return (type, data);
        }
        catch (FormatException)
        {
            return (null, null);
        }
    }
}
