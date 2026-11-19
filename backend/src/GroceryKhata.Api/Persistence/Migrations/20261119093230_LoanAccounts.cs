using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoanAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "loan_id",
                table: "cash_moves",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "loan_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    principal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    interest = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    principal_expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    interest_expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    struck_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_payments", x => x.id);
                    table.UniqueConstraint("AK_loan_payments_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lender = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    rate_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    emi = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    received_into = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    struck_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.id);
                    table.UniqueConstraint("AK_loans_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_shop_id_loan_id",
                table: "loan_payments",
                columns: new[] { "shop_id", "loan_id" });

            migrationBuilder.Sql("""
                ALTER TABLE loans ENABLE ROW LEVEL SECURITY;
                ALTER TABLE loans FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON loans
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                ALTER TABLE loan_payments ENABLE ROW LEVEL SECURITY;
                ALTER TABLE loan_payments FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON loan_payments
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_payments");

            migrationBuilder.DropTable(
                name: "loans");

            migrationBuilder.DropColumn(
                name: "loan_id",
                table: "cash_moves");
        }
    }
}
