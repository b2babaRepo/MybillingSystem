using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierOrderGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Receipts now read their items from the delivery lines. Any receipt that somehow has none is copied across
            // from its JSON first (row security lifted only while copying), so nothing is lost when the old copy goes.
            migrationBuilder.Sql("""
                ALTER TABLE goods_receipts NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE goods_receipt_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_order_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_lines NO FORCE ROW LEVEL SECURITY;

                INSERT INTO goods_receipt_lines (id, shop_id, supplier_id, receipt_id, purchase_id, order_id, order_line_id, product_id, received, damaged, bags, rate, occurred_at, cancelled_at, created_at, updated_at)
                SELECT gen_random_uuid(), r.shop_id, r.supplier_id, r.id, r.purchase_id, r.purchase_order_id,
                       (SELECT ol.id FROM purchase_order_lines ol WHERE ol.order_id = r.purchase_order_id AND ol.product_id = (l->>'productId')::uuid LIMIT 1),
                       (l->>'productId')::uuid, (l->>'quantity')::numeric, COALESCE((l->>'damaged')::numeric, 0), NULLIF(l->>'bags', '')::numeric,
                       (SELECT pl.unit_cost FROM purchase_lines pl WHERE pl.purchase_id = r.purchase_id AND pl.product_id = (l->>'productId')::uuid LIMIT 1),
                       r.received_at, r.cancelled_at, now(), now()
                FROM goods_receipts r, jsonb_array_elements(r.lines) AS l
                WHERE NOT EXISTS (SELECT 1 FROM goods_receipt_lines a WHERE a.receipt_id = r.id);

                ALTER TABLE goods_receipts FORCE ROW LEVEL SECURITY;
                ALTER TABLE goods_receipt_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_order_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE purchase_lines FORCE ROW LEVEL SECURITY;
                """);
            migrationBuilder.DropColumn(
                name: "lines",
                table: "goods_receipts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lines",
                table: "goods_receipts",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
