using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DairyGridAndChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dairy_grid_cells",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fat = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    snf = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    rate_per_litre = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dairy_grid_cells", x => x.id);
                    table.UniqueConstraint("AK_dairy_grid_cells_shop_id_id", x => new { x.shop_id, x.id });
                    table.ForeignKey(
                        name: "FK_dairy_grid_cells_dairy_rate_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "dairy_rate_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dairy_grid_cells_plan_id",
                table: "dairy_grid_cells",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_dairy_grid_cells_shop_id_plan_id_kind_fat_snf",
                table: "dairy_grid_cells",
                columns: new[] { "shop_id", "plan_id", "kind", "fat", "snf" },
                unique: true);

            migrationBuilder.Sql("""
                ALTER TABLE dairy_grid_cells ENABLE ROW LEVEL SECURITY;
                ALTER TABLE dairy_grid_cells FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON dairy_grid_cells
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dairy_grid_cells");
        }
    }
}
