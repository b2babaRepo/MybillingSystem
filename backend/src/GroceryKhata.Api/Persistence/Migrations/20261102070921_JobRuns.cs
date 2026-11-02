using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    job = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    last_ran_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_runs", x => x.job);
                });

            migrationBuilder.CreateTable(
                name: "seen_messages",
                columns: table => new
                {
                    message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seen_messages", x => x.message_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "seen_messages");
        }
    }
}
