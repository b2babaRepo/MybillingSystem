using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "added_by_user_id",
                table: "platform_admins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "platform_admins",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Super");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "added_by_user_id",
                table: "platform_admins");

            migrationBuilder.DropColumn(
                name: "role",
                table: "platform_admins");
        }
    }
}
