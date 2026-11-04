using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DairyFarmerPhones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dairy_farmer_phones",
                columns: table => new
                {
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_farmer_phones", x => new { x.phone, x.farmer_id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_dairy_farmer_phones_farmer_id",
                table: "dairy_farmer_phones",
                column: "farmer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dairy_farmer_phones");
        }
    }
}
