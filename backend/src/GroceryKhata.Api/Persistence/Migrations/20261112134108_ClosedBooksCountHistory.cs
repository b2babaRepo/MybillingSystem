using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClosedBooksCountHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cash_counts_shop_id_day",
                table: "cash_counts");

            migrationBuilder.AddColumn<DateOnly>(
                name: "books_closed_until",
                table: "shops",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_cash_counts_shop_id_day",
                table: "cash_counts",
                columns: new[] { "shop_id", "day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cash_counts_shop_id_day",
                table: "cash_counts");

            migrationBuilder.DropColumn(
                name: "books_closed_until",
                table: "shops");

            migrationBuilder.CreateIndex(
                name: "IX_cash_counts_shop_id_day",
                table: "cash_counts",
                columns: new[] { "shop_id", "day" },
                unique: true);
        }
    }
}
