using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ArrivalLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "short_closed",
                table: "purchase_order_lines",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "short_reason",
                table: "purchase_order_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "goods_receipt_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    damaged = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    bags = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_receipt_lines", x => x.id);
                    table.UniqueConstraint("AK_goods_receipt_lines_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_shop_id_order_id",
                table: "goods_receipt_lines",
                columns: new[] { "shop_id", "order_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_shop_id_product_id_occurred_at",
                table: "goods_receipt_lines",
                columns: new[] { "shop_id", "product_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_shop_id_purchase_id",
                table: "goods_receipt_lines",
                columns: new[] { "shop_id", "purchase_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_shop_id_receipt_id",
                table: "goods_receipt_lines",
                columns: new[] { "shop_id", "receipt_id" });

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipt_lines_shop_id_supplier_id_occurred_at",
                table: "goods_receipt_lines",
                columns: new[] { "shop_id", "supplier_id", "occurred_at" });

            // Back-fill every past arrival: receipt lines from their JSON, and each bill's own share beyond its receipts.
            // Row security is lifted only while copying, then forced on the new table like every other.
            migrationBuilder.Sql("""
                ALTER TABLE goods_receipts NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchases NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_order_lines NO FORCE ROW LEVEL SECURITY;

                INSERT INTO goods_receipt_lines (id, shop_id, supplier_id, receipt_id, purchase_id, order_id, order_line_id, product_id, received, damaged, bags, rate, occurred_at, cancelled_at, created_at, updated_at)
                SELECT gen_random_uuid(), r.shop_id, r.supplier_id, r.id, r.purchase_id, r.purchase_order_id,
                       (SELECT ol.id FROM purchase_order_lines ol WHERE ol.order_id = r.purchase_order_id AND ol.product_id = (l->>'productId')::uuid LIMIT 1),
                       (l->>'productId')::uuid, (l->>'quantity')::numeric, COALESCE((l->>'damaged')::numeric, 0), NULLIF(l->>'bags', '')::numeric,
                       (SELECT pl.unit_cost FROM purchase_lines pl WHERE pl.purchase_id = r.purchase_id AND pl.product_id = (l->>'productId')::uuid LIMIT 1),
                       r.received_at, r.cancelled_at, now(), now()
                FROM goods_receipts r, jsonb_array_elements(r.lines) AS l;

                INSERT INTO goods_receipt_lines (id, shop_id, supplier_id, receipt_id, purchase_id, order_id, order_line_id, product_id, received, damaged, bags, rate, occurred_at, cancelled_at, created_at, updated_at)
                SELECT gen_random_uuid(), p.shop_id, p.supplier_id, NULL, p.id, p.purchase_order_id,
                       (SELECT ol.id FROM purchase_order_lines ol WHERE ol.order_id = p.purchase_order_id AND ol.product_id = pl.product_id LIMIT 1),
                       pl.product_id, pl.quantity + pl.free_quantity - pl.from_receipt, 0,
                       CASE WHEN pl.bags > 0 AND pl.quantity + pl.free_quantity > 0 THEN round(pl.bags * (pl.quantity + pl.free_quantity - pl.from_receipt) / (pl.quantity + pl.free_quantity), 3) END,
                       pl.unit_cost, p.occurred_at, p.cancelled_at, now(), now()
                FROM purchase_lines pl JOIN purchases p ON p.id = pl.purchase_id
                WHERE pl.quantity + pl.free_quantity - pl.from_receipt > 0;

                ALTER TABLE goods_receipts FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchases FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_order_lines FORCE ROW LEVEL SECURITY;
                """);
            migrationBuilder.EnableShopIsolation("goods_receipt_lines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "goods_receipt_lines");

            migrationBuilder.DropColumn(
                name: "short_closed",
                table: "purchase_order_lines");

            migrationBuilder.DropColumn(
                name: "short_reason",
                table: "purchase_order_lines");
        }
    }
}
