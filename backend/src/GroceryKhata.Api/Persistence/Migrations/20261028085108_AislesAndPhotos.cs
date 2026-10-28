using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AislesAndPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "image_at",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aisle",
                table: "catalogue_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.product_id);
                    table.ForeignKey(
                        name: "FK_product_images_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 1,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 2,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 3,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 4,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 5,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 6,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 7,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 8,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 9,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 10,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 11,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 12,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 13,
                column: "aisle",
                value: "dal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 14,
                column: "aisle",
                value: "atta-rice");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 15,
                column: "aisle",
                value: "sugar-salt");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 16,
                column: "aisle",
                value: "sugar-salt");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 17,
                column: "aisle",
                value: "sugar-salt");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 18,
                column: "aisle",
                value: "oil-ghee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 19,
                column: "aisle",
                value: "oil-ghee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 20,
                column: "aisle",
                value: "oil-ghee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 21,
                column: "aisle",
                value: "oil-ghee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 22,
                column: "aisle",
                value: "oil-ghee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 23,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 24,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 25,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 26,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 27,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 28,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 29,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 30,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 31,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 32,
                column: "aisle",
                value: "masala");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 33,
                column: "aisle",
                value: "tea-coffee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 34,
                column: "aisle",
                value: "tea-coffee");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 35,
                column: "aisle",
                value: "dairy");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 36,
                column: "aisle",
                value: "dairy");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 37,
                column: "aisle",
                value: "dairy");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 38,
                column: "aisle",
                value: "dairy");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 39,
                column: "aisle",
                value: "breakfast");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 40,
                column: "aisle",
                value: "snacks");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 41,
                column: "aisle",
                value: "snacks");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 42,
                column: "aisle",
                value: "instant");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 43,
                column: "aisle",
                value: "dairy");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 44,
                column: "aisle",
                value: "personal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 45,
                column: "aisle",
                value: "cleaning");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 46,
                column: "aisle",
                value: "cleaning");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 47,
                column: "aisle",
                value: "cleaning");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 48,
                column: "aisle",
                value: "cleaning");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 49,
                column: "aisle",
                value: "personal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 50,
                column: "aisle",
                value: "personal");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 51,
                column: "aisle",
                value: "household");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 52,
                column: "aisle",
                value: "pooja");

            migrationBuilder.UpdateData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 53,
                column: "aisle",
                value: "household");

            migrationBuilder.InsertData(
                table: "catalogue_items",
                columns: new[] { "id", "aisle", "category", "name", "name_en", "typical_price", "unit" },
                values: new object[,]
                {
                    { 54, "fruits-veg", "फल, सब्ज़ी", "प्याज", "Onion", 40m, "Kg" },
                    { 55, "fruits-veg", "फल, सब्ज़ी", "आलू", "Potato", 30m, "Kg" },
                    { 56, "fruits-veg", "फल, सब्ज़ी", "टमाटर", "Tomato", 40m, "Kg" },
                    { 57, "fruits-veg", "फल, सब्ज़ी", "केला", "Banana", 6m, "Piece" },
                    { 58, "fruits-veg", "फल, सब्ज़ी", "नींबू", "Lemon", 5m, "Piece" },
                    { 59, "drinks", "पेय", "कोल्ड ड्रिंक", "Cold drink", 40m, "Piece" },
                    { 60, "drinks", "पेय", "जूस", "Juice", 110m, "Litre" },
                    { 61, "drinks", "पेय", "पानी की बोतल", "Water bottle", 20m, "Piece" },
                    { 62, "breakfast", "बिस्कुट, नमकीन", "कॉर्नफ़्लेक्स", "Cornflakes", 180m, "Packet" },
                    { 63, "breakfast", "बिस्कुट, नमकीन", "जैम", "Jam", 140m, "Packet" },
                    { 64, "instant", "बिस्कुट, नमकीन", "सूप", "Soup", 60m, "Packet" },
                    { 65, "personal", "पर्सनल केयर", "बालों का तेल", "Hair oil", 90m, "Piece" },
                    { 66, "personal", "पर्सनल केयर", "टूथब्रश", "Toothbrush", 30m, "Piece" },
                    { 67, "personal", "पर्सनल केयर", "फ़ेस वॉश", "Face wash", 150m, "Piece" },
                    { 68, "pooja", "पूजा का सामान", "कपूर", "Camphor", 30m, "Packet" },
                    { 69, "pooja", "पूजा का सामान", "दीया बाती", "Diya wicks", 20m, "Packet" },
                    { 70, "pooja", "पूजा का सामान", "धूपबत्ती", "Dhoop sticks", 40m, "Packet" },
                    { 71, "household", "घर का सामान", "बल्ब", "Bulb", 90m, "Piece" },
                    { 72, "household", "घर का सामान", "झाड़ू", "Broom", 120m, "Piece" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_images_shop_id",
                table: "product_images",
                column: "shop_id");

            // Shop-owned like products: row-level security as the second guard (see the Initial migration).
            migrationBuilder.Sql("""
                ALTER TABLE product_images ENABLE ROW LEVEL SECURITY;
                ALTER TABLE product_images FORCE ROW LEVEL SECURITY;
                CREATE POLICY shop_isolation ON product_images
                    USING (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)
                    WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "catalogue_items",
                keyColumn: "id",
                keyValue: 72);

            migrationBuilder.DropColumn(
                name: "image_at",
                table: "products");

            migrationBuilder.DropColumn(
                name: "aisle",
                table: "catalogue_items");
        }
    }
}
