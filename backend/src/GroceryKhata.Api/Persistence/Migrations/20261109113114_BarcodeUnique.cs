using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BarcodeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old codes take the one spelling the app now uses (12-digit UPC-A gets its leading 0), and where two active
            // items of a shop share a code, only the oldest keeps it, so the unique rule below can be made.
            migrationBuilder.Sql("""
                ALTER TABLE products NO FORCE ROW LEVEL SECURITY;
                UPDATE products SET barcode = '0' || barcode WHERE barcode ~ '^[0-9]{12}$';
                UPDATE products p SET barcode = NULL
                WHERE p.barcode IS NOT NULL AND p.is_active AND EXISTS (
                    SELECT 1 FROM products o WHERE o.shop_id = p.shop_id AND o.barcode = p.barcode AND o.is_active
                    AND (o.created_at, o.id) < (p.created_at, p.id));
                ALTER TABLE products FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id_barcode",
                table: "products",
                columns: new[] { "shop_id", "barcode" },
                unique: true,
                filter: "barcode IS NOT NULL AND is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_shop_id_barcode",
                table: "products");
        }
    }
}
