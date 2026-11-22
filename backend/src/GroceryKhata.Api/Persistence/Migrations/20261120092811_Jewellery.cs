using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Jewellery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jewellery_bill_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    piece_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tag_no = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purity = table.Column<int>(type: "integer", nullable: false),
                    gross = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    stone = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    net = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    rate_per_gram = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    metal_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    wastage_percent = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    wastage_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    making_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    making_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    making_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    stone_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    hallmark_charge = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    huid = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    fixed_price = table.Column<bool>(type: "boolean", nullable: false),
                    taxable = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    gst = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jewellery_bill_lines", x => x.id);
                    table.UniqueConstraint("AK_jewellery_bill_lines_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "jewellery_bills",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    money_paid = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_metal_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    old_metal_expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jewellery_bills", x => x.id);
                    table.UniqueConstraint("AK_jewellery_bills_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "jewellery_pieces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_no = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purity = table.Column<int>(type: "integer", nullable: false),
                    gross = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    stone = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    wastage_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    making_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    making_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    stone_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    hallmark_charge = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    huid = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    fixed_price = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    sold_bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sold_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jewellery_pieces", x => x.id);
                    table.UniqueConstraint("AK_jewellery_pieces_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "metal_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purity = table.Column<int>(type: "integer", nullable: false),
                    sell_per_gram = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    buy_per_gram = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    set_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metal_rates", x => x.id);
                    table.UniqueConstraint("AK_metal_rates_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "old_metal_purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    gross = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    reading = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fine = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    rate_per_gram = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deduction_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_old_metal_purchases", x => x.id);
                    table.UniqueConstraint("AK_old_metal_purchases_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_bill_lines_shop_id_bill_id",
                table: "jewellery_bill_lines",
                columns: new[] { "shop_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_bills_shop_id_bill_id",
                table: "jewellery_bills",
                columns: new[] { "shop_id", "bill_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_bills_shop_id_customer_id_created_at",
                table: "jewellery_bills",
                columns: new[] { "shop_id", "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_pieces_shop_id_huid",
                table: "jewellery_pieces",
                columns: new[] { "shop_id", "huid" });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_pieces_shop_id_sold_bill_id",
                table: "jewellery_pieces",
                columns: new[] { "shop_id", "sold_bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_jewellery_pieces_shop_id_tag_no",
                table: "jewellery_pieces",
                columns: new[] { "shop_id", "tag_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metal_rates_shop_id_metal_purity_created_at",
                table: "metal_rates",
                columns: new[] { "shop_id", "metal", "purity", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_old_metal_purchases_shop_id_bill_id",
                table: "old_metal_purchases",
                columns: new[] { "shop_id", "bill_id" });
        
            migrationBuilder.Sql("""
                ALTER TABLE metal_rates ENABLE ROW LEVEL SECURITY;
                ALTER TABLE metal_rates FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON metal_rates
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE jewellery_pieces ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jewellery_pieces FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON jewellery_pieces
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE jewellery_bills ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jewellery_bills FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON jewellery_bills
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE jewellery_bill_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE jewellery_bill_lines FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON jewellery_bill_lines
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE old_metal_purchases ENABLE ROW LEVEL SECURITY;
                ALTER TABLE old_metal_purchases FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON old_metal_purchases
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "jewellery_bill_lines");

            migrationBuilder.DropTable(
                name: "jewellery_bills");

            migrationBuilder.DropTable(
                name: "jewellery_pieces");

            migrationBuilder.DropTable(
                name: "metal_rates");

            migrationBuilder.DropTable(
                name: "old_metal_purchases");
        }
    }
}
