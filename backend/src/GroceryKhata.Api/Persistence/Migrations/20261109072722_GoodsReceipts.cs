using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoodsReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "from_receipt",
                table: "purchase_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "goods_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipts", x => x.id);
                    table.UniqueConstraint("AK_goods_receipts_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_shop_id_number",
                table: "goods_receipts",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_shop_id_supplier_id_purchase_id",
                table: "goods_receipts",
                columns: new[] { "shop_id", "supplier_id", "purchase_id" });

            migrationBuilder.EnableShopIsolation("goods_receipts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "from_receipt",
                table: "purchase_lines");
        }
    }
}
