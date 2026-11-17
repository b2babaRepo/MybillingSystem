using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeSlipCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "exchange_slip_helper_max",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "slip_cancelled_at",
                table: "credit_notes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slip_code",
                table: "credit_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "tax_free",
                table: "credit_notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Slips already handed out get their secret code now (the owner reprints them from the slips list).
            migrationBuilder.Sql("UPDATE credit_notes SET slip_code = lpad(floor(random() * 10000)::int::text, 4, '0') WHERE for_exchange");
            // Returns on bills without GST (no GSTIN, or a composition bill of supply) are return notes, not tax credit notes.
            migrationBuilder.Sql("UPDATE credit_notes n SET tax_free = true FROM bills b WHERE n.bill_id = b.id AND (b.gstin IS NULL OR b.bill_of_supply)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exchange_slip_helper_max",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "slip_cancelled_at",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "slip_code",
                table: "credit_notes");

            migrationBuilder.DropColumn(
                name: "tax_free",
                table: "credit_notes");
        }
    }
}
