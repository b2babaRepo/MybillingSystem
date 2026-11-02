using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class KhataLinkExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "khata_token_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            // Links already shared get their 90 days from today, not cut off at once.
            // customers has forced row-level security: lifted for this one update (the migrator owns the table), then put back.
            migrationBuilder.Sql("""
                ALTER TABLE customers NO FORCE ROW LEVEL SECURITY;
                UPDATE customers SET khata_token_at = now() WHERE khata_token IS NOT NULL;
                ALTER TABLE customers FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "khata_token_at",
                table: "customers");
        }
    }
}
