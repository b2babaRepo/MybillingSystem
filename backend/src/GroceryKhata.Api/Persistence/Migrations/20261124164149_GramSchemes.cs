using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GramSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "scheme_plans",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "amount");

            migrationBuilder.AddColumn<int>(
                name: "purity",
                table: "scheme_plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "grams",
                table: "scheme_instalments",
                type: "numeric(10,3)",
                precision: 10,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rate_per_gram",
                table: "scheme_instalments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "scheme_plans");

            migrationBuilder.DropColumn(
                name: "purity",
                table: "scheme_plans");

            migrationBuilder.DropColumn(
                name: "grams",
                table: "scheme_instalments");

            migrationBuilder.DropColumn(
                name: "rate_per_gram",
                table: "scheme_instalments");
        }
    }
}
