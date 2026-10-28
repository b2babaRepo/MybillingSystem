using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DisputesAndUpiRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "upi_ref",
                table: "ledger_entries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolution = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disputes", x => x.id);
                    table.UniqueConstraint("AK_disputes_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_disputes_customers_shop_id_customer_id",
                        columns: x => new { x.shop_id, x.customer_id },
                        principalTable: "customers",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_disputes_ledger_entries_shop_id_entry_id",
                        columns: x => new { x.shop_id, x.entry_id },
                        principalTable: "ledger_entries",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries",
                columns: new[] { "shop_id", "upi_ref" },
                unique: true,
                filter: "upi_ref IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_entry_id",
                table: "disputes",
                column: "entry_id",
                unique: true,
                filter: "resolved_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_disputes_shop_id_customer_id",
                table: "disputes",
                columns: new[] { "shop_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_shop_id_entry_id",
                table: "disputes",
                columns: new[] { "shop_id", "entry_id" });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_shop_id_resolved_at",
                table: "disputes",
                columns: new[] { "shop_id", "resolved_at" });

            // Shop-owned like the ledger: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE disputes ENABLE ROW LEVEL SECURITY;
                ALTER TABLE disputes FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON disputes
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "disputes");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "upi_ref",
                table: "ledger_entries");
        }
    }
}
