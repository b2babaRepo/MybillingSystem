using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing shops are numbered in the order they opened; new ones take the next number from the sequence.
            migrationBuilder.Sql("""
                CREATE SEQUENCE shop_number_seq START 1;
                ALTER TABLE shops ADD COLUMN number bigint;
                UPDATE shops s SET number = o.n
                FROM (SELECT id, row_number() OVER (ORDER BY created_at, id) AS n FROM shops) o
                WHERE s.id = o.id;
                SELECT setval('shop_number_seq', COALESCE((SELECT max(number) FROM shops), 0) + 1, false);
                ALTER TABLE shops ALTER COLUMN number SET DEFAULT nextval('shop_number_seq');
                ALTER TABLE shops ALTER COLUMN number SET NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_shops_number",
                table: "shops",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shops_number",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "number",
                table: "shops");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS shop_number_seq;");
        }
    }
}
