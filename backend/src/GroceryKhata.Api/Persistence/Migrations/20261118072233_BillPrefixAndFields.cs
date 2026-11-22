using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillPrefixAndFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bill_fields",
                table: "shops",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bill_prefix",
                table: "shops",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fields",
                table: "bills",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prefix",
                table: "bills",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bill_fields",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "bill_prefix",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "fields",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "prefix",
                table: "bills");
        }
    }
}
