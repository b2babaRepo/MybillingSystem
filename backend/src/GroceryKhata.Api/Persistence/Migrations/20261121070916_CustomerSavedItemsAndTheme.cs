using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerSavedItemsAndTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "store_theme",
                table: "shops",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    was_out = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_saved_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_items_user_id_shop_id_product_id",
                table: "saved_items",
                columns: new[] { "user_id", "shop_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_items_was_out",
                table: "saved_items",
                column: "was_out",
                filter: "was_out");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_items");

            migrationBuilder.DropColumn(
                name: "store_theme",
                table: "shops");
        }
    }
}
