using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SaleOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "offer_id",
                table: "bill_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "offer_name",
                table: "bill_lines",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sale_offers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    product_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    stopped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_offers", x => x.id);
                    table.UniqueConstraint("AK_sale_offers_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_lines_offer_id",
                table: "bill_lines",
                column: "offer_id",
                filter: "offer_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sale_offers_shop_id_ends_at",
                table: "sale_offers",
                columns: new[] { "shop_id", "ends_at" });

            migrationBuilder.EnableShopIsolation("sale_offers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_offers");

            migrationBuilder.DropIndex(
                name: "IX_bill_lines_offer_id",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "offer_id",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "offer_name",
                table: "bill_lines");
        }
    }
}
