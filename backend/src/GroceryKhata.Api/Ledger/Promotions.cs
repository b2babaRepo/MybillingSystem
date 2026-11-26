namespace GroceryKhata.Api.Ledger;

/// <summary>
/// The promotion engine on the server: the same rules as the app's (frontend lib/promotions.ts), checked against the
/// same cases (shared/promotions.cases.json), so a bill priced on a till with no internet is priced the same here.
/// Item offers never stack: the biggest saving is taken first, and a line it uses isn't used again; then the best bill
/// offer comes off what is left. Money is worked in whole paise.
/// </summary>
public static class Promotions
{
    public sealed record Line(Guid? ProductId, string? Category, decimal Regular, decimal Quantity);

    public sealed record Rule(
        string Id, string Name, string Kind, decimal Value, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset? StoppedAt,
        string Scope, string? Category, IReadOnlyList<Guid> ProductIds, int? BuyQty = null, int? GetQty = null, decimal? MinQty = null,
        decimal? MinBill = null, decimal? ComboPrice = null, int? Days = null, int? FromMinute = null, int? ToMinute = null);

    public sealed record Part(decimal Quantity, decimal UnitPrice, string? OfferId, string? OfferName);

    public sealed record BillOff(string OfferId, string OfferName, decimal Amount);

    public sealed record Result(IReadOnlyList<IReadOnlyList<Part>> Parts, BillOff? Bill, decimal Saved);

    private static readonly TimeSpan India = TimeSpan.FromHours(5.5);

    private static long Paise(decimal rupees) => (long)Math.Round(rupees * 100, MidpointRounding.AwayFromZero);

    private static decimal Rupees(long paise) => paise / 100m;

    private static long Round(decimal x) => (long)Math.Round(x, MidpointRounding.AwayFromZero);

    public static bool RunsAt(Rule p, DateTimeOffset at)
    {
        var end = p.StoppedAt is { } stopped && stopped < p.EndsAt ? stopped : p.EndsAt;
        if (!(p.StartsAt <= at && at < end))
        {
            return false;
        }

        var india = at.ToOffset(India);
        if (p.Days is { } days && ((days >> (int)india.DayOfWeek) & 1) == 0)
        {
            return false;
        }

        if (p is { FromMinute: { } from, ToMinute: { } to })
        {
            var minute = (india.Hour * 60) + india.Minute;
            return from <= to ? from <= minute && minute < to : minute >= from || minute < to;
        }

        return true;
    }

    private static bool Covers(Rule p, Line line) =>
        line.ProductId is { } id && p.Scope switch
        {
            "all" => true,
            "category" => line.Category is { } c && p.Category is { } want && string.Equals(c, want, StringComparison.OrdinalIgnoreCase),
            _ => p.ProductIds.Contains(id),
        };

    private sealed record Candidate(long Saving, int Order, IReadOnlyList<int> Lines, Dictionary<int, List<Part>> Parts);

    public static Result Apply(IReadOnlyList<Line> lines, IReadOnlyList<Rule> rules, DateTimeOffset at)
    {
        var running = rules.Where(r => RunsAt(r, at)).ToList();
        var regular = lines.Select(l => Paise(l.Regular)).ToArray();
        var candidates = new List<Candidate>();
        Part PartOf(Rule p, decimal quantity, long unit) => new(quantity, Rupees(unit), p.Id, p.Name);
        List<Part> Rest(int i, decimal quantity) => quantity > 0 ? [new Part(decimal.Round(quantity, 3, MidpointRounding.AwayFromZero), Rupees(regular[i]), null, null)] : [];

        for (var order = 0; order < running.Count; order++)
        {
            var p = running[order];
            var covered = Enumerable.Range(0, lines.Count).Where(i => Covers(p, lines[i]) && lines[i].Quantity > 0 && regular[i] > 0).ToList();
            switch (p.Kind)
            {
                case "percent" or "amount":
                    foreach (var i in covered)
                    {
                        var price = Math.Max(0, p.Kind == "percent" ? Round(regular[i] * (100 - p.Value) / 100) : regular[i] - Paise(p.Value));
                        var saving = Round((regular[i] - price) * lines[i].Quantity);
                        if (saving > 0)
                        {
                            candidates.Add(new Candidate(saving, order, [i], new() { [i] = [PartOf(p, lines[i].Quantity, price)] }));
                        }
                    }

                    break;
                case "buy_get" when p.BuyQty >= 1 && p.GetQty >= 1:
                    foreach (var i in covered)
                    {
                        var sets = (long)Math.Floor(Math.Floor(lines[i].Quantity) / (p.BuyQty!.Value + p.GetQty!.Value));
                        var free = sets * p.GetQty!.Value;
                        if (free > 0)
                        {
                            candidates.Add(new Candidate(free * regular[i], order, [i], new() { [i] = [.. Rest(i, lines[i].Quantity - free), PartOf(p, free, 0)] }));
                        }
                    }

                    break;
                case "slab" when covered.Count > 0 && p.MinQty is { } minQty && covered.Sum(i => lines[i].Quantity) >= minQty:
                {
                    var parts = new Dictionary<int, List<Part>>();
                    long saving = 0;
                    foreach (var i in covered)
                    {
                        var price = Round(regular[i] * (100 - p.Value) / 100);
                        saving += Round((regular[i] - price) * lines[i].Quantity);
                        parts[i] = [PartOf(p, lines[i].Quantity, price)];
                    }

                    if (saving > 0)
                    {
                        candidates.Add(new Candidate(saving, order, covered, parts));
                    }

                    break;
                }

                case "combo" when p.ComboPrice is { } comboPrice && p.ProductIds.Count >= 2:
                {
                    // One line per combo item (the first line holding it); as many combos as the scarcest item allows.
                    var held = p.ProductIds.Select(id => Enumerable.Range(0, lines.Count).FirstOrDefault(i => lines[i].ProductId == id && regular[i] > 0, -1)).ToList();
                    if (held.Any(i => i < 0))
                    {
                        break;
                    }

                    var sets = held.Min(i => Math.Floor(lines[i].Quantity));
                    var full = held.Sum(i => regular[i]);
                    var price = Paise(comboPrice);
                    if (sets < 1 || price >= full)
                    {
                        break;
                    }

                    // Each item's share of the combo price by its regular price; the first item takes the rounding.
                    var shares = held.Select(i => Round((decimal)regular[i] * price / full)).ToArray();
                    shares[0] = Math.Max(0, price - shares.Skip(1).Sum());
                    var parts = new Dictionary<int, List<Part>>();
                    for (var k = 0; k < held.Count; k++)
                    {
                        parts[held[k]] = [PartOf(p, sets, shares[k]), .. Rest(held[k], lines[held[k]].Quantity - sets)];
                    }

                    candidates.Add(new Candidate((long)sets * (full - price), order, held, parts));
                    break;
                }
            }
        }

        // Biggest saving first; a tie goes to the offer listed first, then the earlier line.
        var chosen = new List<Part>[lines.Count];
        for (var i = 0; i < lines.Count; i++)
        {
            chosen[i] = [new Part(lines[i].Quantity, Rupees(regular[i]), null, null)];
        }

        var used = new HashSet<int>();
        long saved = 0;
        foreach (var c in candidates.OrderByDescending(c => c.Saving).ThenBy(c => c.Order).ThenBy(c => c.Lines[0]))
        {
            if (c.Lines.Any(used.Contains))
            {
                continue;
            }

            foreach (var i in c.Lines)
            {
                used.Add(i);
                chosen[i] = c.Parts[i];
            }

            saved += c.Saving;
        }

        // The bill offer comes off what is left after item offers.
        var subtotal = chosen.Sum(ps => ps.Sum(x => Round(Paise(x.UnitPrice) * x.Quantity)));
        BillOff? bill = null;
        long best = 0;
        foreach (var p in running.Where(r => r.Kind is "bill_percent" or "bill_amount"))
        {
            if (subtotal <= 0 || subtotal < Paise(p.MinBill ?? 0))
            {
                continue;
            }

            var off = Math.Min(subtotal - 1, p.Kind == "bill_percent" ? Round(subtotal * p.Value / 100) : Paise(p.Value));
            if (off > best)
            {
                (best, bill) = (off, new BillOff(p.Id, p.Name, Rupees(off)));
            }
        }

        return new Result(chosen.Select(ps => (IReadOnlyList<Part>)ps).ToList(), bill, Rupees(saved + best));
    }
}
