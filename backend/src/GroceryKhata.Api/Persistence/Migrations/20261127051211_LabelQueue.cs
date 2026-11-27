using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LabelQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "label_printed_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "price_changed_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            // A price or MRP changed by any route (the rates screen, an import, an edit) puts the item in the label queue.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION products_price_changed() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND (NEW.selling_price IS DISTINCT FROM OLD.selling_price OR NEW.mrp IS DISTINCT FROM OLD.mrp) THEN
                        NEW.price_changed_at := clock_timestamp();
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER products_price_changed BEFORE UPDATE ON products FOR EACH ROW EXECUTE FUNCTION products_price_changed();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS products_price_changed ON products; DROP FUNCTION IF EXISTS products_price_changed();");

            migrationBuilder.DropColumn(
                name: "label_printed_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "price_changed_at",
                table: "products");
        }
    }
}
