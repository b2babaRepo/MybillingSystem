using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseBatchesAndReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "batch_id",
                table: "purchase_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_percent",
                table: "purchase_lines",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "free_quantity",
                table: "purchase_lines",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "supplier_returns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_note_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    credit_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_returns", x => x.id);
                    table.UniqueConstraint("AK_supplier_returns_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "supplier_return_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_return_lines", x => x.id);
                    table.UniqueConstraint("AK_supplier_return_lines_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_supplier_return_lines_supplier_returns_return_id",
                        column: x => x.return_id,
                        principalTable: "supplier_returns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_return_lines_return_id",
                table: "supplier_return_lines",
                column: "return_id");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_returns_shop_id_number",
                table: "supplier_returns",
                columns: new[] { "shop_id", "number" },
                unique: true);

            foreach (var table in new[] { "supplier_returns", "supplier_return_lines" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY shop_isolation ON {table}
                        USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                        WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_return_lines");

            migrationBuilder.DropTable(
                name: "supplier_returns");

            migrationBuilder.DropColumn(
                name: "batch_id",
                table: "purchase_lines");

            migrationBuilder.DropColumn(
                name: "discount_percent",
                table: "purchase_lines");

            migrationBuilder.DropColumn(
                name: "free_quantity",
                table: "purchase_lines");
        }
    }
}
