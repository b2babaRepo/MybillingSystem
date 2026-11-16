using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DebitNoteNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "debit_note_no",
                table: "goods_receipts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_goods_receipts_shop_id_debit_note_no",
                table: "goods_receipts",
                columns: new[] { "shop_id", "debit_note_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_goods_receipts_shop_id_debit_note_no",
                table: "goods_receipts");

            migrationBuilder.DropColumn(
                name: "debit_note_no",
                table: "goods_receipts");
        }
    }
}
