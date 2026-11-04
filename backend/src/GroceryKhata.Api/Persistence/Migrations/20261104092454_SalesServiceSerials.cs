using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesServiceSerials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "tracks_serials",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "warranty_months",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_serials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    serial_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sold_on = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_until = table.Column<DateOnly>(type: "date", nullable: true),
                    warranty_registered = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_serials", x => x.id);
                    table.UniqueConstraint("AK_product_serials_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_shop_id_bill_id",
                table: "product_serials",
                columns: new[] { "shop_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_shop_id_product_id_serial_no",
                table: "product_serials",
                columns: new[] { "shop_id", "product_id", "serial_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_serials_shop_id_serial_no",
                table: "product_serials",
                columns: new[] { "shop_id", "serial_no" });

            migrationBuilder.EnableShopIsolation("product_serials");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_serials");

            migrationBuilder.DropColumn(
                name: "tracks_serials",
                table: "products");

            migrationBuilder.DropColumn(
                name: "warranty_months",
                table: "products");
        }
    }
}
