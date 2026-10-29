using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopTypesAndGst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "shops",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Kirana");

            migrationBuilder.AddColumn<decimal>(
                name: "gst_rate",
                table: "products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hsn_code",
                table: "products",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "gst_rate",
                table: "products");

            migrationBuilder.DropColumn(
                name: "hsn_code",
                table: "products");
        }
    }
}
