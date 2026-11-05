using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MasterItemPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "master_item_id",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "image_at",
                table: "master_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_credit",
                table: "master_items",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "master_item_images",
                columns: table => new
                {
                    master_item_id = table.Column<int>(type: "integer", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    thumb_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    thumb_data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_item_images", x => x.master_item_id);
                    table.ForeignKey(
                        name: "FK_master_item_images_master_items_master_item_id",
                        column: x => x.master_item_id,
                        principalTable: "master_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_master_item_id",
                table: "products",
                column: "master_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_master_items_master_item_id",
                table: "products",
                column: "master_item_id",
                principalTable: "master_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_master_items_master_item_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "master_item_images");

            migrationBuilder.DropIndex(
                name: "IX_products_master_item_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "master_item_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "image_at",
                table: "master_items");

            migrationBuilder.DropColumn(
                name: "image_credit",
                table: "master_items");
        }
    }
}
