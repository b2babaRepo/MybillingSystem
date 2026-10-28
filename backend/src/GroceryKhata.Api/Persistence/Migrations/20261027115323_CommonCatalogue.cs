using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GroceryKhata.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommonCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "catalogue_item_id",
                table: "shopping_list_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "catalogue_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    name_en = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    typical_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogue_items", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "catalogue_items",
                columns: new[] { "id", "category", "name", "name_en", "typical_price", "unit" },
                values: new object[,]
                {
                    { 1, "आटा, चावल, दाल", "आटा", "Atta", 45m, "Kg" },
                    { 2, "आटा, चावल, दाल", "चावल", "Rice", 50m, "Kg" },
                    { 3, "आटा, चावल, दाल", "बासमती चावल", "Basmati rice", 120m, "Kg" },
                    { 4, "आटा, चावल, दाल", "मैदा", "Maida", 40m, "Kg" },
                    { 5, "आटा, चावल, दाल", "सूजी", "Suji", 50m, "Kg" },
                    { 6, "आटा, चावल, दाल", "बेसन", "Besan", 95m, "Kg" },
                    { 7, "आटा, चावल, दाल", "तूर दाल", "Toor dal", 160m, "Kg" },
                    { 8, "आटा, चावल, दाल", "मूंग दाल", "Moong dal", 130m, "Kg" },
                    { 9, "आटा, चावल, दाल", "चना दाल", "Chana dal", 95m, "Kg" },
                    { 10, "आटा, चावल, दाल", "उड़द दाल", "Urad dal", 140m, "Kg" },
                    { 11, "आटा, चावल, दाल", "मसूर दाल", "Masoor dal", 110m, "Kg" },
                    { 12, "आटा, चावल, दाल", "राजमा", "Rajma", 150m, "Kg" },
                    { 13, "आटा, चावल, दाल", "काबुली चना", "Kabuli chana", 140m, "Kg" },
                    { 14, "आटा, चावल, दाल", "पोहा", "Poha", 60m, "Kg" },
                    { 15, "चीनी, नमक, गुड़", "चीनी", "Sugar", 46m, "Kg" },
                    { 16, "चीनी, नमक, गुड़", "गुड़", "Jaggery", 70m, "Kg" },
                    { 17, "चीनी, नमक, गुड़", "नमक", "Salt", 28m, "Packet" },
                    { 18, "तेल, घी", "सरसों तेल", "Mustard oil", 175m, "Litre" },
                    { 19, "तेल, घी", "रिफ़ाइंड तेल", "Refined oil", 150m, "Litre" },
                    { 20, "तेल, घी", "मूंगफली तेल", "Groundnut oil", 210m, "Litre" },
                    { 21, "तेल, घी", "देसी घी", "Desi ghee", 620m, "Litre" },
                    { 22, "तेल, घी", "वनस्पति", "Vanaspati", 140m, "Kg" },
                    { 23, "मसाले", "हल्दी पाउडर", "Turmeric powder", 35m, "Packet" },
                    { 24, "मसाले", "लाल मिर्च पाउडर", "Red chilli powder", 45m, "Packet" },
                    { 25, "मसाले", "धनिया पाउडर", "Coriander powder", 35m, "Packet" },
                    { 26, "मसाले", "जीरा", "Jeera", 0.6m, "G" },
                    { 27, "मसाले", "राई", "Mustard seeds", 0.2m, "G" },
                    { 28, "मसाले", "गरम मसाला", "Garam masala", 60m, "Packet" },
                    { 29, "मसाले", "हींग", "Hing", 50m, "Packet" },
                    { 30, "मसाले", "काली मिर्च", "Black pepper", 1m, "G" },
                    { 31, "मसाले", "इलायची", "Elaichi", 3.5m, "G" },
                    { 32, "मसाले", "मेथी दाना", "Methi seeds", 0.2m, "G" },
                    { 33, "चाय, कॉफ़ी, दूध", "चायपत्ती", "Tea", 140m, "Packet" },
                    { 34, "चाय, कॉफ़ी, दूध", "कॉफ़ी", "Coffee", 180m, "Packet" },
                    { 35, "चाय, कॉफ़ी, दूध", "दूध", "Milk", 66m, "Litre" },
                    { 36, "चाय, कॉफ़ी, दूध", "दही", "Curd", 80m, "Kg" },
                    { 37, "चाय, कॉफ़ी, दूध", "पनीर", "Paneer", 0.45m, "G" },
                    { 38, "चाय, कॉफ़ी, दूध", "मक्खन", "Butter", 60m, "Packet" },
                    { 39, "बिस्कुट, नमकीन", "ब्रेड", "Bread", 45m, "Packet" },
                    { 40, "बिस्कुट, नमकीन", "बिस्कुट", "Biscuits", 10m, "Packet" },
                    { 41, "बिस्कुट, नमकीन", "नमकीन", "Namkeen", 50m, "Packet" },
                    { 42, "बिस्कुट, नमकीन", "मैगी", "Maggi", 14m, "Packet" },
                    { 43, "बिस्कुट, नमकीन", "अंडे", "Eggs", 7m, "Piece" },
                    { 44, "साफ़-सफ़ाई", "नहाने का साबुन", "Bath soap", 40m, "Piece" },
                    { 45, "साफ़-सफ़ाई", "कपड़े धोने का साबुन", "Washing soap", 30m, "Piece" },
                    { 46, "साफ़-सफ़ाई", "सर्फ़ / डिटर्जेंट", "Detergent", 120m, "Kg" },
                    { 47, "साफ़-सफ़ाई", "बर्तन धोने का साबुन", "Dishwash bar", 20m, "Piece" },
                    { 48, "साफ़-सफ़ाई", "फ़िनाइल", "Phenyl", 90m, "Litre" },
                    { 49, "साफ़-सफ़ाई", "टूथपेस्ट", "Toothpaste", 95m, "Piece" },
                    { 50, "साफ़-सफ़ाई", "शैम्पू", "Shampoo", 2m, "Packet" },
                    { 51, "घर का सामान", "माचिस", "Matchbox", 10m, "Packet" },
                    { 52, "घर का सामान", "अगरबत्ती", "Agarbatti", 50m, "Packet" },
                    { 53, "घर का सामान", "मोमबत्ती", "Candles", 40m, "Packet" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_shopping_list_items_catalogue_item_id",
                table: "shopping_list_items",
                column: "catalogue_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_shopping_list_items_catalogue_items_catalogue_item_id",
                table: "shopping_list_items",
                column: "catalogue_item_id",
                principalTable: "catalogue_items",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shopping_list_items_catalogue_items_catalogue_item_id",
                table: "shopping_list_items");

            migrationBuilder.DropTable(
                name: "catalogue_items");

            migrationBuilder.DropIndex(
                name: "IX_shopping_list_items_catalogue_item_id",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "catalogue_item_id",
                table: "shopping_list_items");
        }
    }
}
