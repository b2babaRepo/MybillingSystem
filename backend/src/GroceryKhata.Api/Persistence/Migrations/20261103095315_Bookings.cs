using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Bookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    hold_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occasion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    guests = table.Column<int>(type: "integer", nullable: false),
                    plate_rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    hall_rent = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_taken = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    deposit_returned = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    actual_guests = table.Column<int>(type: "integer", nullable: true),
                    job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                    table.UniqueConstraint("AK_bookings_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "venues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venues", x => x.id);
                    table.UniqueConstraint("AK_venues_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_shop_id_number",
                table: "bookings",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_shop_id_venue_id_date",
                table: "bookings",
                columns: new[] { "shop_id", "venue_id", "date" });
            // Shop-owned: row-level security as the second guard (see the Initial migration).
            foreach (var table in new[] { "venues", "bookings" })
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
                name: "bookings");

            migrationBuilder.DropTable(
                name: "venues");
        }
    }
}
