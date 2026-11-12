using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShipmentPhotoAndDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "photo",
                table: "order_shipments",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_type",
                table: "order_shipments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "photo",
                table: "order_shipments");

            migrationBuilder.DropColumn(
                name: "photo_type",
                table: "order_shipments");
        }
    }
}
