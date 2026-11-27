# Current app flow: item and category from the common (master) list to the shop's list to the online store

Scope: code facts on branch `claude/mybillingsystem-app-review-9y3wq0` (checked 2026-11-27). All paths are relative to `/home/user/MybillingSystem/`. "Source" links are file:line references in the repository, not web URLs. No code was changed.

Short version: the master list's category is a **free-text string** on each `MasterItem`. When a shop picks an item, that string is copied onto the new `Product` and turned into the shop's own `Category` row by matching the normalised name. After that, nothing ties the shop's category to the master's category. The link from product to master row (`Product.MasterItemId`) is saved, but the update check matches items **by name** and compares **only GST and HSN**. The online store does not group by the shop's `Category` rows. It groups by a hard-coded grocery aisle number when the item matches one of the 71 common-catalogue items, and otherwise by the flat category string, sorted A to Z.

## 1. Master list: data model and seeding

### Takeaway
`MasterItem` is a flat, shared, platform-owned row: (Trade, Category string, Name, NameHi, Unit, GstRate, HsnCode, PackSizes, image, IsActive). It has no category id, no tree or parent, no aisle, and no attribute fields. Lists are seeded at start-up from `CommonCatalogue` (kirana), `StarterItems` (other trades) and 7 embedded CSVs. A one-way re-grouping step moves old category names to new group names on the master rows only.

### Cited Findings
- `MasterItem` fields: `Id` (int), `Trade` (string, e.g. "building_materials"), `Category` (required string), `Name`, `NameHi`, `Unit`, `GstRate` (nullable, "a CA should confirm"), `HsnCode`, `CatalogueItemId` (link to the grocery common list), `IsActive` ("False: no longer offered to new picks. Shops that took it keep their copy. Never deleted"), `PackSizes` (List<string>, "a shop that picks it gets one item per size"), `ImageAt`, `ImageCredit`, `UpdatedAt`, `CreatedAt` (null for the list the app started with) — [MasterItems.cs:14-54](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Comment: "Shared by every shop, owned by none (no row-level security). An item sold in two trades has a row in each." — [MasterItems.cs:10-13](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- DB mapping: table `master_items`, `Category` max 60 chars, `Trade` max 30, index on (Trade, Category), FK to `catalogue_items` with SetNull. There is no category table or FK for the master category — [AppDbContext.Catalogue.cs:29-41](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
- The photo is in a separate `MasterItemImage` table keyed by `MasterItemId` (cascade delete) — [AppDbContext.Catalogue.cs:23-26](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
- `MasterSuggestion` (a shop asks for a missing item) carries its own free-text `Category`, `Trade`, `Name` and `Unit`. On approval it becomes a master item through `AddAsync` — [MasterItems.cs:60-86](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [MasterItems.cs:585-602](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- `SeedAsync` runs at start-up and fills only trades with no rows yet. Kirana gets `CommonCatalogue.Items`, with Category = the common item's Hindi category, Name = NameEn, NameHi = Hindi, and `CatalogueItemId` set. Every other trade gets `StarterItems.For(type)`, with Category `r.Category ?? "Other"` — [MasterItems.cs:148-171](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- `SeedListsAsync` reads 7 embedded CSVs (medical, kirana, paint, hardware, beauty, clothes, **electrical**). It adds only names not already in the trade. For an existing row it fills only a missing `NameHi` or missing `PackSizes`, and never overwrites an admin edit — [MasterItems.cs:178-243](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Rows added by `SeedListsAsync` get no `CreatedAt` (the object initialiser sets Trade, Name, NameHi, Category, Unit, GstRate, HsnCode, UpdatedAt and PackSizes only). The CSV category is cut to 60 chars, and an empty one becomes "Other" — [MasterItems.cs:234-242](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Re-grouping: when an existing master row's category is not one of the CSV's current groups, it moves to the group the CSV lists for that name, else to `StarterGroups[oldCategory]`. This changes `MasterItem.Category` only — [MasterItems.cs:245-255](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- `StarterGroups` maps old names to new groups, e.g. "Men" → "Men · Shirts and T-shirts", "Hinges"/"Fittings" → "Door and furniture fittings", "Plumbing" → "Pipes and fittings", "Antibiotics (Schedule H)" → "Antibiotics & infections", "Diabetes & BP" → "Heart & BP" — [MasterItems.cs:262-273](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- CSV format: header `name,name_hi,category,unit,gst,hsn,packs`. Pack sizes are split by "|". The medical CSV has no `packs` column (header `name,name_hi,category,unit,gst,hsn`) — [Catalogue/Data/*.csv line 1](backend/src/GroceryKhata.Api/Catalogue/Data/)
- Category counts per CSV (counted from column 3):
  - beauty: 1017 rows, 13 groups, including the new **"Baby and kids care" (22)**, e.g. Johnson's Baby Powder 100 g|200 g|400 g
  - clothes: 204 rows, 20 groups, all encoding two levels with " · " ("Women · Sarees", "Kids · Girls")
  - **electrical: 258 rows, 18 groups** ("LED bulbs and tubes", "House wire", "MCB, RCCB and isolators", "Fans", ...)
  - hardware: 319 rows, 14 groups
  - kirana: 958 rows, 18 **Hindi** groups ("पर्सनल केयर", "आटा, चावल, दाल", "बेबी केयर" ...)
  - medical: 847 rows, 24 groups
  - paint: 337 rows, 13 groups

  — [Catalogue/Data/](backend/src/GroceryKhata.Api/Catalogue/Data/)
- Both new lists came in recent commits: da2b37f "Add electrical master list (258 items in 18 groups)" (changes electrical-master.csv, a 3-line change to MasterItems.cs, StarterItems.cs) and 38101da "Add 65 beauty items, a baby and kids care group" (beauty-master.csv). Each commit also adds text to `reports/Hardware paint and water menu.md`. Neither commit adds menu code — `git show --stat da2b37f 38101da`
- Electrical attributes are only inside the item name: "Wire 1.0 sq mm (90 m coil)", "Wire 1.5 sq mm (per metre)", all in category "House wire", unit `box`/`metre` — [electrical-master.csv:2-6](backend/src/GroceryKhata.Api/Catalogue/Data/electrical-master.csv). Clothes ages sit in pack sizes ("2-3Y|3-4Y...") — [clothes-master.csv:133-135](backend/src/GroceryKhata.Api/Catalogue/Data/clothes-master.csv)
- Kirana `CommonCatalogue`: `CatalogueItem` (Id fixed forever, Name Hindi, NameEn, Unit, Category, TypicalPrice, **Aisle**). Table `catalogue_items` is seeded with `HasData` and has no RLS — [CommonCatalogue.cs:9-27](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs), [AppDbContext.Catalogue.cs:55-66](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
- `Aisles.All` is a hard-coded two-level tree of 4 sections ("Daily staples", "Dairy & breakfast", "Snacks & drinks", "Home, cleaning & pooja") with 16 aisle codes (atta-rice, dal, oil-ghee, masala, ... household). Its comment: "Two levels only; brand and pack size are options on an item, not more levels" — [CommonCatalogue.cs:32-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs). It is served anonymously at `GET /api/v1/catalogue/aisles` — [CatalogueEndpoints.cs:25](backend/src/GroceryKhata.Api/Catalogue/CatalogueEndpoints.cs)
- The common catalogue has 71 rows. Each row's aisle comes from **id ranges** (`AisleOf(id)`: ≤6 or 14 → atta-rice, ≤13 → dal, ...) — [CommonCatalogue.cs:118-157](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs). `CommonCatalogue.Match(name)` matches a product to a catalogue id by exact Hindi or English name, or by name prefix followed by " " or "(" — [CommonCatalogue.cs:~75-88](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
- `StarterItems.For(type)` has starter rows for 15 trades (BuildingMaterials, Paint, WaterSupply, Beauty, Sweets, Toys, Clothes, Hardware, Electrical, Woodwork, Banquet, Restaurant, Lodge, Medical, SalesService), with categories as strings such as "Spare parts" — [StarterItems.cs:12-40](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)
- Admin endpoints under `/api/v1/platform/master-items`: list/search (searches Name, NameHi and Category with ILIKE), add, change (PUT), CSV import, and approve/reject suggestions. `Check` needs a Category of 2 to 60 chars and allows at most 10 pack sizes — [MasterItems.cs:131-140](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [MasterItems.cs:648-684](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)

### Inferences
- The kirana master list has two category vocabularies. The 71 `CommonCatalogue` rows carry an `Aisle` code (on `CatalogueItem`, not on `MasterItem`) and a Hindi category. The 958 CSV rows carry only one of 18 Hindi category strings, and these do not map one-to-one onto the 16 aisles.
- Clothes is the only trade whose master categories encode a parent ("Kids · Girls"), and only by a text convention.
- Electrical and beauty items added by `SeedListsAsync` have `CreatedAt = null`. They will never count as "new" in the shop update check (see section 2), so shops that existed before the electrical list and the 65 new beauty items arrived are not told about them.

### Gaps
- I did not count how many of the 958 kirana CSV names `CommonCatalogue.Match` links to a common id, so the share of kirana master items that reach an aisle is unknown.

## 2. Shop list: Product, Category entity, /products/from-master and later master edits

### Takeaway
The shop has a real per-shop `Category` table: tenant-owned with RLS, unique by normalised name, at most one parent level, its own sort order, an active flag and defaults. `Product` keeps both `CategoryId` and a copied `Category` name. Picking copies the master's category **string**, and `Categories.LinkAsync` finds or creates the shop category by name. `MasterItemId` is kept on the product. Later master edits reach the shop only for GST and HSN, only through a name match, and only when the owner applies them. Master category changes never reach shop items.

### Cited Findings
- `Category : Entity, ITenantOwned`: ShopId, Name, NormalisedName, `ParentId` ("The group above it, at most one level"), SortOrder, IsActive, MergedIntoId, DefaultGstRate, DefaultHsn, DefaultUnit, TargetCoverDays, SlowAfterDays. Its comment: "Product.Category keeps a copy of the name for the many screens that group by it" — [Categories.cs:9-45](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- Table `categories` has a tenant key, Name max 60, and a unique (ShopId, NormalisedName) index filtered to `merged_into_id IS NULL` — [AppDbContext.Catalogue.cs:130-139](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
- Migration `20261112060445_Categories` backfilled categories from the distinct `products.category` strings per shop, set `products.category_id`, and enabled shop isolation (RLS policy `shop_isolation` on `app.shop_id`) — [20261112060445_Categories.cs:55-75](backend/src/GroceryKhata.Api/Persistence/Migrations/20261112060445_Categories.cs), [MigrationExtensions.cs:8-20](backend/src/GroceryKhata.Api/Persistence/MigrationExtensions.cs)
- Other catalogue migrations: StockAndCatalogue, CommonCatalogue, ProductCatalogueLink, AislesAndPhotos, MasterItems, MasterItemPackSizes, MasterSuggestions, MasterItemCreatedAt, MasterItemPhotos, Categories. None is named node or tree — [Persistence/Migrations/](backend/src/GroceryKhata.Api/Persistence/Migrations/)
- Category endpoints `/api/v1/categories`: list (member), and create, update, merge and order (owner only) — [Categories.cs:63-72](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). A rename rewrites `Product.Category` for every item with that `CategoryId` — [Categories.cs:224-229](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). A merge moves the products and child categories and keeps the old row as a pointer — [Categories.cs:234-256](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). A parent must be a live top-level category (two levels) — [Categories.cs:161-176](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- `ResolveAsync`: tidies the name, cuts it to 60, matches on the normalised name (following merges), and otherwise INSERTs a new top-level category with sort_order max+1 (`ON CONFLICT DO NOTHING`). New categories always have `ParentId` NULL — [Categories.cs:83-111](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). `LinkAsync` sets `(product.CategoryId, product.Category) = (category.Id, category.Name)` — [Categories.cs:114-129](backend/src/GroceryKhata.Api/Catalogue/Categories.cs)
- Product fields that matter for category and master: `Category` (copied name), `CategoryId`, `CatalogueItemId` (common-list match), `PackSize`/`PackUnit`, `IsDefaultOption`, `ImageAt`, `GstRate`, `HsnCode`, `StyleName`/`Size`/`Colour` (clothes), `MasterItemId` + `MasterItem` nav ("its photo shows here until the shop adds its own"). There is no "for" (women/men/kids), wattage, sq mm or brand field, and no `TreeKey` — [Product.cs:5-173](backend/src/GroceryKhata.Api/Catalogue/Product.cs). A repo-wide grep for `CategoryMenu|TreeKey|CategoryNode|tradeTree` in backend and frontend source found nothing.
- FK `products.master_item_id` → master_items, SetNull — [AppDbContext.Catalogue.cs:72](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
- `POST /api/v1/products/from-master` (`PickAsync`) takes 1 to 500 ids plus optional sizes per id, and calls `AddToShopAsync` — [MasterItems.cs:126](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [MasterItems.cs:311-320](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- `AddToShopAsync` steps — [MasterItems.cs:337-360](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs):
  1. Keeps only active masters in the shop's trades (`ExtraTypes` + `Type`).
  2. Takes the name in the owner's language (Hindi `NameHi` when `PreferredLanguage == "hi"`).
  3. With no pack sizes, makes one row. Otherwise makes one row per chosen size (or every size), named `"{name} {size}"`, unit `Packet` for kg/g/litre/ml.
  4. Copies `i.Category`, `CatalogueItemId`, `GstRate`, `HsnCode` and `MasterItemId` onto each row, and `Schedule` for medical.
  5. For clothes, sets `StyleName = name` and `Size = size`.
  6. Price is 0.
- Rows go through `StockService.ImportAsync`. It **skips any row whose name the shop already has** — [StockService.cs:477-481](backend/src/GroceryKhata.Api/Catalogue/StockService.cs). It creates the product, calls `Categories.LinkAsync` (with a cache), then sets CatalogueItemId, `MasterItemId`, StyleName/Size, PackSize, GstRate and HsnCode — [StockService.cs:474-500](backend/src/GroceryKhata.Api/Catalogue/StockService.cs). `PackSize` is not set by the master pick (the `ImportRow` has no PackSize from `AddToShopAsync`).
- Category defaults (DefaultGstRate, DefaultHsn) are applied only on single-item create (`request.GstRate ?? defaults?.DefaultGstRate`), not in `ImportAsync` — [StockService.cs:146-151](backend/src/GroceryKhata.Api/Catalogue/StockService.cs)
- `AddTradeListAsync` (a shop opened by an admin) adds every active master item of the shop's **primary `Type` only**, not `ExtraTypes` — [MasterItems.cs:323-335](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Update check `GET /api/v1/master-items/updates`: first runs `LinkByNameAsync`, which sets `MasterItemId` on unlinked products whose name matches a master name or name+size — [MasterItems.cs:502-521](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). Then `CompareAsync` — [MasterItems.cs:429-449](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs):
  - It matches products to masters **by name** (`NamesFor`: NameHi or Name, plus every "{name} {size}"), not by `MasterItemId`.
  - "Changed" = the master's GstRate or HsnCode is set and differs from the product's. Category is not compared.
  - "Fresh" = masters with `CreatedAt` in the last 30 days that the shop doesn't hold by name, at most 200.
  - The comment says: "Items the shop renamed or made itself never match."
- `POST /master-items/updates/apply` copies only `GstRate` and `HsnCode` for the chosen product ids — [MasterItems.cs:533-546](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). The home screen fetches `/v1/master-items/updates` for owners — [HomeScreen.tsx:60](frontend/src/screens/HomeScreen.tsx)
- Shop edits of an item: on update, `product.Category = Clean(request.Category)` then `LinkAsync`. The name change re-matches `CatalogueItemId`. `MasterItemId` is untouched — [StockService.cs:208-212](backend/src/GroceryKhata.Api/Catalogue/StockService.cs)
- Admin `ChangeAsync` (PUT) rewrites the master row's Category, Name, GST, HSN, IsActive and PackSizes in place, with no fan-out to products — [MasterItems.cs:705-721](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)

### Inferences
- The master category "follows" the item into the shop **only once, at pick time, and only by its text**. When two trades' master lists use the same group name, a multi-trade shop gets one merged shop category. When a shop already renamed its own category, a newly picked item with the old name recreates the old name, unless it was merged (merges are followed, renames are not).
- Clothes picks create flat shop categories literally named "Men · Shirts and T-shirts", with `ParentId` NULL. The two-level structure in the master text is lost, although `Category.ParentId` exists.
- Since `CompareAsync` uses names rather than `MasterItemId`, a renamed item keeps its photo link but stops getting GST/HSN prompts. A category re-grouping on the master never reaches any shop.

### Gaps
- I found no code path that sets a shop category's `ParentId` or `SortOrder` from master data.

## 3. How shopkeepers pick from the common list and create their own categories and items

### Takeaway
`PickItemsScreen` lists `/v1/master-items` and groups it client-side by the master `category` string: a side rail on desktop, chips and picture tiles on phones, trade tabs for multi-trade shops, a "Popular" starter bundle, fuzzy search, add-whole-group, pack-size ticks, and "suggest an item". Own categories come from typing a name on an item or from `CategoriesScreen`, which supports one parent level.

### Cited Findings
- `GET /api/v1/master-items` returns the active items of all the shop's trades (`ExtraTypes` + `Type`), optionally filtered by `trade`, ordered Trade → Category → Name, and flagged `Popular`. Popular = has a `CatalogueItemId` (kirana) or its name is in that trade's `StarterItems` — [MasterItems.cs:278-308](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- Picker structure — [PickItemsScreen.tsx:14](frontend/src/screens/PickItemsScreen.tsx), [PickItemsScreen.tsx:53-95](frontend/src/screens/PickItemsScreen.tsx):
  - The `MasterItem` type has id, trade, category, name, nameHi, unit, gstRate, packSizes, imageAt and popular.
  - Trades come from the items; `categories` = distinct `i.category` for the active trade in server order; there are tiles per category and groups by `i.category`.
- UI — [PickItemsScreen.tsx:167-170](frontend/src/screens/PickItemsScreen.tsx), [PickItemsScreen.tsx:198-207](frontend/src/screens/PickItemsScreen.tsx), [PickItemsScreen.tsx:107](frontend/src/screens/PickItemsScreen.tsx), [PickItemsScreen.tsx:128-129](frontend/src/screens/PickItemsScreen.tsx):
  - a desktop side rail (★ Popular, All, one button per category)
  - trade chips when there is more than one trade
  - a search box
  - category chips
  - "add group"
  - a POST to `/v1/products/from-master` with `{ ids, sizes }`
- Search tolerates spelling ("aata" finds "Atta") — [PickItemsScreen.tsx:23](frontend/src/screens/PickItemsScreen.tsx)
- "Suggest an item" posts `{trade, name, category (default 'Other'), unit}` to `/v1/master-items/suggest` — [PickItemsScreen.tsx:152-157](frontend/src/screens/PickItemsScreen.tsx), [PickItemsScreen.tsx:273-281](frontend/src/screens/PickItemsScreen.tsx)
- `ItemsScreen` also loads `/v1/master-items` for owners (suggestions) and shows the shop's own category chips in the shop's order via `orderedNames(categoryRows, products.map(p => p.category))` — [ItemsScreen.tsx:47-57](frontend/src/screens/ItemsScreen.tsx), [ItemsScreen.tsx:141-144](frontend/src/screens/ItemsScreen.tsx). `orderedNames` puts active known categories in SortOrder, then unknown names A to Z, and ignores `parentId` — [lib/categories.ts:23-28](frontend/src/lib/categories.ts)
- `CategoriesScreen` edits name, parent (`select` of top-level categories), active flag, GST, HSN, unit, cover days and slow days. It shows "Parent › Child" and supports reordering — [CategoriesScreen.tsx:15-17](frontend/src/screens/CategoriesScreen.tsx), [CategoriesScreen.tsx:50-59](frontend/src/screens/CategoriesScreen.tsx), [CategoriesScreen.tsx:69-103](frontend/src/screens/CategoriesScreen.tsx). Item forms use `CategoryInput` with `useCategories` / `sameCategory` — [components/CategoryInput.tsx:3](frontend/src/components/CategoryInput.tsx)

### Inferences
- The picker's grouping is one level and string-based. Clothes' "Women · Sarees" shows as one long chip, and kirana shows Hindi groups with no aisle tiles, even though `Aisles` exists.
- The prior report "Category menu for all shop types" proposes a shared `<CategoryMenu>` plus a per-trade tree registry. No such component or registry exists in code (grep returned nothing).

### Gaps
- I did not read the full picker render code for desktop tile images or the exact Popular-bundle untick behaviour.

## 4. Customer side: online store grouping, filtering and visibility

### Takeaway
`GET /api/v1/store/{slug}` lists every active product with a price above 0 (services included), with a computed `Available`, the category **string**, `CatalogueItemId`, and the photo (the shop's own or the master's). The store groups items by aisle when `aisleOf(catalogueItemId)` resolves, else by `cat:<category string>` (A to Z), else "Other". It ignores the shop's category order, parents and active flag. Kirana gets a left rail on wide screens and no filter drawer. Other trades get chips and a filter drawer. Only the primary `Shop.Type` reaches the store.

### Cited Findings
- Visibility filter: `p.IsActive && p.SellingPrice > 0` ("an item without a price can't be ordered online"), ordered by Category then Name — [StoreEndpoints.cs:88-91](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). There is no per-item online/hidden flag, no stock filter (out-of-stock items are listed with Available ≤ 0), no photo requirement, and no `Category.IsActive` check.
- The store needs the `Store` entitlement — [StoreEndpoints.cs:83-86](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
- `Available` = 0 if `OutOn == today`, 9999 if `Uncounted`, else `Stock - Reserved` — [StoreEndpoints.cs:92](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs), [StoreEndpoints.cs:24-28](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). `Product.OnSale` = `OutOn != today && (Uncounted || Stock - Reserved > 0)` — [Product.cs:91-92](backend/src/GroceryKhata.Api/Catalogue/Product.cs)
- An order is refused for out-of-stock goods (non-service, `OutOn == today`, or not enough `Stock - Reserved`) — [StoreEndpoints.cs:210-219](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
- Image: `p.ImageAt ?? p.MasterItem.ImageAt`, with the credit falling back to "Representative" — [StoreEndpoints.cs:93-94](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
- A missing `CatalogueItemId` is filled by `CommonCatalogue.Match(i.Name)` — [StoreEndpoints.cs:97](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
- `StoreItemDto` carries only `string? Category`, with no CategoryId, parent or sort order — [StoreEndpoints.cs:20-22](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). `StoreDto` carries `ShopType? Type` but not `ExtraTypes` — [StoreEndpoints.cs:10-11](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs), [StoreEndpoints.cs:166-167](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). The shop has `Type` (default Kirana) and `string[] ExtraTypes` — [Shop.cs:121](backend/src/GroceryKhata.Api/Shops/Shop.cs), [Shop.cs:157](backend/src/GroceryKhata.Api/Shops/Shop.cs)
- Jewellery shows tagged `JewelleryPieces` separately when `JewelleryShowOnline` is on, ordered by piece Category — [StoreEndpoints.cs:125-137](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
- Frontend layout switches — [StoreApp.tsx:298-299](frontend/src/screens/store/StoreApp.tsx), [lib/productSheet.ts:2](frontend/src/lib/productSheet.ts):
  - `kirana = !data.type || data.type === 'kirana'`
  - `full = fullPageTrades.includes(type)`, where `fullPageTrades = ['clothes','jewellery','fashion','footwear','furniture']`
- Grouping: `aisleOfItem = aisleOf(i.catalogueItemId) ?? (i.category ? 'cat:'+i.category : 'other')`. The chips are All, then the aisles that hold stock (in `useAisles()` order), then own categories `.sort()` alphabetically, then "Other" — [StoreApp.tsx:301-312](frontend/src/screens/store/StoreApp.tsx)
- The frontend `aisleOf` is a hard-coded copy of the backend id ranges — [lib/itemArt.ts:64-72](frontend/src/lib/itemArt.ts). Aisle names come from `/v1/catalogue/aisles` — [lib/aisles.ts](frontend/src/lib/aisles.ts)
- Search ranks on `${name} ${category}`. Filters cover sort (relevance, low, high), in-stock only and max price. In-stock items sort first — [StoreApp.tsx:315-319](frontend/src/screens/store/StoreApp.tsx)
- The filter button shows only when `!kirana` — [StoreApp.tsx:387](frontend/src/screens/store/StoreApp.tsx). For kirana the chips are hidden on lg (`lg:hidden`) and a left rail of the same chip list shows instead — [StoreApp.tsx:389](frontend/src/screens/store/StoreApp.tsx), [StoreApp.tsx:410-418](frontend/src/screens/store/StoreApp.tsx). The filter drawer is at [StoreApp.tsx:472-486](frontend/src/screens/store/StoreApp.tsx)
- Beauty shows salon services (items with `serviceMinutes`) separately — [StoreApp.tsx:350](frontend/src/screens/store/StoreApp.tsx)

### Inferences
- A kirana shop's store mixes two systems: aisle chips for items that match the 71 common items, and the picked items' Hindi master-category chips for the rest. The same kind of item can land in an aisle or in a category chip depending on its name.
- For non-kirana trades the customer sees the shop's flat category strings A to Z: master groups such as "House wire" and "Baby and kids care" if the shop kept them, and "Men · Shirts and T-shirts" as one literal chip. The shopkeeper's order and parent groups from `CategoriesScreen` are not used.
- A kirana + beauty shop (Type kirana, ExtraTypes [beauty]) gets the kirana layout with no filter drawer for its beauty goods. A beauty shop with a kirana extra gets the non-kirana layout, though common grocery items still map into aisles.
- A hidden (`IsActive=false`) shop category still shows on the store, because the store only checks product `IsActive`.

### Gaps
- I did not check whether the store frontend renders `full` product pages differently by category, or how the jewellery pieces UI groups.

## 5. Concrete gaps blocking "common-list categories follow the item into the shop's list and show the same way to customers"

### Takeaway
The pieces exist: a master category string, a per-shop Category table with a parent, `MasterItemId` on products, and a hard-coded kirana aisle tree. They are joined only by text at pick time, and the store bypasses the shop's Category rows entirely.

### Cited Findings (each a code fact)
1. **The master category is free text with no stable id.** `MasterItem.Category` is a `string` with max 60, and there is no master-category table — [MasterItems.cs:21](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [AppDbContext.Catalogue.cs:34](backend/src/GroceryKhata.Api/Persistence/AppDbContext.Catalogue.cs)
2. **There is no master tree.** The parent level is only text in clothes (" · "). The only real tree is the hard-coded kirana `Aisles` (16 aisles, 4 sections), and it sits on `CatalogueItem` (71 rows), not on `MasterItem` — [CommonCatalogue.cs:26](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs), [CommonCatalogue.cs:36-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs)
3. **Picking maps to the shop category by name only.** Nothing records which master category a shop category came from. A new shop category always has `ParentId` NULL — [Categories.cs:101-106](backend/src/GroceryKhata.Api/Catalogue/Categories.cs), [StockService.cs:483-486](backend/src/GroceryKhata.Api/Catalogue/StockService.cs)
4. **Master category changes don't reach shop items.** These all change master rows only:
   - `SeedListsAsync` re-grouping — [MasterItems.cs:245-255](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
   - admin `ChangeAsync` — [MasterItems.cs:705-721](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
   - `CompareAsync`/`ApplyChangesAsync`, which compare and apply GST and HSN only — [MasterItems.cs:441](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [MasterItems.cs:540](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
5. **The product → master link exists but the update logic doesn't use it.** `MasterItemId` is saved — [StockService.cs:486](backend/src/GroceryKhata.Api/Catalogue/StockService.cs). `CompareAsync` matches by name, so renamed items drop out — [MasterItems.cs:426-440](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). A re-pick of an existing name is skipped, so it never refreshes the category — [StockService.cs:477-481](backend/src/GroceryKhata.Api/Catalogue/StockService.cs)
6. **Seeded new items are invisible as "new".** `SeedListsAsync` sets no `CreatedAt`, and "fresh" needs `CreatedAt >= now-30d` — [MasterItems.cs:234-242](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs), [MasterItems.cs:445-447](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). This affects the electrical list and the new beauty "Baby and kids care" items for existing shops.
7. **The store ignores the shop's Category rows.** The DTO has only the category string — [StoreEndpoints.cs:20-22](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). The frontend sorts own categories A to Z and ignores SortOrder and ParentId — [StoreApp.tsx:305](frontend/src/screens/store/StoreApp.tsx). There is no `Category.IsActive` filter — [StoreEndpoints.cs:90](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
8. **The kirana store uses aisles from a hard-coded id-range copy, not categories.** See [lib/itemArt.ts:64-72](frontend/src/lib/itemArt.ts) vs [CommonCatalogue.cs:133-157](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs). The 958 CSV kirana items fall to Hindi category chips unless their name prefix-matches a common item — [StoreApp.tsx:303](frontend/src/screens/store/StoreApp.tsx)
9. **There are no attribute fields** for size in sq mm, amps or watts, coil length, "for" (women/men/kids/baby), shade, brand or salt. They live in item names or pack-size strings. Clothes has `StyleName`/`Size`/`Colour` only — [Product.cs:138-142](backend/src/GroceryKhata.Api/Catalogue/Product.cs), [electrical-master.csv:2-6](backend/src/GroceryKhata.Api/Catalogue/Data/electrical-master.csv)
10. **Multi-trade is partial.** The picker and the update check use `ExtraTypes` — [MasterItems.cs:280-281](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). `AddTradeListAsync` uses only the primary `Type` — [MasterItems.cs:328-330](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). The store gets only `Type` and picks one layout (kirana rail with no filters vs chips with filters) — [StoreEndpoints.cs:10-11](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs), [StoreApp.tsx:298](frontend/src/screens/store/StoreApp.tsx)
11. **No per-item online flag.** Every active, priced product (services included) is shown — [StoreEndpoints.cs:88-91](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs)
12. **Category defaults are not applied to bulk or master picks.** Only single-item create fills GST and HSN from the category — [StockService.cs:148-151](backend/src/GroceryKhata.Api/Catalogue/StockService.cs)
13. **There is no shared category menu component or per-trade tree registry.** The reports propose them, but a grep for `CategoryMenu|TreeKey|CategoryNode|tradeTree` finds nothing in `frontend/src` or `backend/src`.

### Inferences
- The lowest-friction foundation in today's code would be: a stable master category key (or a tree node id) on `MasterItem`, a matching key on the shop `Category`, sync logic that uses `Product.MasterItemId` rather than names, and a store DTO that sends `CategoryId`, parent and sort order. This is an inference from the gaps, not something the code or reports implement.
- Claims in `reports/Category menu for all shop types.md` were checked against code: "Clothes categories already encode two levels" is confirmed (text only), and "no per-trade configuration anywhere" is confirmed. Its statement that "Electrical has only 14 starter rows" is **out of date**: electrical-master.csv now has 258 rows (commit da2b37f).

### Gaps
- I did not examine offline sync (`lib/catalogueSync.ts`, local DB) for how the category is cached on devices. It may matter for any change to Category ids.
- I did not verify the test coverage in `GroceryKhata.Api.Tests/TestingFixesTests.cs` for the new electrical and beauty groups.
