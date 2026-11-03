using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BridalBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bridal_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    at_home = table.Column<bool>(type: "boolean", nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    travel_charge = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    trial_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bridal_bookings", x => x.id);
                    table.UniqueConstraint("AK_bridal_bookings_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "bridal_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    stylist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bridal_events", x => x.id);
                    table.UniqueConstraint("AK_bridal_events_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_bridal_events_bridal_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bridal_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bridal_bookings_shop_id_number",
                table: "bridal_bookings",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bridal_events_booking_id",
                table: "bridal_events",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_bridal_events_shop_id_date_stylist_id",
                table: "bridal_events",
                columns: new[] { "shop_id", "date", "stylist_id" });

            foreach (var table in new[] { "bridal_bookings", "bridal_events" })
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
                name: "bridal_events");

            migrationBuilder.DropTable(
                name: "bridal_bookings");
        }
    }
}
