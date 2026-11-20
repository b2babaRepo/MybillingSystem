using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JewelleryWorkshop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tray",
                table: "jewellery_pieces",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "metal_moves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    metal = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    gross = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reading = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fine = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    finished = table.Column<bool>(type: "boolean", nullable: false),
                    allowance = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    labour = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    rate_per_gram = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    cash_effect = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    balance_effect = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    loose_effect = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    due_back = table.Column<DateOnly>(type: "date", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metal_moves", x => x.id);
                    table.UniqueConstraint("AK_metal_moves_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "metal_parties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    allowance_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    allowance_on_fine = table.Column<bool>(type: "boolean", nullable: false),
                    labour_per_gram = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    gst_registered = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metal_parties", x => x.id);
                    table.UniqueConstraint("AK_metal_parties_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "piece_stones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    piece_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    lab_grown = table.Column<bool>(type: "boolean", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    carat = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    shape = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    colour = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    clarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cert_lab = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cert_no = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    rate_per_carat = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_piece_stones", x => x.id);
                    table.UniqueConstraint("AK_piece_stones_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "tray_counts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tray = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    expected_pieces = table.Column<int>(type: "integer", nullable: false),
                    expected_gross = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    counted_pieces = table.Column<int>(type: "integer", nullable: false),
                    counted_gross = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true),
                    missing = table.Column<string>(type: "text", nullable: true),
                    extra = table.Column<string>(type: "text", nullable: true),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tray_counts", x => x.id);
                    table.UniqueConstraint("AK_tray_counts_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "weighing_scales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reads_to = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    stamped_until = table.Column<DateOnly>(type: "date", nullable: true),
                    baud = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weighing_scales", x => x.id);
                    table.UniqueConstraint("AK_weighing_scales_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_metal_moves_shop_id_at",
                table: "metal_moves",
                columns: new[] { "shop_id", "at" });

            migrationBuilder.CreateIndex(
                name: "IX_metal_moves_shop_id_number",
                table: "metal_moves",
                columns: new[] { "shop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_metal_moves_shop_id_party_id",
                table: "metal_moves",
                columns: new[] { "shop_id", "party_id" });

            migrationBuilder.CreateIndex(
                name: "IX_piece_stones_shop_id_piece_id",
                table: "piece_stones",
                columns: new[] { "shop_id", "piece_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tray_counts_shop_id_tray_created_at",
                table: "tray_counts",
                columns: new[] { "shop_id", "tray", "created_at" });

            migrationBuilder.Sql("""
                ALTER TABLE metal_parties ENABLE ROW LEVEL SECURITY;
                ALTER TABLE metal_parties FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON metal_parties
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE metal_moves ENABLE ROW LEVEL SECURITY;
                ALTER TABLE metal_moves FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON metal_moves
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE piece_stones ENABLE ROW LEVEL SECURITY;
                ALTER TABLE piece_stones FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON piece_stones
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE tray_counts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tray_counts FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON tray_counts
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE weighing_scales ENABLE ROW LEVEL SECURITY;
                ALTER TABLE weighing_scales FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON weighing_scales
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "metal_moves");

            migrationBuilder.DropTable(
                name: "metal_parties");

            migrationBuilder.DropTable(
                name: "piece_stones");

            migrationBuilder.DropTable(
                name: "tray_counts");

            migrationBuilder.DropTable(
                name: "weighing_scales");

            migrationBuilder.DropColumn(
                name: "tray",
                table: "jewellery_pieces");
        }
    }
}
