using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_markets_name",
                table: "markets");

            migrationBuilder.AddColumn<string>(
                name: "district",
                table: "markets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "markets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "village",
                table: "markets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_markets_name",
                table: "markets",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_markets_name",
                table: "markets");

            migrationBuilder.DropColumn(
                name: "district",
                table: "markets");

            migrationBuilder.DropColumn(
                name: "state",
                table: "markets");

            migrationBuilder.DropColumn(
                name: "village",
                table: "markets");

            migrationBuilder.CreateIndex(
                name: "IX_markets_name",
                table: "markets",
                column: "name",
                unique: true);
        }
    }
}
