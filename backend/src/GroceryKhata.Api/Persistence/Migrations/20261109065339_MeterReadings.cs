using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeterReadings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "meter_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    reading = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meter_readings", x => x.id);
                    table.UniqueConstraint("AK_meter_readings_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_meter_readings_shop_id_day",
                table: "meter_readings",
                columns: new[] { "shop_id", "day" });

            migrationBuilder.EnableShopIsolation("meter_readings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "meter_readings");
        }
    }
}
