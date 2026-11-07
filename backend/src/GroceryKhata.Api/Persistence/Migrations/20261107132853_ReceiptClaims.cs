using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "claimed_by_customer_id",
                table: "incoming_receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_incoming_receipts_shop_id_claimed_by_customer_id",
                table: "incoming_receipts",
                columns: new[] { "shop_id", "claimed_by_customer_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_incoming_receipts_customers_shop_id_claimed_by_customer_id",
                table: "incoming_receipts",
                columns: new[] { "shop_id", "claimed_by_customer_id" },
                principalTable: "customers",
                principalColumns: new[] { "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incoming_receipts_customers_shop_id_claimed_by_customer_id",
                table: "incoming_receipts");

            migrationBuilder.DropIndex(
                name: "IX_incoming_receipts_shop_id_claimed_by_customer_id",
                table: "incoming_receipts");

            migrationBuilder.DropColumn(
                name: "claimed_by_customer_id",
                table: "incoming_receipts");
        }
    }
}
