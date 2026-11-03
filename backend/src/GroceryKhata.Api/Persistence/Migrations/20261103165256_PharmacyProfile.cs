using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PharmacyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pharmacy_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    licence_form20 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    licence_form21 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    licence_issued_on = table.Column<DateOnly>(type: "date", nullable: true),
                    pharmacist_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    pharmacist_reg_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    fssai_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_profiles", x => x.id);
                    table.UniqueConstraint("AK_pharmacy_profiles_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_profiles_shop_id",
                table: "pharmacy_profiles",
                column: "shop_id",
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE pharmacy_profiles ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pharmacy_profiles FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON pharmacy_profiles
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pharmacy_profiles");
        }
    }
}
