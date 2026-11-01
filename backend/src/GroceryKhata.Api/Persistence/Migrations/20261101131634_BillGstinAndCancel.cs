using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillGstinAndCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancel_reason",
                table: "bills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "bills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                table: "bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gstin",
                table: "bills",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);
        
    // Old bills take the shop's current GSTIN, the best record there is. Row-level security would hide every row here.
    migrationBuilder.Sql("""
        ALTER TABLE bills NO FORCE ROW LEVEL SECURITY;
        UPDATE bills b SET gstin = s.gstin FROM shops s WHERE s.id = b.shop_id;
        ALTER TABLE bills FORCE ROW LEVEL SECURITY;
        """);

    // GST billing is a paid feature: Gold opens it to begin with, and admins can add it to other plans.
    migrationBuilder.Sql("UPDATE plans SET feature_keys = array_append(feature_keys, 'gst') WHERE code = 'pro' AND NOT 'gst' = ANY(feature_keys);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancel_reason",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "gstin",
                table: "bills");
        }
    }
}
