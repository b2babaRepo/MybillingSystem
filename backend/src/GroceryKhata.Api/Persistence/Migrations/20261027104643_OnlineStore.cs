using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OnlineStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "reserved",
                table: "products",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_user_id",
                table: "orders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "orders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "List");

            migrationBuilder.AddColumn<string>(
                name: "status_token_hash",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_user_id",
                table: "order_lines",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "order_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reserved_quantity",
                table: "order_lines",
                type: "numeric(12,3)",
                precision: 12,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_shop_id_product_id",
                table: "order_lines",
                columns: new[] { "shop_id", "product_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_order_lines_products_shop_id_product_id",
                table: "order_lines",
                columns: new[] { "shop_id", "product_id" },
                principalTable: "products",
                principalColumns: new[] { "shop_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_order_lines_products_shop_id_product_id",
                table: "order_lines");

            migrationBuilder.DropIndex(
                name: "IX_order_lines_shop_id_product_id",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "reserved",
                table: "products");

            migrationBuilder.DropColumn(
                name: "address",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "source",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status_token_hash",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "reserved_quantity",
                table: "order_lines");

            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_user_id",
                table: "orders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "placed_by_user_id",
                table: "order_lines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
