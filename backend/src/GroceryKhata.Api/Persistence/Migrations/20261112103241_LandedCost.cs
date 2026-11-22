using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LandedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cost_split",
                table: "purchases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "extra_in_cost",
                table: "purchases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "landed_unit_cost",
                table: "purchase_lines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "purchase_charges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_charges", x => x.id);
                    table.UniqueConstraint("AK_purchase_charges_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_purchase_charges_purchases_purchase_id",
                        column: x => x.purchase_id,
                        principalTable: "purchases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_charges_purchase_id",
                table: "purchase_charges",
                column: "purchase_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_charges_shop_id_purchase_id",
                table: "purchase_charges",
                columns: new[] { "shop_id", "purchase_id" });

            migrationBuilder.EnableShopIsolation("purchase_charges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_charges");

            migrationBuilder.DropColumn(
                name: "cost_split",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "extra_in_cost",
                table: "purchases");

            migrationBuilder.DropColumn(
                name: "landed_unit_cost",
                table: "purchase_lines");
        }
    }
}
