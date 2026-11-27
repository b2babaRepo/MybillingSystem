using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JewelleryCashRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cash_payout_limit",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 10000m);

            migrationBuilder.AddColumn<decimal>(
                name: "cash_receipt_limit",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 200000m);

            migrationBuilder.AddColumn<decimal>(
                name: "pan_from",
                table: "shops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 200000m);

            // Two earlier columns went in at 0 for shops that already existed: every exchange slip of theirs counted as
            // expired, and helpers could not take any slip. Give them the intended 90 days and Rs 5,000.
            migrationBuilder.Sql("UPDATE shops SET exchange_slip_days = 90 WHERE exchange_slip_days = 0");
            migrationBuilder.Sql("UPDATE shops SET exchange_slip_helper_max = 5000 WHERE exchange_slip_helper_max = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cash_payout_limit",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "cash_receipt_limit",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "pan_from",
                table: "shops");
        }
    }
}
