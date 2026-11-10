using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseOrdersAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "batch_id",
                table: "supplier_return_lines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "purchase_order_id",
                table: "purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    expected_on = table.Column<DateOnly>(type: "date", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                    table.UniqueConstraint("AK_purchase_orders_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "purchase_order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    received = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_order_lines", x => x.id);
                    table.UniqueConstraint("AK_purchase_order_lines_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_order_lines_purchase_orders_order_id",
                        column: x => x.order_id,
                        principalTable: "purchase_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_order_id",
                table: "purchase_order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_shop_id_number",
                table: "purchase_orders",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_shop_id_supplier_id_status",
                table: "purchase_orders",
                columns: new[] { "shop_id", "supplier_id", "status" });

            migrationBuilder.EnableShopIsolation("purchase_orders", "purchase_order_lines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_order_lines");

            migrationBuilder.DropTable(
                name: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "purchase_order_id",
                table: "purchases");

            migrationBuilder.AlterColumn<Guid>(
                name: "batch_id",
                table: "supplier_return_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
