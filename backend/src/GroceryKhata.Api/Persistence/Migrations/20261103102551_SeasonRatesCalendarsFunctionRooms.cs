using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeasonRatesCalendarsFunctionRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "booking_id",
                table: "stays",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "room_charge",
                table: "stays",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ical_import_url",
                table: "rooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ical_token",
                table: "rooms",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rate_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    from = table.Column<DateOnly>(type: "date", nullable: false),
                    to = table.Column<DateOnly>(type: "date", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rate_periods", x => x.id);
                    table.UniqueConstraint("AK_rate_periods_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_stays_booking_id",
                table: "stays",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_rooms_ical_token",
                table: "rooms",
                column: "ical_token",
                unique: true);
            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE rate_periods ENABLE ROW LEVEL SECURITY;
                ALTER TABLE rate_periods FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON rate_periods
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rate_periods");

            migrationBuilder.DropIndex(
                name: "IX_stays_booking_id",
                table: "stays");

            migrationBuilder.DropIndex(
                name: "IX_rooms_ical_token",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "booking_id",
                table: "stays");

            migrationBuilder.DropColumn(
                name: "room_charge",
                table: "stays");

            migrationBuilder.DropColumn(
                name: "ical_import_url",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "ical_token",
                table: "rooms");
        }
    }
}
