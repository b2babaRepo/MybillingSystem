using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockLedgerStage1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ref_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ref_type",
                table: "stock_movements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "staff_max_write_off",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 1000m);

            migrationBuilder.CreateTable(
                name: "stocktakes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    taken_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    items = table.Column<int>(type: "integer", nullable: false),
                    differences = table.Column<int>(type: "integer", nullable: false),
                    value_difference = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stocktakes", x => x.id);
                    table.UniqueConstraint("AK_stocktakes_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_stocktakes_shop_id_number",
                table: "stocktakes",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.EnableShopIsolation("stocktakes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stocktakes");

            migrationBuilder.DropColumn(
                name: "ref_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "ref_type",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "staff_max_write_off",
                table: "shops");
        }
    }
}
