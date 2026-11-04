using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TripCashPhotosSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "trip_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ledger_entry_id",
                table: "trip_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trip_id",
                table: "ledger_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trip_id",
                table: "expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "trip_photos",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_photos", x => x.entry_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_photos_shop_id",
                table: "trip_photos",
                column: "shop_id");
        
            migrationBuilder.EnableShopIsolation("trip_photos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_photos");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "trip_entries");

            migrationBuilder.DropColumn(
                name: "ledger_entry_id",
                table: "trip_entries");

            migrationBuilder.DropColumn(
                name: "trip_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "trip_id",
                table: "expenses");
        }
    }
}
