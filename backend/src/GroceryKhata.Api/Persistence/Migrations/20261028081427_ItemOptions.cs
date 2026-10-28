using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ItemOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_default_option",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "pack_size",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pack_unit",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "approval",
                table: "order_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<decimal>(
                name: "expected_amount",
                table: "order_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preference",
                table: "order_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "ShopChooses");

            migrationBuilder.AddColumn<string>(
                name: "requested_name",
                table: "order_lines",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "requested_product_id",
                table: "order_lines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_default_option",
                table: "products");

            migrationBuilder.DropColumn(
                name: "pack_size",
                table: "products");

            migrationBuilder.DropColumn(
                name: "pack_unit",
                table: "products");

            migrationBuilder.DropColumn(
                name: "approval",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "expected_amount",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "preference",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "requested_name",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "requested_product_id",
                table: "order_lines");
        }
    }
}
