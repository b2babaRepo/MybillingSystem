using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GirviCompoundYearly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "compound_yearly",
                table: "girvi_loans",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "compound_yearly",
                table: "girvi_loans");
        }
    }
}
