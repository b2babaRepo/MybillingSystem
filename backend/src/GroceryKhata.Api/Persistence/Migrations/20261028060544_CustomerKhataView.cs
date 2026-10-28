using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerKhataView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "khata_token",
                table: "customers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_phone",
                table: "customers",
                column: "phone");

            // A household may read its own customer rows in every shop: the ones with the signed-in person's phone.
            // Read-only (FOR SELECT); writes still need the shop. Policies add up, so shop_isolation is unchanged.
            migrationBuilder.Sql("""
                CREATE POLICY own_khata_read ON customers FOR SELECT
                    USING (phone IS NOT NULL AND phone = (
                        SELECT u.phone FROM users u WHERE u.id = NULLIF(current_setting('app.user_id', true), '')::uuid));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS own_khata_read ON customers;");
            migrationBuilder.DropIndex(
                name: "IX_customers_phone",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "khata_token",
                table: "customers");
        }
    }
}
