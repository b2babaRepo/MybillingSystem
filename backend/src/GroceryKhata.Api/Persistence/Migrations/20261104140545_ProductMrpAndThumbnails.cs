using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductMrpAndThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "mrp",
                table: "products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "thumb_data",
                table: "product_images",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thumb_type",
                table: "product_images",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mrp",
                table: "products");

            migrationBuilder.DropColumn(
                name: "thumb_data",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "thumb_type",
                table: "product_images");
        }
    }
}
