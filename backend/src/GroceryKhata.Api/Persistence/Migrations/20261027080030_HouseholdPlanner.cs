using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HouseholdPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "shops",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "shops",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    list_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fulfilment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    slot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    payment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    final_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    packed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.UniqueConstraint("AK_orders_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_orders_bills_shop_id_bill_id",
                        columns: x => new { x.shop_id, x.bill_id },
                        principalTable: "bills",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_orders_users_placed_by_user_id",
                        column: x => x.placed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shopping_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_lists", x => x.id);
                    table.ForeignKey(
                        name: "FK_shopping_lists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    packed_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    removed = table.Column<bool>(type: "boolean", nullable: false),
                    shop_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_order_lines_orders_shop_id_order_id",
                        columns: x => new { x.shop_id, x.order_id },
                        principalTable: "orders",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shopping_list_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    @checked = table.Column<bool>(name: "checked", type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_list_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_shopping_list_items_shopping_lists_list_id",
                        column: x => x.list_id,
                        principalTable: "shopping_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_shop_id_order_id_line_no",
                table: "order_lines",
                columns: new[] { "shop_id", "order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_placed_by_user_id_created_at",
                table: "orders",
                columns: new[] { "placed_by_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_shop_id_bill_id",
                table: "orders",
                columns: new[] { "shop_id", "bill_id" });

            migrationBuilder.CreateIndex(
                name: "IX_orders_shop_id_status_created_at",
                table: "orders",
                columns: new[] { "shop_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_list_id",
                table: "shopping_list_items",
                column: "list_id");

            migrationBuilder.CreateIndex(
                name: "IX_shopping_lists_user_id_kind",
                table: "shopping_lists",
                columns: new[] { "user_id", "kind" },
                unique: true);

            // Orders are the shop's rows, but the household that placed one may read it too. Writes still need the
            // shop: a household places an order by working as that shop for the insert (PlannerEndpoints).
            foreach (var table in new[] { "orders", "order_lines" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY shop_or_placed_by ON {table}
                        USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid
                               OR placed_by_user_id = NULLIF(current_setting('app.user_id', true), '')::uuid)
                        WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines");

            migrationBuilder.DropTable(
                name: "shopping_list_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "address",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "area",
                table: "shops");
        }
    }
}
