using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptsEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries");

            migrationBuilder.CreateTable(
                name: "incoming_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    utr = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    payer_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    disposition_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incoming_receipts", x => x.id);
                    table.UniqueConstraint("AK_incoming_receipts_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_incoming_receipts_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_allocations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_allocations", x => x.id);
                    table.UniqueConstraint("AK_receipt_allocations_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_receipt_allocations_customers_shop_id_customer_id",
                        columns: x => new { x.shop_id, x.customer_id },
                        principalTable: "customers",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_allocations_incoming_receipts_shop_id_receipt_id",
                        columns: x => new { x.shop_id, x.receipt_id },
                        principalTable: "incoming_receipts",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_allocations_ledger_entries_shop_id_ledger_entry_id",
                        columns: x => new { x.shop_id, x.ledger_entry_id },
                        principalTable: "ledger_entries",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries",
                columns: new[] { "shop_id", "upi_ref" },
                filter: "upi_ref IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_incoming_receipts_shop_id_received_at",
                table: "incoming_receipts",
                columns: new[] { "shop_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_incoming_receipts_shop_id_utr",
                table: "incoming_receipts",
                columns: new[] { "shop_id", "utr" },
                unique: true,
                filter: "utr IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_allocations_shop_id_customer_id",
                table: "receipt_allocations",
                columns: new[] { "shop_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_allocations_shop_id_ledger_entry_id",
                table: "receipt_allocations",
                columns: new[] { "shop_id", "ledger_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_allocations_shop_id_receipt_id",
                table: "receipt_allocations",
                columns: new[] { "shop_id", "receipt_id" });

            // Every UTR payment so far becomes a receipt with its allocation. Row-level security would hide every shop's
            // rows from this script, so it is lifted on the ledger only while it runs.
            migrationBuilder.Sql("""
                ALTER TABLE ledger_entries NO FORCE ROW LEVEL SECURITY;

                INSERT INTO incoming_receipts (id, shop_id, source, amount, received_at, utr, created_by_user_id, created_at, updated_at)
                SELECT gen_random_uuid(), shop_id, 'Manual', -amount, occurred_at, upi_ref, created_by_user_id, now(), now()
                FROM ledger_entries WHERE upi_ref IS NOT NULL AND kind = 'Payment';

                INSERT INTO receipt_allocations (id, shop_id, receipt_id, customer_id, ledger_entry_id, amount, user_id, created_at, updated_at)
                SELECT gen_random_uuid(), e.shop_id, r.id, e.customer_id, e.id, -e.amount, e.created_by_user_id, now(), now()
                FROM ledger_entries e JOIN incoming_receipts r ON r.shop_id = e.shop_id AND r.utr = e.upi_ref
                WHERE e.kind = 'Payment';

                ALTER TABLE ledger_entries FORCE ROW LEVEL SECURITY;

                ALTER TABLE incoming_receipts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE incoming_receipts FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON incoming_receipts
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);

                ALTER TABLE receipt_allocations ENABLE ROW LEVEL SECURITY;
                ALTER TABLE receipt_allocations FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON receipt_allocations
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_allocations");

            migrationBuilder.DropTable(
                name: "incoming_receipts");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_shop_id_upi_ref",
                table: "ledger_entries",
                columns: new[] { "shop_id", "upi_ref" },
                unique: true,
                filter: "upi_ref IS NOT NULL");
        }
    }
}
