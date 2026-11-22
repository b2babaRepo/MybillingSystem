using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CardAndCheques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cheque_bank",
                table: "ledger_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "cheque_date",
                table: "ledger_entries",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cheque_no",
                table: "ledger_entries",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cheque_settled_at",
                table: "ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cheque_status",
                table: "ledger_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_shop_id_cheque_status",
                table: "ledger_entries",
                columns: new[] { "shop_id", "cheque_status" },
                filter: "cheque_status IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_shop_id_cheque_status",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "cheque_bank",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "cheque_date",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "cheque_no",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "cheque_settled_at",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "cheque_status",
                table: "ledger_entries");
        }
    }
}
