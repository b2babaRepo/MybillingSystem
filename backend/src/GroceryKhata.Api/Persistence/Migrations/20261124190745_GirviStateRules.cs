using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GirviStateRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "girvi_licence",
                table: "shops",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "girvi_max_yearly_rate",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "girvi_notice_days",
                table: "shops",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "girvi_state",
                table: "shops",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "girvi_licence",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "girvi_max_yearly_rate",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "girvi_notice_days",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "girvi_state",
                table: "shops");
        }
    }
}
