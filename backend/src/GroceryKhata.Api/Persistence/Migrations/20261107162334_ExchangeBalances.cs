using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Exchange credit used to move the balance with no khata line. The entries are the truth, so customers touched
            // by an exchange go back to their sum. Row-level security would hide every shop's rows from this script.
            migrationBuilder.Sql("""
                ALTER TABLE customers NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE ledger_entries NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE credit_notes NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE bills NO FORCE ROW LEVEL SECURITY;

                UPDATE customers c SET balance = COALESCE((SELECT sum(e.amount) FROM ledger_entries e WHERE e.customer_id = c.id), 0)
                WHERE c.id IN (SELECT customer_id FROM credit_notes WHERE for_exchange AND customer_id IS NOT NULL)
                   OR c.id IN (SELECT customer_id FROM bills WHERE exchange_credit > 0 AND customer_id IS NOT NULL);

                ALTER TABLE bills FORCE ROW LEVEL SECURITY;
                ALTER TABLE credit_notes FORCE ROW LEVEL SECURITY;
                ALTER TABLE ledger_entries FORCE ROW LEVEL SECURITY;
                ALTER TABLE customers FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
