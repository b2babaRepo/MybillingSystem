using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fingerprint",
                table: "incoming_receipts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_incoming_receipts_shop_id_fingerprint",
                table: "incoming_receipts",
                columns: new[] { "shop_id", "fingerprint" },
                unique: true,
                filter: "fingerprint IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incoming_receipts_shop_id_fingerprint",
                table: "incoming_receipts");

            migrationBuilder.DropColumn(
                name: "fingerprint",
                table: "incoming_receipts");
        }
    }
}
