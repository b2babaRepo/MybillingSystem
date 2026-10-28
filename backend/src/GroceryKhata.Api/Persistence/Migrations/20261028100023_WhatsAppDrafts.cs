using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WhatsAppDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "ai_proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "ai_proposals",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "App");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "ai_proposals");

            migrationBuilder.DropColumn(
                name: "source",
                table: "ai_proposals");
        }
    }
}
