using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_owners",
                table: "plans",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // New limits for the plans as first set up; a plan an admin already changed keeps its numbers.
            migrationBuilder.Sql("""
                UPDATE plans SET max_staff = 1, max_owners = 1, feature_keys = array_append(feature_keys, 'staff')
                    WHERE code = 'basic' AND max_staff = 0 AND NOT ('staff' = ANY(feature_keys));
                UPDATE plans SET max_staff = 3, max_owners = 2 WHERE code = 'silver' AND max_staff = 2;
                UPDATE plans SET max_staff = 10, max_owners = 3 WHERE code = 'pro' AND max_staff = 5;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_owners",
                table: "plans");
        }
    }
}
