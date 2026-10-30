using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Accountability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "financial_year",
                table: "bills",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "number",
                table: "bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "platform_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    detail = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_activities", x => x.id);
                });

            // Number the bills already saved: per shop and financial year (April to March, India time), oldest first.
            // Then each shop's counter carries on from its last number. The table owner skips row-level security while
            // this runs (NO FORCE), so every shop's bills are numbered.
            migrationBuilder.Sql("""
                CREATE TABLE bill_counters (
                    shop_id uuid NOT NULL REFERENCES shops (id) ON DELETE CASCADE,
                    financial_year varchar(7) NOT NULL,
                    last_number integer NOT NULL,
                    PRIMARY KEY (shop_id, financial_year));

                ALTER TABLE bills NO FORCE ROW LEVEL SECURITY;
                UPDATE bills b SET financial_year = x.fy, number = x.n
                FROM (SELECT id,
                             fy,
                             row_number() OVER (PARTITION BY shop_id, fy ORDER BY created_at, id) AS n
                      FROM (SELECT id, shop_id, created_at,
                                   y::text || '-' || lpad(((y + 1) % 100)::text, 2, '0') AS fy
                            FROM (SELECT id, shop_id, created_at,
                                         extract(year FROM created_at AT TIME ZONE 'Asia/Kolkata')::int
                                           - CASE WHEN extract(month FROM created_at AT TIME ZONE 'Asia/Kolkata') >= 4 THEN 0 ELSE 1 END AS y
                                  FROM bills) a) c) x
                WHERE b.id = x.id;
                INSERT INTO bill_counters (shop_id, financial_year, last_number)
                SELECT shop_id, financial_year, max(number) FROM bills GROUP BY shop_id, financial_year;
                ALTER TABLE bills FORCE ROW LEVEL SECURITY;

                ALTER TABLE bill_counters ENABLE ROW LEVEL SECURITY;
                ALTER TABLE bill_counters FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON bill_counters
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_bills_shop_id_financial_year_number",
                table: "bills",
                columns: new[] { "shop_id", "financial_year", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_activities_admin_user_id_created_at",
                table: "platform_activities",
                columns: new[] { "admin_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_platform_activities_created_at",
                table: "platform_activities",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_platform_activities_shop_id_created_at",
                table: "platform_activities",
                columns: new[] { "shop_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_activities");

            migrationBuilder.DropIndex(
                name: "IX_bills_shop_id_financial_year_number",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "financial_year",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "number",
                table: "bills");
        }
    }
}
