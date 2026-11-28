using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_pictures",
                columns: table => new
                {
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_pictures", x => new { x.shop_id, x.key });
                });

            // Each shop sees only its own pictures (row-level security, like every shop table).
            migrationBuilder.Sql("""
                ALTER TABLE shop_pictures ENABLE ROW LEVEL SECURITY;
                ALTER TABLE shop_pictures FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON shop_pictures
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_pictures");
        }
    }
}
