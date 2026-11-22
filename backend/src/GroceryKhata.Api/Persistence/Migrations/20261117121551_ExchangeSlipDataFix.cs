using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeSlipDataFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The two migrations before this updated old rows with row-level security on, which hides every row, so
            // nothing changed. Again, with it off for the moment:
            // - slips used before balances: the bill took all of it (any extra went back as change);
            // - slips already given get their secret code;
            // - returns on bills without GST (no GSTIN, or a composition bill of supply) are return notes.
            migrationBuilder.Sql("""
                ALTER TABLE credit_notes NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE bills NO FORCE ROW LEVEL SECURITY;
                UPDATE credit_notes SET spent = total WHERE exchange_bill_id IS NOT NULL AND spent = 0;
                UPDATE credit_notes SET slip_code = lpad(floor(random() * 10000)::int::text, 4, '0') WHERE for_exchange AND slip_code IS NULL;
                UPDATE credit_notes n SET tax_free = true FROM bills b WHERE n.bill_id = b.id AND (b.gstin IS NULL OR b.bill_of_supply) AND NOT n.tax_free;
                ALTER TABLE bills FORCE ROW LEVEL SECURITY;
                ALTER TABLE credit_notes FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
