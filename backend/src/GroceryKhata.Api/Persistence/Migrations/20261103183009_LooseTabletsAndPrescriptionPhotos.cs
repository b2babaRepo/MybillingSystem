using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LooseTabletsAndPrescriptionPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "units_per_pack",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prescription_photos",
                columns: table => new
                {
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescription_photos", x => x.bill_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prescription_photos_shop_id",
                table: "prescription_photos",
                column: "shop_id");

            migrationBuilder.Sql("""
                ALTER TABLE prescription_photos ENABLE ROW LEVEL SECURITY;
                ALTER TABLE prescription_photos FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON prescription_photos
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prescription_photos");

            migrationBuilder.DropColumn(
                name: "units_per_pack",
                table: "products");
        }
    }
}
