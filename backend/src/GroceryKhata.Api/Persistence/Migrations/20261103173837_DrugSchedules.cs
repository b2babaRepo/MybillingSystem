using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DrugSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "schedule",
                table: "products",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bill_prescriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    doctor_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    doctor_reg_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    patient_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    patient_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    prescribed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_prescriptions", x => x.id);
                    table.UniqueConstraint("AK_bill_prescriptions_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_prescriptions_shop_id_bill_id",
                table: "bill_prescriptions",
                columns: new[] { "shop_id", "bill_id" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE bill_prescriptions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE bill_prescriptions FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON bill_prescriptions
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_prescriptions");

            migrationBuilder.DropColumn(
                name: "schedule",
                table: "products");
        }
    }
}
