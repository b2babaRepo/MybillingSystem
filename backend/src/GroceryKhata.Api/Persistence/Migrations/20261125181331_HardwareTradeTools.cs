using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardwareTradeTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "contractor_discount_percent",
                table: "shops",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "dealer_discount_percent",
                table: "shops",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "price_group",
                table: "customers",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estimate_id",
                table: "challans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "brand_schemes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    brand = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    to = table.Column<DateOnly>(type: "date", nullable: false),
                    basis = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    rebate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    received = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_schemes", x => x.id);
                    table.UniqueConstraint("AK_brand_schemes_shop_id_id", x => new { x.shop_id, x.id });
                });
            migrationBuilder.Sql("""
                ALTER TABLE brand_schemes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE brand_schemes FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON brand_schemes
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                CREATE INDEX "IX_challans_estimate_id" ON challans (estimate_id) WHERE estimate_id IS NOT NULL;
                """);
        
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_challans_estimate_id\";");

            migrationBuilder.DropTable(
                name: "brand_schemes");

            migrationBuilder.DropColumn(
                name: "contractor_discount_percent",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "dealer_discount_percent",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "price_group",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "estimate_id",
                table: "challans");
        }
    }
}
