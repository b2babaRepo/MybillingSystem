using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductCatalogueLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "catalogue_item_id",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_catalogue_item_id",
                table: "products",
                column: "catalogue_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_products_shop_id_catalogue_item_id",
                table: "products",
                columns: new[] { "shop_id", "catalogue_item_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_products_catalogue_items_catalogue_item_id",
                table: "products",
                column: "catalogue_item_id",
                principalTable: "catalogue_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_catalogue_items_catalogue_item_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_catalogue_item_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_shop_id_catalogue_item_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "catalogue_item_id",
                table: "products");
        }
    }
}
