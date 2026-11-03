using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalonCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hair_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    skin_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    allergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    colour_formula = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    patch_test_on = table.Column<DateOnly>(type: "date", nullable: true),
                    patch_test_reaction = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_cards", x => x.id);
                    table.UniqueConstraint("AK_client_cards_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "service_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_products", x => x.id);
                    table.UniqueConstraint("AK_service_products_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_cards_shop_id_customer_id",
                table: "client_cards",
                columns: new[] { "shop_id", "customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_products_shop_id_service_id_product_id",
                table: "service_products",
                columns: new[] { "shop_id", "service_id", "product_id" },
                unique: true);

            foreach (var table in new[] { "service_products", "client_cards" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY shop_isolation ON {table}
                        USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                        WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_cards");

            migrationBuilder.DropTable(
                name: "service_products");
        }
    }
}
