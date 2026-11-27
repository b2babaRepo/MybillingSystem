using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GiftVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gift_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sold_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sold_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    buyer_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    sold_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_vouchers", x => x.id);
                    table.UniqueConstraint("AK_gift_vouchers_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_gift_vouchers_shop_id_code",
                table: "gift_vouchers",
                columns: new[] { "shop_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_vouchers_shop_id_sold_at",
                table: "gift_vouchers",
                columns: new[] { "shop_id", "sold_at" });

            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE gift_vouchers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE gift_vouchers FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON gift_vouchers
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gift_vouchers");
        }
    }
}
