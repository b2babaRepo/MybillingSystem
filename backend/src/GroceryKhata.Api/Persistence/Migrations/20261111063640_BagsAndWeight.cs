using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BagsAndWeight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "bags",
                table: "stock_movements",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "bags",
                table: "purchase_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "bag_size",
                table: "products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bilty",
                table: "goods_receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "gross_weight",
                table: "goods_receipts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tare_weight",
                table: "goods_receipts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transporter",
                table: "goods_receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vehicle_no",
                table: "goods_receipts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "bags",
                table: "bill_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bags",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "bags",
                table: "purchase_lines");

            migrationBuilder.DropColumn(
                name: "bag_size",
                table: "products");

            migrationBuilder.DropColumn(
                name: "bilty",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "gross_weight",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "tare_weight",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "transporter",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "vehicle_no",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "bags",
                table: "bill_lines");
        }
    }
}
