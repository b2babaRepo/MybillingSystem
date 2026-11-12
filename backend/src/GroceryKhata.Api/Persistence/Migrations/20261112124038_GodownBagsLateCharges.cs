using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GodownBagsLateCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "from_goods",
                table: "bag_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ref_id",
                table: "bag_moves",
                type: "uuid",
                nullable: true);

            // Bills saved before landed cost: their landed cost is what they were costed at, the rate before GST.
            migrationBuilder.Sql("""
                ALTER TABLE purchase_lines NO FORCE ROW LEVEL SECURITY;
                UPDATE purchase_lines SET landed_unit_cost = round(taxable / (quantity + free_quantity), 2)
                WHERE landed_unit_cost IS NULL AND quantity + free_quantity > 0;
                ALTER TABLE purchase_lines FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "from_goods",
                table: "bag_types");

            migrationBuilder.DropColumn(
                name: "ref_id",
                table: "bag_moves");
        }
    }
}
