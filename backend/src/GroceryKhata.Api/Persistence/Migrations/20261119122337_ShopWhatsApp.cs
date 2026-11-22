using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShopWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shop_whatsapp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waba_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    phone_number_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    display_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    verified_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    access_token = table.Column<string>(type: "text", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    test_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_whatsapp", x => x.id);
                    table.UniqueConstraint("AK_shop_whatsapp_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_numbers",
                columns: table => new
                {
                    phone_number_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_numbers", x => x.phone_number_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shop_whatsapp_shop_id",
                table: "shop_whatsapp",
                column: "shop_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_numbers_shop_id",
                table: "whatsapp_numbers",
                column: "shop_id");

            migrationBuilder.Sql("""
                ALTER TABLE shop_whatsapp ENABLE ROW LEVEL SECURITY;
                ALTER TABLE shop_whatsapp FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON shop_whatsapp
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shop_whatsapp");

            migrationBuilder.DropTable(
                name: "whatsapp_numbers");
        }
    }
}
