using Microsoft.EntityFrameworkCore.Migrations;

namespace GroceryKhata.Api.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Row-level security on shop-owned tables: each shop sees and writes only its own rows (app.shop_id, set per request).
    /// Every new shop-owned table needs this in its migration.
    /// </summary>
    public static void EnableShopIsolation(this MigrationBuilder migrationBuilder, params string[] tables)
    {
        foreach (var table in tables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON {table}
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }
    }
}
