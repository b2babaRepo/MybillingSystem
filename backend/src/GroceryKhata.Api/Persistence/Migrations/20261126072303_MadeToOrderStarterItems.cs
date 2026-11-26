using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MadeToOrderStarterItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The restaurant, lodge and banquet starter lists made dishes and services counted stock: every sale took them below
            // zero and onto "order today". Mark those starter items not counted, in those shops, where no stock was ever
            // brought in for them (an item the shop does keep in stock is left as it is). The table owner skips row-level
            // security on both tables while this runs (NO FORCE): every shop is done, and the stock check sees every shop's
            // movements (under FORCE it would see none and mark items the shop does stock).
            migrationBuilder.Sql("""
                ALTER TABLE products NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE stock_movements NO FORCE ROW LEVEL SECURITY;
                UPDATE products p SET uncounted = true
                FROM shops s
                WHERE s.id = p.shop_id AND s.type IN ('Restaurant', 'Lodge', 'Banquet') AND NOT p.is_service AND NOT p.uncounted
                  AND p.name IN (
                    'Airport / station pickup',
                    'Balloon decoration',
                    'Birthday cake (per kg)',
                    'Breakfast',
                    'Butter naan',
                    'DJ and sound',
                    'Dal makhani',
                    'Decoration package',
                    'Early check-in',
                    'Extra bed (per night)',
                    'Extra plate',
                    'Generator (per hour)',
                    'Gulab jamun (2 pc)',
                    'Jeera rice',
                    'Late check-out',
                    'Laundry (per piece)',
                    'Lunch / dinner thali',
                    'Masala chai',
                    'Masala dosa',
                    'Paneer butter masala',
                    'Party decoration',
                    'Room for baraat (per night)',
                    'Stage and flower decoration',
                    'Tandoori roti',
                    'Tea / coffee',
                    'Valet parking',
                    'Veg biryani',
                    'Veg thali')
                  AND NOT EXISTS (SELECT 1 FROM stock_movements m
                                  WHERE m.product_id = p.id AND m.kind IN ('Opening', 'Purchase') AND m.quantity > 0);
                ALTER TABLE stock_movements FORCE ROW LEVEL SECURITY;
                ALTER TABLE products FORCE ROW LEVEL SECURITY;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which items this marked isn't recorded, and counting them again would only bring the problem back.
        }
    }
}
