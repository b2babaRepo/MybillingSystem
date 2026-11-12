using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Godowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "godowns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_godowns", x => x.id);
                    table.UniqueConstraint("AK_godowns_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "stock_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    from_godown_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_godown_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfers", x => x.id);
                    table.UniqueConstraint("AK_stock_transfers_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "stock_transfer_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    bags = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_transfer_lines", x => x.id);
                    table.UniqueConstraint("AK_stock_transfer_lines_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_stock_transfer_lines_stock_transfers_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "stock_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_shop_id_product_id",
                table: "stock_transfer_lines",
                columns: new[] { "shop_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfer_lines_transfer_id",
                table: "stock_transfer_lines",
                column: "transfer_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_transfers_shop_id_number",
                table: "stock_transfers",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.EnableShopIsolation("godowns");
            migrationBuilder.EnableShopIsolation("stock_transfers");
            migrationBuilder.EnableShopIsolation("stock_transfer_lines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "godowns");

            migrationBuilder.DropTable(
                name: "stock_transfer_lines");

            migrationBuilder.DropTable(
                name: "stock_transfers");
        }
    }
}
