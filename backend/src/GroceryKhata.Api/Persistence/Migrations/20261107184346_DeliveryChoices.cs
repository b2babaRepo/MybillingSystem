using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "delivers_goods",
                table: "shops",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "customers",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            // Shops that already deliver keep the row in front of them. Row-level security would hide every shop's bills from this script.
            migrationBuilder.Sql("""
                ALTER TABLE bills NO FORCE ROW LEVEL SECURITY;

                UPDATE shops s SET delivers_goods = true
                WHERE EXISTS (SELECT 1 FROM bills b WHERE b.shop_id = s.id AND (b.site IS NOT NULL OR b.vehicle_number IS NOT NULL
                    OR (b.place_of_supply IS NOT NULL AND s.gstin IS NOT NULL AND b.buyer_gstin IS NULL AND b.place_of_supply <> left(s.gstin, 2))));

                ALTER TABLE bills FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delivers_goods",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "state",
                table: "customers");
        }
    }
}
