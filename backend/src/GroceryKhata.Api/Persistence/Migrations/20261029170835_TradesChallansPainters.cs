using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TradesChallansPainters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gstin",
                table: "shops",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "customers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "painter_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "site",
                table: "bills",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_number",
                table: "bills",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gst_rate",
                table: "bill_lines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hsn_code",
                table: "bill_lines",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "challans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    site = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    vehicle_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_challans", x => x.id);
                    table.UniqueConstraint("AK_challans_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_challans_customers_shop_id_customer_id",
                        columns: x => new { x.shop_id, x.customer_id },
                        principalTable: "customers",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "painters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    commission_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_painters", x => x.id);
                    table.UniqueConstraint("AK_painters_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bills_shop_id_painter_id_occurred_at",
                table: "bills",
                columns: new[] { "shop_id", "painter_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_challans_shop_id_customer_id_invoice_id",
                table: "challans",
                columns: new[] { "shop_id", "customer_id", "invoice_id" });

            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE challans ENABLE ROW LEVEL SECURITY;
                ALTER TABLE challans FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON challans
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
            migrationBuilder.Sql("""
                ALTER TABLE painters ENABLE ROW LEVEL SECURITY;
                ALTER TABLE painters FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON painters
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "challans");

            migrationBuilder.DropTable(
                name: "painters");

            migrationBuilder.DropIndex(
                name: "IX_bills_shop_id_painter_id_occurred_at",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "gstin",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "painter_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "site",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "vehicle_number",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "gst_rate",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "hsn_code",
                table: "bill_lines");
        }
    }
}
