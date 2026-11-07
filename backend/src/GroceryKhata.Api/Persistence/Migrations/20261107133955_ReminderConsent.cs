using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReminderConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reminder_consent_at",
                table: "customers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    given = table.Column<bool>(type: "boolean", nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    wording = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_consents", x => x.id);
                    table.UniqueConstraint("AK_customer_consents_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_customer_consents_customers_shop_id_customer_id",
                        columns: x => new { x.shop_id, x.customer_id },
                        principalTable: "customers",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reminder_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder_logs", x => x.id);
                    table.UniqueConstraint("AK_reminder_logs_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_reminder_logs_customers_shop_id_customer_id",
                        columns: x => new { x.shop_id, x.customer_id },
                        principalTable: "customers",
                        principalColumns: new[] { "shop_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_consents_shop_id_customer_id_recorded_at",
                table: "customer_consents",
                columns: new[] { "shop_id", "customer_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "IX_reminder_logs_shop_id_customer_id_sent_at",
                table: "reminder_logs",
                columns: new[] { "shop_id", "customer_id", "sent_at" });

            migrationBuilder.Sql("""
                ALTER TABLE customer_consents ENABLE ROW LEVEL SECURITY;
                ALTER TABLE customer_consents FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON customer_consents
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);

                ALTER TABLE reminder_logs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE reminder_logs FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON reminder_logs
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_consents");

            migrationBuilder.DropTable(
                name: "reminder_logs");

            migrationBuilder.DropColumn(
                name: "reminder_consent_at",
                table: "customers");
        }
    }
}
