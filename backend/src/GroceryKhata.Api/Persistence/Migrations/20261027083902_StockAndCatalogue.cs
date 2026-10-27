using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockAndCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "products",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reorder_level",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "stock",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cost_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_bills_shop_id_bill_id",
                        columns: x => new { x.shop_id, x.bill_id },
                        principalTable: "bills",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_products_shop_id_product_id",
                        columns: x => new { x.shop_id, x.product_id },
                        principalTable: "products",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id_category",
                table: "products",
                columns: new[] { "shop_id", "category" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_shop_id_bill_id",
                table: "stock_movements",
                columns: new[] { "shop_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_shop_id_product_id_occurred_at",
                table: "stock_movements",
                columns: new[] { "shop_id", "product_id", "occurred_at" });

            // Shop-owned like products: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE stock_movements ENABLE ROW LEVEL SECURITY;
                ALTER TABLE stock_movements FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON stock_movements
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropIndex(
                name: "IX_products_shop_id_category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "products");

            migrationBuilder.DropColumn(
                name: "reorder_level",
                table: "products");

            migrationBuilder.DropColumn(
                name: "stock",
                table: "products");
        }
    }
}
