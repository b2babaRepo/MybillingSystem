using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_shop_id_created_at",
                table: "audit_log",
                columns: new[] { "shop_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_shop_id_record_id_created_at",
                table: "audit_log",
                columns: new[] { "shop_id", "record_id", "created_at" });

            // Shop-owned: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE audit_log ENABLE ROW LEVEL SECURITY;
                ALTER TABLE audit_log FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON audit_log
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");
        }
    }
}
