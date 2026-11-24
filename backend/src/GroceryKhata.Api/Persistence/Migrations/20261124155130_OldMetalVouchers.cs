using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OldMetalVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "voucher_id",
                table: "old_metal_purchases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "old_metal_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    seller_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    seller_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    seller_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pan = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    total = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    expense_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_old_metal_vouchers", x => x.id);
                    table.UniqueConstraint("AK_old_metal_vouchers_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_old_metal_purchases_shop_id_voucher_id",
                table: "old_metal_purchases",
                columns: new[] { "shop_id", "voucher_id" });

            migrationBuilder.CreateIndex(
                name: "IX_old_metal_vouchers_shop_id_number",
                table: "old_metal_vouchers",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE old_metal_vouchers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE old_metal_vouchers FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON old_metal_vouchers
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "old_metal_vouchers");

            migrationBuilder.DropIndex(
                name: "IX_old_metal_purchases_shop_id_voucher_id",
                table: "old_metal_purchases");

            migrationBuilder.DropColumn(
                name: "voucher_id",
                table: "old_metal_purchases");
        }
    }
}
