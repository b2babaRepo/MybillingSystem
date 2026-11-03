using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TableBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "table_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    people = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    occasion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cake = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    decoration = table.Column<bool>(type: "boolean", nullable: false),
                    table = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deposit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_table_bookings", x => x.id);
                    table.UniqueConstraint("AK_table_bookings_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_table_bookings_shop_id_date",
                table: "table_bookings",
                columns: new[] { "shop_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_table_bookings_shop_id_number",
                table: "table_bookings",
                columns: new[] { "shop_id", "number" },
                unique: true);
            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE table_bookings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE table_bookings FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON table_bookings
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "table_bookings");
        }
    }
}
