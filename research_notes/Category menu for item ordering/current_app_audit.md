# Current app audit: where items are picked, and how categories work today

Scope: source code in /home/user/MybillingSystem (frontend/src, backend/src/GroceryKhata.Api) and /reports. No files changed except this note. Paths are relative to the repo root; line numbers are from the files as read on 2026-11-27. "Source" links are file paths, not URLs.

Target layout being compared against ("All categories"): left rail of top categories with icons; right side grouped sub-category tiles with images; then a product list with search and filters.

## 1. Which screens pick items while ordering or billing, and what navigation, search and filters each has

### Takeaway
Seven places pick items. Only three browse by category at all: the online store (chips on phones, a text-only left rail on desktop for kirana), the household list (a two-level section and aisle tile grid), and the master-list picker (chips plus a mobile tile strip plus a desktop text rail). The shop's bill screen, supplier orders, goods-in extras and order packing have no category navigation. Search-only pickers are the norm for shop-side ordering. No screen has the full rail, sub-category tiles and product list layout. The store page is the closest.

### Cited Findings

**A. Customer online store: `frontend/src/screens/store/StoreApp.tsx` (757 lines)**
- State: `category` ('all' default), `search`, and `filters = { open, sort: 'relevance'|'low'|'high', inStock, max }` — [StoreApp.tsx:214-218](frontend/src/screens/store/StoreApp.tsx)
- Category key per item: the common-catalogue aisle if the item matched one (`aisleOf(i.catalogueItemId)`). Otherwise the shop's own category string as `cat:<name>`, otherwise `other` — [StoreApp.tsx:300-305](frontend/src/screens/store/StoreApp.tsx)
- Chips: "All" plus the aisles the shop stocks (localised with `aisleName`), then the shop's own categories sorted A–Z, then "Other", each with a count. Comment: "every item shows at once as a photo card; chips narrow it, search finds by name" — [StoreApp.tsx:306-312](frontend/src/screens/store/StoreApp.tsx)
- Search: forgiving ranked search (`rank` from lib/search.ts, "doodh finds milk, a letter wrong still matches") over `name + category`. Recent searches are remembered on blur — [StoreApp.tsx:313-315, 385](frontend/src/screens/store/StoreApp.tsx)
- Filtering and sort: by category chip, in-stock-only, max price. In-stock items always come first, then price low/high. "Relevance" keeps the search rank — [StoreApp.tsx:316-319](frontend/src/screens/store/StoreApp.tsx)
- The Filters button and drawer (sort chips, max ₹, in-stock checkbox, "Show N items") are **hidden for kirana shops** (`{!kirana && …}`) and shown for other trades — [StoreApp.tsx:387, 472-485](frontend/src/screens/store/StoreApp.tsx)
- Kirana on wide screens (lg): a sticky **left rail** `nav` with the same chips as text buttons and counts, in a `lg:grid-cols-[180px_1fr]` layout. Comment: "Kirana: a left rail of aisles on wide screens (many aisles, few items each); chips on phones." The rail has **no icons or images**, only a name and a count — [StoreApp.tsx:410-421](frontend/src/screens/store/StoreApp.tsx)
- Front page (All, no search): horizontal rails "Your usual" (from past orders), "Offers" and "New arrivals" (added within 21 days) — [StoreApp.tsx:320-327, 398-400](frontend/src/screens/store/StoreApp.tsx)
- Product list: photo-card grid with `ItemCard square`. It is 2 columns on phones, 3 at sm, 4 at md, and 4 (kirana) or 5 at lg. Cards show the price, MRP/offer strike-through, a heart, and Add/−/+ — [StoreApp.tsx:422-462](frontend/src/screens/store/StoreApp.tsx)
- No-match state offers "Ask the shop on WhatsApp" — [StoreApp.tsx:402-407](frontend/src/screens/store/StoreApp.tsx)
- There are no sub-category tiles and no aisle tiles in the store. `AisleTiles` is not imported there; only `aisleOf` is — [StoreApp.tsx:16-19](frontend/src/screens/store/StoreApp.tsx)

**B. Household list / planner: `frontend/src/screens/planner/ListScreen.tsx` (266 lines)**
- Header comment: "Pick items from the common catalogue (search or browse by category) or type any item" — [ListScreen.tsx:20-23](frontend/src/screens/planner/ListScreen.tsx)
- Browse: `AisleTiles` (section headers, each with a grid of tinted tiles carrying aisle art). Tapping a tile replaces the grid with a "‹ Aisle name" back button and a 2-column `ItemCard` grid of that aisle's catalogue items. Already-added items show "✓ on list" — [ListScreen.tsx:175-202](frontend/src/screens/planner/ListScreen.tsx)
- Once a shop is chosen, the shelf shows only items that shop sells, with in-stock items first. Aisle tiles are limited to aisles present (`available`) — [ListScreen.tsx:33-46, 185](frontend/src/screens/planner/ListScreen.tsx)
- Search: a type-ahead name box with suggestions from `searchCatalogue(shelf, name)` showing the Hindi name, English name and ~typical price. Free text can be added as a custom item — [ListScreen.tsx:46, 150-158](frontend/src/screens/planner/ListScreen.tsx)
- Browsing is hidden while the user types (`!name.trim()`). There are no filters or sorts — [ListScreen.tsx:172-176](frontend/src/screens/planner/ListScreen.tsx)
- Only the common catalogue (about 70 generic items, see §2) is browsable here, not the shop's 900-plus branded items.
- `AisleTiles` component: "bold section headers… each with a grid of soft-tinted rounded tiles holding the aisle's picture and a short name below, like the shopping apps' category grids". Aisles not in `available` are hidden — [components/AisleTiles.tsx:15-19](frontend/src/components/AisleTiles.tsx)
- Other planner screens. `SendScreen.tsx` searches **shops**, not items, and has delivery/pickup chips — [SendScreen.tsx:20-23, 165-166](frontend/src/screens/planner/SendScreen.tsx). `AiOrderScreen.tsx` reads free text or voice into lines and has no browse — [AiOrderScreen.tsx:159-181](frontend/src/screens/planner/AiOrderScreen.tsx). `MyOrdersScreen.tsx` has a "↻ reorder" button that repeats a past order — [MyOrdersScreen.tsx:292, 373](frontend/src/screens/planner/MyOrdersScreen.tsx)
- Cross-shop item search: `SearchScreen` in `CustomerScreens.tsx` ranks items across saved shops. It shows "Recent" chips and "Trending" chips, which are the first 8 distinct **category names** of in-stock items, used as tap-to-search terms. It also has voice input — [CustomerScreens.tsx:378-418](frontend/src/screens/planner/CustomerScreens.tsx)

**C. Shop bill screen: `frontend/src/screens/BillScreen.tsx` (1543 lines)**
- Tiles: the shop's products, sorted most-sold first (`usePopularOrder`) and then by name. Size/colour variants are grouped into one style tile. Only **12 tiles on a phone and 30 on wide screens or while searching** — [BillScreen.tsx:62-68](frontend/src/screens/BillScreen.tsx)
- Search: a substring or `nameMatches` (cross-script, "aata" finds "आटा") filter on name and style name. `2*aata` sets the quantity. Enter adds the top match. Barcode scans go to the same box. F2 focuses it and Esc clears it — [BillScreen.tsx:58-65, 700-721, 629](frontend/src/screens/BillScreen.tsx)
- Grid: `grid-cols-3 lg:4 xl:5 2xl:6` compact `ItemCard`s with a photo, price, offer badge and a cart-count badge — [BillScreen.tsx:764-797](frontend/src/screens/BillScreen.tsx)
- **No category chips, tabs, rail or filters at all.** A grep for "categor" in BillScreen.tsx finds nothing. With 900-plus kirana items, an item outside the top 12/30 can only be reached by search or barcode.

**D. Master-list picker (adding items to the shop): `frontend/src/screens/PickItemsScreen.tsx` (312 lines)**
- Data: `/v1/master-items` (the shop's trades). Trade chips appear when the shop has several trades — [PickItemsScreen.tsx:47-54, 200](frontend/src/screens/PickItemsScreen.tsx)
- Categories: the master item's `category` strings in list order. Chips are "★ Popular N", "All" and every category name, as raw strings with no translation — [PickItemsScreen.tsx:78, 204-208](frontend/src/screens/PickItemsScreen.tsx)
- Mobile: a horizontal strip of 68 px category **tiles**. Each tile uses the photo of the first item in the category that has one, otherwise that item's emoji on a rotating tint. It shows only when no category or search is set — [PickItemsScreen.tsx:38-39, 85-86, 209-223](frontend/src/screens/PickItemsScreen.tsx)
- Desktop (lg): a sticky **left rail** `aside` (220 px) with Popular, All, and each category with a count. It is text only — [PickItemsScreen.tsx:166-171](frontend/src/screens/PickItemsScreen.tsx)
- List: sections per category with sticky headers, "Add group" per section, and rows with thumbnail, Hindi/English name, "New" badge, "In shop" and pack-size chips. The list is 2 columns at lg and 3 at xl — [PickItemsScreen.tsx:229-262](frontend/src/screens/PickItemsScreen.tsx)
- Search: loose Hinglish matching. Each typed word must start a word of the English or Hindi name (aa→a, sh→s, and so on). Search does not cover category — [PickItemsScreen.tsx:22-36, 79-84](frontend/src/screens/PickItemsScreen.tsx)
- "Choose all" appears only once the list is narrowed to ≤50 items. Users can "Suggest a missing item" with a free-typed category — [PickItemsScreen.tsx:16-17, 225-228, 275-277](frontend/src/screens/PickItemsScreen.tsx)
- Photo priority: the shop's own photo, then the master photo `/api/v1/master-images/{id}?size=thumb`, then the emoji — [PickItemsScreen.tsx:68-74](frontend/src/screens/PickItemsScreen.tsx)

**E. Items screen (shop catalogue): `frontend/src/screens/ItemsScreen.tsx` (521 lines)**
- "The catalogue with what's on the shelf: search, category chips, low-stock filter." — [ItemsScreen.tsx:26](frontend/src/screens/ItemsScreen.tsx)
- Category chips come from `orderedNames(useCategories)`, which follows the shop's own category order. Tapping a chip toggles it — [ItemsScreen.tsx:47-52, 141-145](frontend/src/screens/ItemsScreen.tsx)
- Filter cards: All / Low / Out of stock. There is also a "Suggested" chip (popular master items not yet in the shop) — [ItemsScreen.tsx:54-68, 106-114, 142](frontend/src/screens/ItemsScreen.tsx)
- Search is a name match ("Search N items") with a clear button — [ItemsScreen.tsx:131-137](frontend/src/screens/ItemsScreen.tsx)
- This screen manages stock and does not order, but it is the only shop-side screen that uses the `categories` table order.

**F. Supplier orders / reorder: `frontend/src/screens/SupplierOrders.tsx` (404 lines) and `SuppliersScreen.tsx`**
- Order lines are "Rows of item + quantity (+ rate), found by typing. Only an exact name picks an item." Each row uses `ItemPicker` — [SupplierOrders.tsx:29-38](frontend/src/screens/SupplierOrders.tsx)
- `ItemPicker` is a type-ahead with up to 8 name matches and no categories, photos or filters. When empty it lists the first 8 products — [components/ItemPicker.tsx:9-34](frontend/src/components/ItemPicker.tsx)
- SuppliersScreen links to `/reorder` and also uses `ItemPicker` for rows — [SuppliersScreen.tsx:127, 620](frontend/src/screens/SuppliersScreen.tsx)
- Goods-in groups the tick-list **by category** (`l.category ?? noCategory`) with per-group progress. Extras are added with `ItemPicker` — [GoodsInScreen.tsx:176-198, 234](frontend/src/screens/GoodsInScreen.tsx)

**G. Order packing: `frontend/src/screens/OrderScreen.tsx` (270 lines)**
- Packs a household's list. For each line the shop chooses among its options for the same `catalogueItemId`, with AI-confidence chips. There is no browse or search for new items — [OrderScreen.tsx:25-30, 55, 14](frontend/src/screens/OrderScreen.tsx)
- `SaleOrdersScreen.tsx` is a sortable, filterable table of booked orders (by party), not an item picker — [SaleOrdersScreen.tsx:75-83](frontend/src/screens/SaleOrdersScreen.tsx)

### Inferences
- The only full browse experiences are the household list (generic items only) and the online store (no tiles, and no filters for kirana). The shop-side ordering screens (bill, supplier order, goods-in extras) depend entirely on search, so a category menu would add the most there and in the store.
- Three different category vocabularies drive these screens: aisles (store, list), master `category` strings (picker) and the shop's `categories` table (items screen, goods-in). A shared "All categories" component would first need one source of truth.
- Hiding the store's filter drawer for kirana looks deliberate (kirana gets the rail instead). The target layout would bring filters back for kirana.

### Gaps
- I did not run the app, so I have no screenshots and no measured item counts per shop. Behaviour is read from code.
- I did not open the `/reorder` route's screen (lib/reorder and the component behind `/reorder`), `TradeBill.tsx`, `EstimatesScreen.tsx` or `HoldsScreen.tsx` in detail. A grep of TradeBill and Estimates for "categor|search" found nothing relevant.

## 2. How categories are modelled (backend and data)

### Takeaway
There are three separate structures. (1) A per-shop `Category` table with an optional one-level `ParentId` and a `SortOrder`. It has no icon, image or translations, and no picking screen uses the parent. (2) A fixed, global two-level tree in `Aisles` (4 sections → 16 aisles, Hindi and English) that covers only the roughly 70-item common catalogue. (3) Free-text `MasterItem.Category` strings per trade, one level only, which for kirana are in Hindi. The kirana master list has 18 categories that do not line up one-to-one with the 16 aisles.

### Cited Findings
- `Category` entity: `ShopId`, `Name`, `NormalisedName`, `ParentId` ("The group above it, at most one level ('Grocery' › 'Pulses')"), `SortOrder`, `IsActive`, `MergedIntoId`, default GST/HSN/unit, `TargetCoverDays` and `SlowAfterDays`. **There is no icon, image, colour or localised name field** — [backend/.../Catalogue/Categories.cs:15-45](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- Typing a new name on an item creates the category, placed last in the order. Matching ignores case and spaces. Merges are followed — [Categories.cs:79-111](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- `GET /api/v1/categories` returns categories ordered by `SortOrder`, then name, with active item counts. Owners can create, update, merge and reorder (`POST /order`) — [Categories.cs:63-71, 134-141](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- `Product.Category` holds a copy of the name "so screens can group by it", and `Product.CategoryId` is the link. `Product.ImageAt` records when the shop's photo was added — [Catalogue/Product.cs:11-15, 50-51](backend/src/GroceryKhata.Api/Catalogue/Product.cs)
- Frontend `CategoryRow` includes `parentId` and `sortOrder`. `orderedNames()` gives the chip order: the shop's order first, then unknown names A–Z. It **ignores parentId** — [frontend/src/lib/categories.ts:5-28](frontend/src/lib/categories.ts)
- `parentId` is used only in `CategoriesScreen.tsx`, to choose a parent and show "Parent › Child". The screen's job is to "Rename…, merge…, hide, put a group under another, set the order of the chips, and set defaults" — [CategoriesScreen.tsx:22-23, 69, 91, 103](frontend/src/screens/CategoriesScreen.tsx). A repo-wide grep for `parentId` finds no other use.
- Common catalogue and Aisles: "The category tree every shop and household browses: four sections, each a row of aisles. Two levels only; brand and pack size are options on an item, not more levels. The order here is the order on screen." — [CommonCatalogue.cs:31-35](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
  - Section "रोज़ का राशन / Daily staples": atta-rice, dal, oil-ghee, masala, sugar-salt, fruits-veg
  - Section "दूध और नाश्ता / Dairy & breakfast": dairy, tea-coffee, breakfast
  - Section "स्नैक्स और पेय / Snacks & drinks": snacks, instant, drinks
  - Section "घर, सफ़ाई और पूजा / Home, cleaning & pooja": personal, cleaning, pooja, household — [CommonCatalogue.cs:38-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
  - Served publicly at `GET /api/v1/catalogue/aisles` ("the online store's guests browse by aisle too") — [CatalogueEndpoints.cs:24-25](backend/src/GroceryKhata.Api/Catalogue/CatalogueEndpoints.cs)
  - About 70 `CatalogueItem`s (generic: आटा/Atta, तूर दाल/Toor dal…). Each has its own coarser `Category` string and an `Aisle` assigned by id range in `AisleOf` — [CommonCatalogue.cs:93-157](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
  - A shop product joins an aisle only through `CatalogueItemId`, which is set by name match (exact Hindi/English, or name + " " / "(" brand suffix) — [CommonCatalogue.cs:67-90](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
- `MasterItem`: `Trade`, `Category` (free string), `Name`, `NameHi`, `Unit`, `GstRate`, `HsnCode`, `CatalogueItemId?`, `PackSizes`, `ImageAt` and `ImageCredit`. It has **no sub-category, sort order or category id** — [MasterItems.cs:14-54](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Master lists are seeded from embedded CSVs (`name,name_hi,category,unit,gst,hsn,packs`), and the category is capped at 60 characters. The server orders by Trade, then Category, then Name, which is **alphabetical, not a curated order** — [MasterItems.cs:174-258, 286-287](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- The medical list's categories "were once finer (143 for medical, now about 24 groups)". `StarterGroups` maps the old names to the new ones — [MasterItems.cs:244-272](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- The kirana master list, `backend/src/GroceryKhata.Api/Catalogue/Data/kirana-master.csv`, has 958 items in **18 categories, all named in Hindi only** (item counts in brackets): आटा, चावल, दाल (99); तेल, घी (39); चीनी, नमक, गुड़ (20); मसाले (100); सूखे मेवे (33); अचार, सॉस, पापड़ (43); इंस्टेंट, नाश्ता (54); बिस्कुट, नमकीन (76); डेयरी, ब्रेड (41); चाय, कॉफ़ी, दूध (49); बेबी केयर (12); चॉकलेट, टॉफ़ी (33); पेय (39); दांतों की देखभाल (43); पर्सनल केयर (120); साफ़-सफ़ाई (89); घर का सामान (37); पूजा का सामान (31) — counted from the CSV with Python.
- Other trades' category names, in English, with item counts:
  - **medical** (847 items, 24 categories): Fever & pain; Antibiotics & infections; Stomach & digestion; Cold, cough & allergy; Breathing & respiratory; Diabetes; Heart & BP; Thyroid, hormones & steroids; Vitamins & supplements; Skin care & creams; First aid & wound care; Eye, ear & mouth; Women's health; Kidney & urology; Sexual wellness & condoms; Brain & nerves; Surgical & nursing care; Diagnostic devices; Ayurvedic & Unani; Baby & mother care; Nutrition & health drinks; Personal care & hygiene; Supports & mobility; Hot, cold & water therapy.
  - **beauty** (952 items, 12 categories): Bath, body and personal care; Facial, bleach and clean-up; Fragrance and talc; Hair care (163); Hair colour and henna; Makeup (173); Men's grooming; Nails and mani-pedi; Other; Salon consumables, bridal and tools; Salon professional hair; Skin care (156).
  - **hardware** (319 items, 14 categories): Cement; Chain, rope and welding; Door and furniture fittings; Doors and windows; Farm tools; Fasteners; Hand tools; Locks; Other; Pipes and fittings; Power tools and accessories; Roofing sheets; Sariya and steel; Wire and mesh.
  - **paint** (337 items, 13 categories): Adhesive and sealant; Brushes, rollers and tools; Distemper; Enamel and metal paint; Exterior emulsion; Interior emulsion; Other; Primer; Putty and texture; Stainer and thinner; Waterproofing; White cement; Wood finish and polish.
  - **clothes** (204 items, 20 categories): these already encode two levels in the string with " · ", for example "Women · Sarees", "Men · Bottoms" and "Kids · Baby (0–2 years)", alongside flat names such as "Winter wear", "Accessories", "Sportswear" and "Fabric (by metre)" — all from the CSVs in `backend/src/GroceryKhata.Api/Catalogue/Data/`.
- Kirana starter seeding: if the kirana trade is empty, the master list is first filled from `CommonCatalogue.Items` with `CatalogueItemId` set — [MasterItems.cs:150-160](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)

### Inferences
- **Is there a two-level tree?** Yes in three partial forms, but no complete one: (a) the global `Aisles` tree (section → aisle), which covers only common-catalogue items; (b) the per-shop `Category.ParentId`, a one-level parent that is stored and editable but used by no picker; (c) a " · " convention inside clothes category strings. No department → category → sub-category tree spans the 958-item kirana master list.
- The kirana CSV's 18 groups do not match the 16 aisles. Several CSV groups have no aisle: सूखे मेवे, अचार/सॉस/पापड़, चॉकलेट/टॉफ़ी, बेबी केयर and दांतों की देखभाल. "आटा, चावल, दाल" merges two aisles (atta-rice and dal). There is no फल/सब्ज़ी group in the CSV. Branded CSV items will rarely get a `CatalogueItemId`, because matching needs the generic name at the start (for example "Aashirvaad Whole Wheat Atta" does not start with "Atta"). So in the store most kirana items probably fall into `cat:<Hindi category>` chips, mixed with aisle chips. Duplicate-looking chips such as "Atta & rice" (aisle) next to "आटा, चावल, दाल" (shop category) are likely. I have not verified this against live data.
- A rail of about 16–18 top categories with sub-category tiles would need a new mapping: master category → top-level department, plus sub-categories such as Toor/Moong/Chana under Dal. That data exists nowhere today.

### Gaps
- I did not look at DB contents (how many products in real shops have `CatalogueItemId`, or which categories shops actually created). The CSV counts are from the seed files only.
- `StarterItems.cs` was not read in full. A grep shows no Category handling beyond the class declaration.

## 3. Item images usable for category tiles

### Takeaway
There are photos at three levels: shop product photos, master-item photos (platform-set, with credit) and drawn "item art" (emoji plus tint) for the 16 aisles and about 70 common items. Categories and aisles have no image fields. Tiles today borrow either the aisle emoji art or the first photographed item in the category.

### Cited Findings
- Shop product photo: `GET /api/v1/images/{shopId}/{productId}?size=thumb`. A product with no photo of its own falls back to the shared master photo (`MasterPhotos.Serve`). A thumbnail is generated on demand if it is missing — [ProductImages.cs:52, 99-125](backend/src/GroceryKhata.Api/Catalogue/ProductImages.cs)
- Master photo: `GET /api/v1/master-images/{id}?size=thumb`, anonymous. It is resized to full and thumb squares — [MasterPhotos.cs:46, 58, 86-95](backend/src/GroceryKhata.Api/Catalogue/MasterPhotos.cs). The sizes are `Full = 512, Thumb = 320` — [PhotoSources.cs:18](backend/src/GroceryKhata.Api/Catalogue/PhotoSources.cs)
- `MasterItem.ImageAt` and `ImageCredit` record when the platform team set a photo and its credit; otherwise shops show "Representative image" — [MasterItems.cs:44-48](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). `Product` also has `ImageCredit` and `PhotoReports`/`PhotoReportNote` (customers can report a wrong photo) — [Product.cs:160-165](backend/src/GroceryKhata.Api/Catalogue/Product.cs); [StoreApp.tsx:722-734](frontend/src/screens/store/StoreApp.tsx)
- `AISLE_ART`: one emoji plus a gradient and a tint for each of the 16 aisles (🌾 atta-rice, 🫘 dal, 🫗 oil-ghee, 🌶️ masala, 🍬 sugar-salt, 🥕 fruits-veg, 🥛 dairy, 🍵 tea-coffee, 🍞 breakfast, 🍪 snacks, 🍜 instant, 🥤 drinks, 🧴 personal, 🧼 cleaning, 🪔 pooja, 💡 household), with a fallback of 🛒 — [frontend/src/lib/itemArt.ts:18-36](frontend/src/lib/itemArt.ts). `BY_CATALOGUE_ID` gives one emoji per common item — [itemArt.ts:42-62](frontend/src/lib/itemArt.ts)
- `ItemCard` draws "the item's picture floating gently on its aisle's colours". It uses a photo when there is one and the emoji art otherwise — [components/ItemCard.tsx:6-11](frontend/src/components/ItemCard.tsx)
- PickItemsScreen category tiles use the first item in the category that has a photo, else that item's emoji on a rotating tint from `TINTS` — [PickItemsScreen.tsx:38-39, 85-86, 213-217](frontend/src/screens/PickItemsScreen.tsx)
- `frontend/public` holds only app icons. There are no category illustrations or SVG icon sets — `ls frontend/public`.

### Inferences
- Sub-category tiles with images could be built now by borrowing a representative master photo per group, as PickItemsScreen does. Coverage depends on how many master items have `ImageAt`, which I could not count without DB access. Proper category tiles would need a category image or icon field, or a curated art map like `AISLE_ART` extended to every master category.
- The Master list picker report advises against emoji as the main glyph because rendering differs across Android versions (see §4). Today's tiles and rails still depend on emoji.

### Gaps
- I do not know how many master items have photos. See reports/"Master list photos for all shops.md", which I did not read for this note.

## 4. What existing reports recommend, and whether it was implemented

### Takeaway
Three of the four reports describe category navigation, and the code has implemented much of it: the aisle tree, the picker rail and tiles, the 2-column store grid and 320 px thumbs. The pieces not yet built are bilingual category chips, the "All ▾" tile sheet in the picker, aisle sub-levels (item types), a category rail with icons, and category tiles in the store.

### Cited Findings
- **Master list picker redesign.md**
  - Recommends a starter bundle "★ लोकप्रिय 100", **6–8 bilingual chips plus a "सभी ▾" bottom sheet of 3-column icon tiles** (icon, Hindi name, English name, "3/45" progress), 56–64 px rows with thumbnails and pack-size chips, a dark tray, "Add all (45)" per category, select-all only at ≤50 items, Hinglish search, a short sticky stack (~104 px), and lucide icons or two-tone SVG tiles instead of emoji — [reports/Master list picker redesign.md:3, 46-52, 64](reports/Master%20list%20picker%20redesign.md)
  - Implemented: the popular bundle chip, "Add group" per category, select-all at ≤50, the thumbnail rows with pack chips and "+ add"/"✓", and the loose Hinglish search — [PickItemsScreen.tsx:16-36, 168, 205, 225-262](frontend/src/screens/PickItemsScreen.tsx)
  - Partly implemented: a mobile tile strip replaces the "All ▾" sheet ([PickItemsScreen.tsx:209-223](frontend/src/screens/PickItemsScreen.tsx)). The chips show **every** category, not 6–8. They are not bilingual: kirana shows Hindi strings and the other trades English strings. There is no "3/45" progress and no SVG icons (emoji fallback).
- **Desktop screen design review.md**
  - Found "Item picker (#/items/pick): 21 category chips, then 210 items in one column (page about 14,000px tall)". Recommends "A sticky category list on the left. On the right, a dense grid… 'select all in category'… sticky 'Add N items' bar", listed as priority 5 ("item picker category rail", size M) — [reports/Desktop screen design review.md:24, 59](reports/Desktop%20screen%20design%20review.md)
  - Implemented: the lg left rail and the 2/3-column list in PickItemsScreen ([PickItemsScreen.tsx:166-171, 236](frontend/src/screens/PickItemsScreen.tsx)). The kirana store also got a lg left rail ([StoreApp.tsx:410-421](frontend/src/screens/store/StoreApp.tsx)). The review looked at a clothes shop, not kirana — [Desktop screen design review.md:5](reports/Desktop%20screen%20design%20review.md)
- **Kirana catalogue variants and images.md**
  - Recommends a **preset bilingual two-level tree of about 15 top-level aisles**: Atta, Rice & Grains; Dal & Pulses; Oil & Ghee; Masala & Spices; Sugar, Jaggery & Dry Fruits; Dairy, Bread & Eggs; Fruits & Vegetables; Snacks & Biscuits; Instant & Packaged Food; Beverages; Personal Care; Cleaning & Detergents; Household; Pooja Items; Baby Care. Each would have **one sub-level of item types** (Toor, Moong, Chana under Dal), with brand and size kept on options. The aisles would be grouped under 4–5 home headers, and shops could add custom sub-categories without changing the shared top level — [reports/Kirana catalogue variants and images.md:9](reports/Kirana%20catalogue%20variants%20and%20images.md)
  - Tile spec: 4 per row at 360 dp (3 below 340 dp), about 16 px radius, a pastel tint from the category colour, one cut-out photo filling about 70%, and a label of at most 2 lines. A "category page: left rail of subcategory thumbnails + 2-column grid" is listed as **unverified** — [Kirana catalogue variants and images.md:35-39](reports/Kirana%20catalogue%20variants%20and%20images.md)
  - Images: 160/320 px WebP thumbnails, lazy loading, emoji-on-gradient fallback, and Open Food Facts (CC BY-SA) or GS1 DataKart as sources. Copying images from Blinkit/Zepto is ruled out — [Kirana catalogue variants and images.md:43-45](reports/Kirana%20catalogue%20variants%20and%20images.md)
  - Implemented: the section → aisle tree (16 aisles under 4 sections, Hindi and English) and the AisleTiles section-header grid ([CommonCatalogue.cs:31-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs); [AisleTiles.tsx:15-44](frontend/src/components/AisleTiles.tsx)), plus 320 px thumbs ([PhotoSources.cs:18](backend/src/GroceryKhata.Api/Catalogue/PhotoSources.cs))
  - Not implemented: the item-type sub-level, Baby Care and Dry Fruits as aisles, custom shop sub-categories under the shared aisles, and the left rail of sub-category thumbnails. I did not verify the variants/options model.
- **Product image grid for mobile.md**
  - Recommends **2 columns on phones (about 164 px cards at 360 px)** and 4–6 on desktop, a 320–360 px grid rendition, and a tighter byte budget. It "overrides the 3-column suggestion in our earlier catalogue research", while "Three columns still suit compact category tiles that carry no long text" — [reports/Product image grid for mobile.md:3, 9](reports/Product%20image%20grid%20for%20mobile.md)
  - Implemented in the store (`grid-cols-2 … lg:grid-cols-4/5`, `thumb` size) — [StoreApp.tsx:422-429](frontend/src/screens/store/StoreApp.tsx). BillScreen still uses 3 compact columns on phones — [BillScreen.tsx:764](frontend/src/screens/BillScreen.tsx)

### Inferences
- The new "All categories" layout follows on from these reports. It combines the Kirana report's two-level tree with item-type sub-levels and the Master picker report's icon tile sheet, applied to the ordering screens (store, household list and bill) rather than only the picker.
- The reports disagree on columns: 3-column product cards (Kirana report) versus 2-column cards (Image grid report). The later Image grid report says it overrides the earlier one for product cards, and 3–4 per row stays acceptable for category tiles.

### Gaps
- I did not check whether the Kirana report's options/variants layer (generic item with brand/pack options) was built. It affects whether sub-category tiles should be generic items such as "Atta" or real sub-categories.
- I did not read "Master list photos for all shops.md", "Supermarket and mall POS plan.md" or "Table format review of all pages.md". They may mention category navigation.

## 5. Existing i18n (Hindi / Gujarati) names for categories

### Takeaway
Only the 16 common aisles and 4 sections are fully localised: Hindi and English on the server, Gujarati in the frontend. Master-list and shop category names are stored as single raw strings with no translation (kirana in Hindi, the other trades in English), so a Gujarati or English user sees Hindi kirana category names in the picker.

### Cited Findings
- Each aisle carries `Name` (Hindi), `NameEn`, `Section` (Hindi) and `SectionEn` — [CommonCatalogue.cs:28-29, 38-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
- Gujarati names for all 16 aisles and 4 sections are kept in the frontend (for example 'atta-rice': 'લોટ અને ચોખા', dal: 'દાળ અને કઠોળ', pooja: 'પૂજાનો સામાન', household: 'ઘરવખરી'). `aisleName()` and `sectionName()` choose Hindi, Gujarati or English by the app language — [frontend/src/lib/aisleNames.ts:3-18](frontend/src/lib/aisleNames.ts)
- PickItemsScreen localises **item** names (`nameHi` when the language is Hindi) but shows category names as stored — [PickItemsScreen.tsx:78-79, 207](frontend/src/screens/PickItemsScreen.tsx)
- The kirana CSV category column is in Hindi only, and the CSV has no English or Gujarati category column (header `name,name_hi,category,unit,gst,hsn,packs`) — [kirana-master.csv:1-3](backend/src/GroceryKhata.Api/Catalogue/Data/kirana-master.csv)
- A grep for kirana category strings such as "पर्सनल केयर" and "सूखे मेवे" across the frontend/backend .ts/.tsx/.cs files finds them only in EF migration Designer files, not in any translation map.
- UI strings exist for category-related labels, for example Hindi `colCategory: 'श्रेणी'`, `allAisles: 'सब विभाग'`, `otherItems: 'बाकी सामान'` — [frontend/src/i18n/hiMore.ts:481, 515-516](frontend/src/i18n/hiMore.ts)
- The `Category` entity has no localised name fields — [Categories.cs:15-45](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)

### Inferences
- A bilingual or trilingual category rail would need translations for the 18 kirana master categories, and for the other trades' categories if Hindi or Gujarati is wanted there. The aisle tree's existing `aisleNames.ts` pattern (server Hindi and English plus client Gujarati) is a ready model to follow.

### Gaps
- I did not check whether `allAisles` ('सब विभाग') is still referenced by any component. A grep for `allAisles` in .tsx files returned nothing, so it may be a leftover key.
- I did not check the Gujarati i18n files for category-label keys beyond aisleNames.ts.
