using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "custom_role_id",
                table: "platform_admins",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "platform_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_admins_custom_role_id",
                table: "platform_admins",
                column: "custom_role_id");

            migrationBuilder.CreateIndex(
                name: "IX_platform_roles_name",
                table: "platform_roles",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_platform_admins_platform_roles_custom_role_id",
                table: "platform_admins",
                column: "custom_role_id",
                principalTable: "platform_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_platform_admins_platform_roles_custom_role_id",
                table: "platform_admins");

            migrationBuilder.DropTable(
                name: "platform_roles");

            migrationBuilder.DropIndex(
                name: "IX_platform_admins_custom_role_id",
                table: "platform_admins");

            migrationBuilder.DropColumn(
                name: "custom_role_id",
                table: "platform_admins");
        }
    }
}
