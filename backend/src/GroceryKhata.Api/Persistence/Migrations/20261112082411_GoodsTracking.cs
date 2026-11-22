using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoodsTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "confirmed_at",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "promise_moves",
                table: "purchase_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sent_at",
                table: "purchase_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sent_via",
                table: "purchase_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "snoozed_until",
                table: "purchase_orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stage",
                table: "purchase_orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "placed");

            migrationBuilder.CreateTable(
                name: "order_follow_ups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    new_promised_date = table.Column<DateOnly>(type: "date", nullable: true),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_follow_ups", x => x.id);
                    table.UniqueConstraint("AK_order_follow_ups_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "order_shipments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatched_on = table.Column<DateOnly>(type: "date", nullable: true),
                    expected_on = table.Column<DateOnly>(type: "date", nullable: true),
                    transporter = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    lr_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    vehicle_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    eway_bill_no = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    eway_valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    freight = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    freight_paid_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_shipments", x => x.id);
                    table.UniqueConstraint("AK_order_shipments_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_follow_ups_shop_id_order_id_at",
                table: "order_follow_ups",
                columns: new[] { "shop_id", "order_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_order_shipments_shop_id_order_id",
                table: "order_shipments",
                columns: new[] { "shop_id", "order_id" });
            migrationBuilder.EnableShopIsolation("order_shipments", "order_follow_ups");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_follow_ups");

            migrationBuilder.DropTable(
                name: "order_shipments");

            migrationBuilder.DropColumn(
                name: "confirmed_at",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "promise_moves",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "sent_at",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "sent_via",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "snoozed_until",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "stage",
                table: "purchase_orders");
        }
    }
}
