using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id_updated_at",
                table: "products",
                columns: new[] { "shop_id", "updated_at" });

            // Every change to an item stamps updated_at, whichever way it is saved (bulk stock updates skip the app's
            // own stamp), so a till asking for "changed since" misses nothing.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION products_touch() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.updated_at := clock_timestamp();
                    RETURN NEW;
                END $$;
                CREATE TRIGGER products_touch BEFORE INSERT OR UPDATE ON products FOR EACH ROW EXECUTE FUNCTION products_touch();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS products_touch ON products; DROP FUNCTION IF EXISTS products_touch();");

            migrationBuilder.DropIndex(
                name: "IX_products_shop_id_updated_at",
                table: "products");
        }
    }
}
