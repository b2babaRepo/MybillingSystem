using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Categories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    normalised_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    merged_into_id = table.Column<Guid>(type: "uuid", nullable: true),
                    default_gst_rate = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    default_hsn = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    default_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    target_cover_days = table.Column<int>(type: "integer", nullable: true),
                    slow_after_days = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("AK_categories_shop_id_id", x => new { x.shop_id, x.id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_shop_id_normalised_name",
                table: "categories",
                columns: new[] { "shop_id", "normalised_name" },
                unique: true,
                filter: "merged_into_id IS NULL");

            // One category per name the shop already typed, joining names that differ only by capitals or spaces (the
            // most used spelling wins), then every item points at its category and takes that spelling.
            migrationBuilder.Sql("""
                ALTER TABLE products NO FORCE ROW LEVEL SECURITY;

                INSERT INTO categories (id, shop_id, name, normalised_name, sort_order, is_active, created_at, updated_at)
                SELECT gen_random_uuid(), shop_id, left(name, 60), left(norm, 60), (row_number() OVER (PARTITION BY shop_id ORDER BY norm) - 1)::int, true, now(), now()
                FROM (
                    SELECT shop_id, lower(regexp_replace(trim(category), '[[:space:]]+', ' ', 'g')) AS norm,
                           mode() WITHIN GROUP (ORDER BY regexp_replace(trim(category), '[[:space:]]+', ' ', 'g')) AS name
                    FROM products
                    WHERE category IS NOT NULL AND trim(category) <> ''
                    GROUP BY shop_id, lower(regexp_replace(trim(category), '[[:space:]]+', ' ', 'g'))
                ) g;

                UPDATE products p SET category_id = c.id, category = c.name
                FROM categories c
                WHERE c.shop_id = p.shop_id AND c.normalised_name = left(lower(regexp_replace(trim(p.category), '[[:space:]]+', ' ', 'g')), 60);

                ALTER TABLE products FORCE ROW LEVEL SECURITY;
                """);
            migrationBuilder.EnableShopIsolation("categories");
            migrationBuilder.CreateIndex(name: "IX_products_shop_id_category_id", table: "products", columns: new[] { "shop_id", "category_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_products_shop_id_category_id", table: "products");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "products");
        }
    }
}
