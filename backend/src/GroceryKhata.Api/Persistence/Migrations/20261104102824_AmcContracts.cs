using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AmcContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "amc_id",
                table: "jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "amcs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    serial_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    start_on = table.Column<DateOnly>(type: "date", nullable: false),
                    months = table.Column<int>(type: "integer", nullable: false),
                    visits = table.Column<int>(type: "integer", nullable: false),
                    fee = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    renewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_amcs", x => x.id);
                    table.UniqueConstraint("AK_amcs_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_amcs_shop_id_number",
                table: "amcs",
                columns: new[] { "shop_id", "number" },
                unique: true);
        
            migrationBuilder.EnableShopIsolation("amcs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "amcs");

            migrationBuilder.DropColumn(
                name: "amc_id",
                table: "jobs");
        }
    }
}
