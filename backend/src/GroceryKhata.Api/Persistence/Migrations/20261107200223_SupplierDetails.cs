using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupplierDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "suppliers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gstin",
                table: "suppliers",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "payment_days",
                table: "suppliers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "supplier_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "supplier_entries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference",
                table: "supplier_entries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "extra_charges",
                table: "purchases",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Each old line gets its kind where it can be told for certain; the rest stay Other, shown as an adjustment.
            migrationBuilder.Sql("""
                ALTER TABLE supplier_entries NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE suppliers NO FORCE ROW LEVEL SECURITY;

                UPDATE supplier_entries SET kind = CASE
                    WHEN note = 'Bill cancelled' AND amount < 0 THEN 'BillCancelled'
                    WHEN purchase_id IS NOT NULL AND amount > 0 THEN 'Bill'
                    WHEN note LIKE 'Credit note%' OR note LIKE 'Warranty claim%' THEN 'CreditNote'
                    WHEN amount < 0 AND note IS NULL THEN 'Payment'
                    ELSE 'Other' END;
                UPDATE suppliers SET is_active = true;

                ALTER TABLE suppliers FORCE ROW LEVEL SECURITY;
                ALTER TABLE supplier_entries FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "gstin",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_days",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "supplier_entries");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "supplier_entries");

            migrationBuilder.DropColumn(
                name: "reference",
                table: "supplier_entries");

            migrationBuilder.DropColumn(
                name: "extra_charges",
                table: "purchases");
        }
    }
}
