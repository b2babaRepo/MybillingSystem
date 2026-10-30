using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "approval_above",
                table: "teams",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_limit",
                table: "team_members",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "approved_at",
                table: "team_expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by_user_id",
                table: "team_expenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "pending_since",
                table: "team_expenses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "photo",
                table: "team_expenses",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_type",
                table: "team_expenses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approval_above",
                table: "teams");

            migrationBuilder.DropColumn(
                name: "monthly_limit",
                table: "team_members");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "team_expenses");

            migrationBuilder.DropColumn(
                name: "approved_by_user_id",
                table: "team_expenses");

            migrationBuilder.DropColumn(
                name: "pending_since",
                table: "team_expenses");

            migrationBuilder.DropColumn(
                name: "photo",
                table: "team_expenses");

            migrationBuilder.DropColumn(
                name: "photo_type",
                table: "team_expenses");
        }
    }
}
