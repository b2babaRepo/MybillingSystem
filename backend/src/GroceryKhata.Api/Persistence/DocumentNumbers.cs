using Microsoft.EntityFrameworkCore;

namespace GroceryKhata.Api.Persistence;

/// <summary>
/// The next number for a shop's estimates, jobs, girvi loans and other numbered papers. Taken from one counter row per
/// shop and kind (doc_counters), so two counters saving at once queue on that row instead of both reading the same
/// highest number and one failing on the unique (shop_id, number) key. Bills keep their own counter (bill_counters).
/// </summary>
public static class DocumentNumbers
{
    /// <summary>
    /// The next number for <typeparamref name="T"/> in the current shop; within <paramref name="financialYear"/> for
    /// papers numbered afresh each year. A counter starts from the highest number already saved, and never falls behind
    /// a number saved some other way. Inside a transaction the number is only used if the transaction commits;
    /// outside one it is used once taken, so a save that then fails leaves a gap.
    /// </summary>
    public static async Task<int> NextNumberAsync<T>(this AppDbContext db, CancellationToken ct, string? financialYear = null)
        where T : class
    {
        var entity = db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"{typeof(T).Name} is not mapped.");
        // Names from the model, never from a request.
        var table = entity.GetTableName()!;
        var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, entity.GetSchema());
        string Column(string property) => entity.FindProperty(property)?.GetColumnName(store)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no {property}.");
        var number = Column("Number");
        var shopId = Column("ShopId");
        var yearFilter = financialYear is null ? "" : $" AND {Column("FinancialYear")} = @p1";
        var kind = financialYear is null ? table : $"{table}:{financialYear}";
        // Table and column names come from the model above; the kind and year go in as parameters.
        var sql = $"""
            INSERT INTO doc_counters (shop_id, kind, last_number)
            VALUES (NULLIF(current_setting('app.shop_id', true), '')::uuid, @p0,
                    (SELECT COALESCE(MAX({number}), 0) FROM {table}
                     WHERE {shopId} = NULLIF(current_setting('app.shop_id', true), '')::uuid{yearFilter}) + 1)
            ON CONFLICT (shop_id, kind) DO UPDATE SET last_number = GREATEST(doc_counters.last_number + 1, EXCLUDED.last_number)
            RETURNING last_number AS "Value"
            """;
        return (await db.Database.SqlQueryRaw<int>(sql, financialYear is null ? [new Npgsql.NpgsqlParameter("p0", kind)] : [new Npgsql.NpgsqlParameter("p0", kind), new Npgsql.NpgsqlParameter("p1", financialYear)])
            .ToListAsync(ct)).Single();
    }
}
