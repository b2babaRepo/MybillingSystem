using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoredTax : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "gst_composition",
                table: "shops",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "gstin",
                table: "customers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "bill_of_supply",
                table: "bills",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "buyer_gstin",
                table: "bills",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cgst",
                table: "bill_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "igst",
                table: "bill_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "sgst",
                table: "bill_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "taxable_value",
                table: "bill_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gst_composition",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "gstin",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "bill_of_supply",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "buyer_gstin",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "cgst",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "igst",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "sgst",
                table: "bill_lines");

            migrationBuilder.DropColumn(
                name: "taxable_value",
                table: "bill_lines");
        }
    }
}
