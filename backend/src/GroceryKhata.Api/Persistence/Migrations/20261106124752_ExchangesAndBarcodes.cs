using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExchangesAndBarcodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "exchange_bill_id",
                table: "credit_notes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "for_exchange",
                table: "credit_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "exchange_credit",
                table: "bills",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "exchange_note_id",
                table: "bills",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exchange_bill_id",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "for_exchange",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "exchange_credit",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "exchange_note_id",
                table: "bills");
        }
    }
}
