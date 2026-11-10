using System.Text.RegularExpressions;

namespace GroceryKhata.Api.Identity;

/// <summary>
/// What the owner can stop one helper doing. Nothing is stopped unless the owner turns it on, so helpers keep today's
/// rights. Checked on the server for every request (the app also hides what's stopped), so a helper can't get round it.
/// </summary>
public static partial class StaffLimits
{
    /// <summary>Giving udhaar: a bill not paid in full, or a khata credit entry.</summary>
    public const string Udhaar = "udhaar";

    /// <summary>Sales reports, P&amp;L, GST, cash book, cash ledger and bank book.</summary>
    public const string Reports = "reports";

    /// <summary>Entering supplier bills and payments.</summary>
    public const string Purchases = "purchases";

    /// <summary>Adding or changing items, prices and stock.</summary>
    public const string Items = "items";

    /// <summary>Writing kharcha (expenses).</summary>
    public const string Expenses = "expenses";

    /// <summary>Cancelling bills and taking goods back (credit notes).</summary>
    public const string Cancel = "cancel";

    public static readonly string[] All = [Udhaar, Reports, Purchases, Items, Expenses, Cancel];

    [GeneratedRegex(@"^/api/v1/(reports|cash-book|cash-ledger|bank-book|gst)(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ReportPaths();

    [GeneratedRegex(@"^/api/v1/(suppliers|purchases|goods-receipts)(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex PurchasePaths();

    [GeneratedRegex(@"^/api/v1/(products|stock-movements|stocktakes)(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ItemPaths();

    [GeneratedRegex(@"^/api/v1/expenses(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpensePaths();

    [GeneratedRegex(@"^/api/v1/(bills/[0-9a-f-]{36}/cancel|.*credit-note.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex CancelPaths();

    /// <summary>The limit a request runs into, or null. Reading items and suppliers stays open: billing needs them.</summary>
    public static string? Blocks(IReadOnlyCollection<string> blocked, string method, string path)
    {
        if (blocked.Count == 0)
        {
            return null;
        }

        var write = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method);
        return blocked.Contains(Reports) && ReportPaths().IsMatch(path) ? Reports
            : blocked.Contains(Purchases) && write && PurchasePaths().IsMatch(path) ? Purchases
            : blocked.Contains(Items) && write && ItemPaths().IsMatch(path) ? Items
            : blocked.Contains(Expenses) && write && ExpensePaths().IsMatch(path) ? Expenses
            : blocked.Contains(Cancel) && write && CancelPaths().IsMatch(path) ? Cancel
            : null;
    }

    public static string Message(string limit) => limit switch
    {
        Udhaar => "The shop owner has not allowed you to give udhaar. Take the full amount, or ask the owner.",
        Reports => "The shop owner has not allowed you to see reports.",
        Purchases => "The shop owner has not allowed you to enter supplier bills or payments.",
        Items => "The shop owner has not allowed you to change items, prices or stock.",
        Expenses => "The shop owner has not allowed you to write kharcha.",
        _ => "The shop owner has not allowed you to cancel bills or take goods back.",
    };
}
