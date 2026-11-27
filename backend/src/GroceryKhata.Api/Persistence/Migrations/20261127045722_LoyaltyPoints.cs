using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_earn_per",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_point_value",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "loyalty_points",
                table: "bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "loyalty_value",
                table: "bills",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "loyalty_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_entries", x => x.id);
                    table.UniqueConstraint("AK_loyalty_entries_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_entries_bill_id",
                table: "loyalty_entries",
                column: "bill_id",
                filter: "bill_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_entries_shop_id_customer_id",
                table: "loyalty_entries",
                columns: new[] { "shop_id", "customer_id" });

            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE loyalty_entries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE loyalty_entries FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON loyalty_entries
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loyalty_entries");

            migrationBuilder.DropColumn(
                name: "loyalty_earn_per",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "loyalty_point_value",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "loyalty_points",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "loyalty_value",
                table: "bills");
        }
    }
}
