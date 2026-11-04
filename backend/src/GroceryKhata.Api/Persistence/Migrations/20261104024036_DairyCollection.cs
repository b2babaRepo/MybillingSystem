using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DairyCollection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dairy_farmer_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_farmer_entries", x => x.id);
                    table.UniqueConstraint("AK_dairy_farmer_entries_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "dairy_farmers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    village = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    default_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_farmers", x => x.id);
                    table.UniqueConstraint("AK_dairy_farmers_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "dairy_rate_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @for = table.Column<string>(name: "for", type: "character varying(20)", maxLength: 20, nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_rate_plans", x => x.id);
                    table.UniqueConstraint("AK_dairy_rate_plans_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "milk_collections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    farmer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    litres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    fat = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    snf = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    clr = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    below_standard = table.Column<bool>(type: "boolean", nullable: false),
                    rejected = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edit_log = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milk_collections", x => x.id);
                    table.UniqueConstraint("AK_milk_collections_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "milk_dispatches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agency = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    litres = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    fat = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    snf = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    accepted_litres = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    accepted_fat = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    accepted_snf = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    paid_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_milk_dispatches", x => x.id);
                    table.UniqueConstraint("AK_milk_dispatches_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "dairy_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fat_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    snf_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    flat_rate = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    min_fat = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    min_snf = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_rates", x => x.id);
                    table.UniqueConstraint("AK_dairy_rates_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_dairy_rates_dairy_rate_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "dairy_rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dairy_farmer_entries_shop_id_farmer_id_date",
                table: "dairy_farmer_entries",
                columns: new[] { "shop_id", "farmer_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_dairy_farmers_shop_id_code",
                table: "dairy_farmers",
                columns: new[] { "shop_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dairy_rate_plans_shop_id_for_from",
                table: "dairy_rate_plans",
                columns: new[] { "shop_id", "for", "from" });

            migrationBuilder.CreateIndex(
                name: "IX_dairy_rates_plan_id",
                table: "dairy_rates",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_milk_collections_shop_id_date_shift",
                table: "milk_collections",
                columns: new[] { "shop_id", "date", "shift" });

            migrationBuilder.CreateIndex(
                name: "IX_milk_collections_shop_id_farmer_id_date",
                table: "milk_collections",
                columns: new[] { "shop_id", "farmer_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_milk_dispatches_shop_id_date",
                table: "milk_dispatches",
                columns: new[] { "shop_id", "date" });

            foreach (var table in new[] { "dairy_farmers", "dairy_rate_plans", "dairy_rates", "milk_collections", "dairy_farmer_entries", "milk_dispatches" })
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
                name: "dairy_farmer_entries");

            migrationBuilder.DropTable(
                name: "dairy_farmers");

            migrationBuilder.DropTable(
                name: "dairy_rates");

            migrationBuilder.DropTable(
                name: "milk_collections");

            migrationBuilder.DropTable(
                name: "milk_dispatches");

            migrationBuilder.DropTable(
                name: "dairy_rate_plans");
        }
    }
}
