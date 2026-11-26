using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "buy_qty",
                table: "sale_offers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "combo_price",
                table: "sale_offers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "days",
                table: "sale_offers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "from_minute",
                table: "sale_offers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "get_qty",
                table: "sale_offers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_bill",
                table: "sale_offers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "min_qty",
                table: "sale_offers",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "to_minute",
                table: "sale_offers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "bill_offer_id",
                table: "bills",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "buy_qty",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "combo_price",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "days",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "from_minute",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "get_qty",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "min_bill",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "min_qty",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "to_minute",
                table: "sale_offers");

            migrationBuilder.DropColumn(
                name: "bill_offer_id",
                table: "bills");
        }
    }
}
