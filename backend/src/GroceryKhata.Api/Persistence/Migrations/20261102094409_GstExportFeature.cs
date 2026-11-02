using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GstExportFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tally files for the CA are a paid feature: Gold has it to begin with; admins can add it to other plans.
            migrationBuilder.Sql("UPDATE plans SET feature_keys = array_append(feature_keys, 'gst_export') WHERE code = 'pro' AND NOT 'gst_export' = ANY(feature_keys);");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
