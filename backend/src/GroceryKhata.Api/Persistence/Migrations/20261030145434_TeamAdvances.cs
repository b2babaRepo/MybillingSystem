using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeamAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "from_advance",
                table: "team_expenses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "team_advances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    struck_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_advances", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_advances_team_members_member_id",
                        column: x => x.member_id,
                        principalTable: "team_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_advances_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_team_advances_member_id",
                table: "team_advances",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_advances_team_id_member_id",
                table: "team_advances",
                columns: new[] { "team_id", "member_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "team_advances");

            migrationBuilder.DropColumn(
                name: "from_advance",
                table: "team_expenses");
        }
    }
}
