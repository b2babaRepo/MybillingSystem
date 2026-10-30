using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FieldAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Agent codes: GKA-0001, GKA-0002… in approval order, never reused.
            migrationBuilder.Sql("CREATE SEQUENCE field_agent_code_seq START 1;");

            migrationBuilder.CreateTable(
                name: "field_agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    market = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    aadhaar_last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    aadhaar_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kyc_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    kyc_checked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    upi_id = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agreement_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    added_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_agents", x => x.id);
                    table.ForeignKey(
                        name: "FK_field_agents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_aadhaar_key",
                table: "field_agents",
                column: "aadhaar_key");

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_code",
                table: "field_agents",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_status_created_at",
                table: "field_agents",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_field_agents_user_id",
                table: "field_agents",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS field_agent_code_seq;");

            migrationBuilder.DropTable(
                name: "field_agents");
        }
    }
}
