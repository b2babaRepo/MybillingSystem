using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DisputeItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_bill_lines_shop_id_id",
                table: "bill_lines",
                columns: new[] { "shop_id", "id" });

            migrationBuilder.CreateTable(
                name: "dispute_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    outcome_note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    return_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_items", x => x.id);
                    table.UniqueConstraint("AK_dispute_items_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_dispute_items_bill_lines_shop_id_bill_line_id",
                        columns: x => new { x.shop_id, x.bill_line_id },
                        principalTable: "bill_lines",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_dispute_items_disputes_dispute_id",
                        column: x => x.dispute_id,
                        principalTable: "disputes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispute_items_dispute_id_bill_line_id",
                table: "dispute_items",
                columns: new[] { "dispute_id", "bill_line_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dispute_items_shop_id_bill_line_id",
                table: "dispute_items",
                columns: new[] { "shop_id", "bill_line_id" });

            // Shop-owned like the ledger: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE dispute_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE dispute_items FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON dispute_items
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispute_items");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_bill_lines_shop_id_id",
                table: "bill_lines");
        }
    }
}
