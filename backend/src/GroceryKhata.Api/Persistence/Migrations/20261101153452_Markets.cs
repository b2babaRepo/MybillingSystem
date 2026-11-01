using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Markets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "shops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "shops",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "market_id",
                table: "shops",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "markets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    city = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shop_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_photos", x => x.id);
                    table.ForeignKey(
                        name: "FK_shop_photos_shops_shop_id",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shops_market_id",
                table: "shops",
                column: "market_id");

            migrationBuilder.CreateIndex(
                name: "IX_markets_name",
                table: "markets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shop_photos_shop_id",
                table: "shop_photos",
                column: "shop_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_shops_markets_market_id",
                table: "shops",
                column: "market_id",
                principalTable: "markets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        
    // The markets typed so far: exact matches only, admins merge the rest from the panel.
    migrationBuilder.Sql("""
        INSERT INTO markets (id, name, is_active, created_at, updated_at)
        SELECT gen_random_uuid(), min(trim(v)), true, now(), now()
        FROM (SELECT area AS v FROM shops UNION ALL SELECT market FROM shop_registrations) x
        WHERE length(trim(v)) >= 2
        GROUP BY lower(trim(v));
        UPDATE shops s SET market_id = m.id FROM markets m WHERE lower(trim(s.area)) = lower(m.name);
        UPDATE shops s SET address = coalesce(s.address, left(r.address, 200)), latitude = r.latitude, longitude = r.longitude
        FROM shop_registrations r WHERE r.shop_id = s.id;
        INSERT INTO shop_photos (id, shop_id, content_type, data, created_at, updated_at)
        SELECT gen_random_uuid(), r.shop_id, p.content_type, p.data, now(), now()
        FROM registration_photos p JOIN shop_registrations r ON r.id = p.registration_id
        WHERE p.kind = 'front' AND r.shop_id IS NOT NULL;
        """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shops_markets_market_id",
                table: "shops");

            migrationBuilder.DropTable(
                name: "markets");

            migrationBuilder.DropTable(
                name: "shop_photos");

            migrationBuilder.DropIndex(
                name: "IX_shops_market_id",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "shops");

            migrationBuilder.DropColumn(
                name: "market_id",
                table: "shops");
        }
    }
}
