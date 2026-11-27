# App trades audit: what the code already has per trade for a shared category menu

Scope: repository code and `reports/` only. All sources are paths in /home/user/MybillingSystem. Paths are relative to the repo root. Item and category counts come from parsing each CSV with Python `csv.DictReader` (header row excluded).

## 1. Which shop types exist and how the app switches behaviour by trade

### Takeaway
There are 19 trades. Each shop has one main `Type` plus `ExtraTypes`, and the frontend merges them with `shopTypes()`. Behaviour switches through scattered `types.includes('<trade>')` checks and a few trade lists (`TRADE_TYPES`, `COMMISSION_TYPES`, `SERVICE_TYPES`, `fullPageTrades`). No per-trade configuration object exists, so a per-trade menu would need its own registry.

### Cited Findings
- The backend `enum ShopType` has 19 values: Kirana, BuildingMaterials, Paint, Hardware, Other, WaterSupply, Beauty, Sweets, Toys, Clothes, Electrical, Woodwork, Banquet, Restaurant, Lodge, Medical, Dairy, SalesService and Jewellery. Its doc comment says "the type only changes defaults (units, starter items, bill buttons)". — [backend/src/GroceryKhata.Api/Shops/Shop.cs:195-235](backend/src/GroceryKhata.Api/Shops/Shop.cs)
- `Shop.ExtraTypes` is a `string[]` of the other trades the shop works in ("kirana" plus "sweets"). `Type` is the main trade. — [Shops/Shop.cs:156-157](backend/src/GroceryKhata.Api/Shops/Shop.cs)
- The frontend defines the `ShopType` union and the `SHOP_TYPES` array with the same 19 snake_case values. — [frontend/src/lib/reports.ts:128-129](frontend/src/lib/reports.ts)
- Trade lists in the same file:
  - `STARTER_TYPES` (trades with a starter list) at line 131.
  - `TRADE_TYPES = ['building_materials','paint','hardware','electrical','woodwork']` at line 132.
  - `COMMISSION_TYPES = ['paint','electrical','woodwork']` at line 134.
  - `sendsToSites()`: building materials, hardware, electrical and woodwork, at line 137.
  - `SERVICE_TYPES = ['beauty','restaurant','lodge','banquet']` at line 140.
  - `shopTypes(s)` returns `[s.type ?? 'kirana', ...extraTypes]` at lines 143-145.
  - `isTrade(types)` at line 148.
  - Source: [frontend/src/lib/reports.ts:131-148](frontend/src/lib/reports.ts)
- `isTrade` is used on Home for the rates tile ([HomeScreen.tsx:127](frontend/src/screens/HomeScreen.tsx)), in Settings ([SettingsScreen.tsx:261](frontend/src/screens/SettingsScreen.tsx)) and on the bill screen to show the trade panel ([BillScreen.tsx:1230-1232](frontend/src/screens/BillScreen.tsx)).
- The Home tiles and cards switch by `types.includes(...)` in [HomeScreen.tsx:121-138](frontend/src/screens/HomeScreen.tsx):
  - jewellery → `/jewellery`
  - lodge → `/rooms`
  - restaurant → `/tables`
  - banquet → `/bookings`
  - woodwork or sales_service → `/jobs`
  - COMMISSION_TYPES → `/painters`
  - medical → `MedicalHomeCard`
  - dairy → `DairyHomeCard`
  - sales_service → `ServiceHomeCard`
  - beauty → `SalonHomeCard`
- The desktop side menu adds `/jewellery` for jewellery shops and `/bardana` (empty bags) for kirana. It also computes `medical` from the trades for the batch and H1 pages. — [App.tsx:326-359](frontend/src/App.tsx)
- On the product form, `types.includes('medical')` and `types.includes('sales_service')` gate the medical fields (schedule, strip size) and the serial and warranty fields. — [ProductScreen.tsx:286-288](frontend/src/screens/ProductScreen.tsx)
- Trade-specific screens:
  - `MedicalScreen.tsx`: the home card links to `/medical/stock` (batches), `/medical/refills`, `/medical/reorder`, `/medical/month`, `/medical/returns`, `/medical/h1` and `/medical/setup`. — [MedicalScreen.tsx:18-35](frontend/src/screens/MedicalScreen.tsx)
  - Also `MedicineStockScreen.tsx`, `PharmacyAssistScreens.tsx`, `PharmacyMonthScreen.tsx` and `H1RegisterScreen.tsx`.
  - `JewelleryScreen.tsx` has 12 tabs: bill, today, buyOld, stock, rates, girvi, schemes, orders, approvals, workshop, trays and reports. — [JewelleryScreen.tsx:25](frontend/src/screens/JewelleryScreen.tsx)
  - Paint and hardware: `TradeBill.tsx` (a tinted-line sheet and shade history at lines 96, 145-154), `PaintQuote.tsx`, `PaintersScreen.tsx` and `RatesScreen.tsx`.
  - Sales and service: `SerialsScreen.tsx`, `ServiceDeskScreen.tsx`, `ClaimsScreen.tsx` and `AmcScreen.tsx`.
  - Beauty: `SalonSetupScreen.tsx`, `AppointmentsScreen.tsx` and `BridalScreen.tsx`.
- The online store treats trades in three ways:
  - `kirana = !data.type || data.type === 'kirana'` at [store/StoreApp.tsx:298](frontend/src/screens/store/StoreApp.tsx).
  - The filter button is hidden for kirana (`{!kirana && …}`) at [StoreApp.tsx:387](frontend/src/screens/store/StoreApp.tsx).
  - `fullPageTrades = ['clothes','jewellery','fashion','footwear','furniture']` get a full product page instead of a half-sheet. — [lib/productSheet.ts:1-2](frontend/src/lib/productSheet.ts); used at [StoreApp.tsx:299](frontend/src/screens/store/StoreApp.tsx)
  - Beauty gets salon services from `serviceMinutes`. — [StoreApp.tsx:350](frontend/src/screens/store/StoreApp.tsx)
- The bill screen's only trade-specific branches are behavioural, not navigational:
  - style grouping for clothes ([BillScreen.tsx:66-68, 813](frontend/src/screens/BillScreen.tsx))
  - the schedule prescription gate for medical ([BillScreen.tsx:455-461, 547](frontend/src/screens/BillScreen.tsx))
  - the serial picker for serialised units ([BillScreen.tsx:457-460, 1268, 1525-1536](frontend/src/screens/BillScreen.tsx))
  - the tinted line ([BillScreen.tsx:480](frontend/src/screens/BillScreen.tsx))
  - the clothes 18% note above ₹2,500 ([BillScreen.tsx:979](frontend/src/screens/BillScreen.tsx))
  - the building-materials check at [BillScreen.tsx:798](frontend/src/screens/BillScreen.tsx)

### Inferences
- A per-trade menu can key off `shopTypes(settings)`. A shop with extra types needs either a merged tree or a trade switcher. `PickItemsScreen` already shows trade chips when the shop has more than one trade ([PickItemsScreen.tsx:198-200](frontend/src/screens/PickItemsScreen.tsx)), which is a usable precedent.
- No central per-trade config exists today. A `TRADE_MENUS` registry (tree, filters, tile layout) would be new, but it can follow the existing `TRADE_TYPES` and `fullPageTrades` list pattern.

### Gaps
- The routing for every trade screen was not traced. Only Home, App side menu, ProductScreen, BillScreen and StoreApp were checked.

## 2. Master item lists per trade: files, sizes, categories and sub-categories

### Takeaway
Six trades have a CSV master list in `backend/src/GroceryKhata.Api/Catalogue/Data/`: kirana 958, beauty 952, medical 847, paint 337, hardware 319 and clothes 204 items. Jewellery has no master list. Electrical has only a 14-item starter list. Clothes is the only trade with a sub-category structure, written as "Dept · Category" inside one string. Categories are flat strings everywhere. Kirana's are Hindi-only and the rest are English-only.

### Cited Findings
- **Format.** Every CSV uses the columns `name,name_hi,category,unit,gst,hsn,packs`. Medical has no `packs` column. — [Catalogue/Data/*.csv headers](backend/src/GroceryKhata.Api/Catalogue/Data/)
- **Seeding.** `SeedListsAsync` loads exactly six resources: medical, kirana, paint, hardware, beauty and clothes. It adds only names not already present. — [MasterItems.cs:176-195](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
- **Starter rows first.** Before the CSVs, `SeedAsync` fills every non-kirana trade with no master rows from `StarterItems.For(type)` (category defaults to "Other"). Kirana is seeded from `CommonCatalogue`. — [MasterItems.cs:150-172](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
  - So the CSV trades' master lists also carry the starter rows, whose category names differ. Paint starters use "Interior", "Exterior", "Primer & putty" and "Enamel" ([StarterItems.cs:340-352](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)). The paint CSV uses "Interior emulsion", "Primer" and so on.
- **`MasterItem` entity.**
  - Fields: `Trade`, `Category` (a single string), `Name`, `NameHi`, `Unit`, `GstRate`, `HsnCode`, `CatalogueItemId`, `IsActive`, `PackSizes`, `ImageAt`, `ImageCredit`, `UpdatedAt` and `CreatedAt`.
  - It has no sub-category, sort order, node key or attribute columns.
  - Source: [MasterItems.cs:14-54](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)
  - A computed `Popular` flag (the "what a new shop starts with" set) exists in the DTO. — [MasterItems.cs:108, 288, 303-306](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)

**Kirana**, `kirana-master.csv`: 958 items, 18 Hindi-only categories:
- आटा, चावल, दाल 99
- तेल, घी 39
- चीनी, नमक, गुड़ 20
- मसाले 100
- सूखे मेवे 33
- अचार, सॉस, पापड़ 43
- इंस्टेंट, नाश्ता 54
- बिस्कुट, नमकीन 76
- डेयरी, ब्रेड 41
- चाय, कॉफ़ी, दूध 49
- बेबी केयर 12
- चॉकलेट, टॉफ़ी 33
- पेय 39
- दांतों की देखभाल 43
- पर्सनल केयर 120
- साफ़-सफ़ाई 89
- घर का सामान 37
- पूजा का सामान 31

Source: [Catalogue/Data/kirana-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/kirana-master.csv)

**Beauty**, `beauty-master.csv`: 952 items, 12 categories:
- Bath, body and personal care 63
- Facial, bleach and clean-up 71
- Fragrance and talc 62
- Hair care 163
- Hair colour and henna 22
- Makeup 173
- Men's grooming 47
- Nails and mani-pedi 53
- Other 11
- Salon consumables, bridal and tools 75
- Salon professional hair 56
- Skin care 156

Source: [Catalogue/Data/beauty-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/beauty-master.csv)

**Medical**, `medical-master.csv`: 847 items, 24 categories:
- Fever & pain 62
- Antibiotics & infections 45
- Stomach & digestion 59
- Cold, cough & allergy 48
- Breathing & respiratory 43
- Diabetes 31
- Heart & BP 35
- Thyroid, hormones & steroids 10
- Vitamins & supplements 35
- Skin care & creams 29
- First aid & wound care 44
- Eye, ear & mouth 17
- Women's health 31
- Kidney & urology 11
- Sexual wellness & condoms 20
- Brain & nerves 9
- Surgical & nursing care 41
- Diagnostic devices 51
- Ayurvedic & Unani 77
- Baby & mother care 52
- Nutrition & health drinks 17
- Personal care & hygiene 14
- Supports & mobility 58
- Hot, cold & water therapy 8

The file has no schedule, salt or pack column. Source: [Catalogue/Data/medical-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/medical-master.csv)

**Paint**, `paint-master.csv`: 337 items, 13 categories:
- Adhesive and sealant 10
- Brushes, rollers and tools 50
- Distemper 11
- Enamel and metal paint 33
- Exterior emulsion 45
- Interior emulsion 64
- Other 7
- Primer 29
- Putty and texture 26
- Stainer and thinner 9
- Waterproofing 30
- White cement 2
- Wood finish and polish 21

Source: [Catalogue/Data/paint-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/paint-master.csv)

**Hardware**, `hardware-master.csv`: 319 items, 14 categories:
- Cement 6
- Chain, rope and welding 1
- Door and furniture fittings 4
- Doors and windows 40
- Farm tools 8
- Fasteners 44
- Hand tools 58
- Locks 15
- Other 2
- Pipes and fittings 71
- Power tools and accessories 19
- Roofing sheets 14
- Sariya and steel 21
- Wire and mesh 16

Source: [Catalogue/Data/hardware-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/hardware-master.csv)

**Clothes**, `clothes-master.csv`: 204 items, 20 categories. The only CSV with a two-level "Department · Category" naming convention.
- Women: Sarees 11, Ethnic wear 16, Bottoms 11, Western wear 11, Innerwear 14, Nightwear 10
- Men: Shirts and T-shirts 12, Bottoms 12, Ethnic wear 7, Formal and party wear 4, Innerwear 8, Nightwear 3
- Kids: Baby (0–2 years) 12, Girls 12, Boys 11, School uniform 6
- With no department prefix: Winter wear 21, Accessories 13, Sportswear 5, Fabric (by metre) 5
- In clothes, `packs` holds sizes (e.g. "Free size"), and `IsWearSize` treats clothes pack values as sizes. — [Catalogue/Data/clothes-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/clothes-master.csv); [MasterItems.cs:413-414](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs)

**Electrical**: there is no CSV. The master list is the 14 starter rows in 6 categories (Wires, Switches, Protection, Lights, Fans, Fittings). — [StarterItems.cs:322-338](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)

**Sales & service**: 21 starter rows in 8 categories (Pumps, Motors, Inverters & batteries, Fans, ACs, Solar, Spare parts, Service), all serial-tracked with warranty. — [StarterItems.cs:33-66](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)

**Jewellery**: `StarterItems.For` has no Jewellery branch, so it returns `[]` ([StarterItems.cs:12-30](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)). There is no CSV. Stock is tagged `JewelleryPiece` rows with a free-text `Category`. — [Ledger/Jewellery.cs:36-81](backend/src/GroceryKhata.Api/Ledger/Jewellery.cs); [JewelleryScreen.tsx:189-190, 280](frontend/src/screens/JewelleryScreen.tsx)

**The existing category tree (`Aisles`)**: one global tree of 4 sections and 16 aisles with codes and Hindi and English names. It is described as "the same for every shop" and is served at `GET /api/v1/catalogue/aisles`. — [CommonCatalogue.cs:28-58](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs); [CatalogueEndpoints.cs:25](backend/src/GroceryKhata.Api/Catalogue/CatalogueEndpoints.cs); [lib/aisles.ts:5-7](frontend/src/lib/aisles.ts)
- Gujarati names live in [lib/aisleNames.ts](frontend/src/lib/aisleNames.ts).
- Tiles render in `AisleTiles` ([components/AisleTiles.tsx](frontend/src/components/AisleTiles.tsx)).

**The shop's own `Category` table**: `ParentId` gives one parent level ("Grocery › Pulses"). It also has `SortOrder`, default GST, HSN and unit, `TargetCoverDays` and `SlowAfterDays`. It has no icon, translated names or trade key. — [Categories.cs:15-45](backend/src/GroceryKhata.Api/Catalogue/Categories.cs); frontend in [lib/categories.ts](frontend/src/lib/categories.ts)

### Inferences
- The Aisles tree is kirana-only in content even though its comment says it serves every shop. A per-trade tree needs a `trade` dimension on the tree definition. It could be served as `/catalogue/aisles?trade=` or one tree per trade.
- Clothes' "Women · Sarees" string already encodes department → category. Splitting on " · " gives a two-level tree without changing the data.
- Medical's 24 flat categories need grouping into departments to fit a rail of about 7-10 departments: Medicines, Ayurveda, Devices and supports, Mother and baby, Wellness and personal care. Beauty's 12 categories and kirana's 18 also need departments.
- Mixed starter and CSV category names (paint "Interior" vs "Interior emulsion"; any trade's starter rows falling into "Other") would show as duplicate tiles unless they are mapped to tree nodes.
- Hardware has some very thin categories ("Chain, rope and welding" 1, "Other" 2, "Door and furniture fittings" 4). These could merge in a menu.

### Gaps
- Live DB state was not checked. Admin edits and suggestions may have changed master categories since seeding.
- The starter rows for clothes, beauty, hardware and medical were not counted individually.

## 3. Item attributes per trade that could become filters

### Takeaway
Filter-ready attributes already exist for:
- medicine schedule, strip size, batch, expiry and MRP
- jewellery metal, purity, weight, HUID, making type and tray
- clothes style, size and colour
- serial and warranty units
- paint shade, but only on bill lines

Not stored anywhere: medicine salt or composition, rack or shelf location, paint base, hardware size, electrical wattage, and beauty shade. Some of these appear only inside item names (for example "Wire 1.5 sq mm", "LED bulb 9 W").

### Cited Findings
- **`Product` fields** in [Catalogue/Product.cs](backend/src/GroceryKhata.Api/Catalogue/Product.cs):
  - `Category` (string copy) at line 12; `CategoryId` at 15; `CatalogueItemId` at 21
  - `PackSize` at 27; `BagSize` at 34; `PurchaseUnit` at 37; `WeightKg` at 40; `PackUnit` at 42; `IsDefaultOption` at 48
  - `Unit` at 53; `SellingPrice` at 56; `GstRate` at 59; `HsnCode` at 62; `CostPrice` at 64
  - `Stock` at 71; `Reserved` at 77; `ReorderLevel` at 80; `Uncounted` at 86; `OutOn` at 89; `IsActive` at 95
  - `IsService` at 98; `DurationMinutes` at 101
  - `Schedule` (DrugSchedule) at 104; `UnitsPerPack` (tablets per strip) at 107
  - `TracksSerials` at 110; `WarrantyMonths` at 113
  - `Mrp` at 116; `WholesalePrice` and `WholesaleMinQty` at 122-124
  - `BillName` at 131
  - `StyleName`, `Size` and `Colour` at 138-142
  - `Barcode` and `ExtraBarcodes` at 145-151
  - `MasterItemId` at 168
- **`DrugSchedule` enum**: G, H, H1 and X. Null means over the counter. — [Product.cs:175-185](backend/src/GroceryKhata.Api/Catalogue/Product.cs)
- **Medicine batches** (`ProductBatch`): `BatchNo`, `Expiry` (the last day of the expiry month), `Mrp`, `CostPrice`, `Quantity` and `ReceivedAt`. The bill uses FEFO and the lowest in-date batch MRP. — [Catalogue/Batches.cs:15-34, 145-146](backend/src/GroceryKhata.Api/Catalogue/Batches.cs)
- **Medicine salt or composition**: no field. A case-insensitive search for `salt|composition|generic` in the backend found only GST composition-scheme code. The pharmacy plan says the item master *needs* "salt/composition, manufacturer … cold-chain flag and rack location". — [reports/Medical shop pharmacy plan.md:15](reports/Medical%20shop%20pharmacy%20plan.md)
- **Rack or shelf location**: no field. The search for `\brack\b` in the backend found nothing. `Godown` has only `Name`, `Address` and `IsActive`. — [Catalogue/Godowns.cs:15-24](backend/src/GroceryKhata.Api/Catalogue/Godowns.cs)
- **Jewellery** (`JewelleryPiece`, [Ledger/Jewellery.cs:36-81](backend/src/GroceryKhata.Api/Ledger/Jewellery.cs)):
  - `TagNo`, `Name`, `Category` (free text), `Metal`, `Purity`
  - `Gross` grams, `Stone` grams, `WastagePercent`, `MakingType` (per_gram, percent or per_piece), `MakingValue`
  - `StoneValue`, `HallmarkCharge`, `Huid`, `FixedPrice`, `SoldBillId`, `SoldAt` and `Tray`
  - Stones are a separate `PieceStone` entity. — [Ledger/JewelleryWorkshop.cs:95](backend/src/GroceryKhata.Api/Ledger/JewelleryWorkshop.cs)
  - The frontend `Metal` is 'gold' or 'silver'. Gold purities are 999, 995, 916, 750 and 585; silver purities are 999 to 800. Labels: 916 → "22K". — [frontend/src/lib/jewellery.ts:3-8, 56-59](frontend/src/lib/jewellery.ts)
- **Clothes styles**: `lib/styles.ts` provides:
  - `groupStyles()`, which groups products by `styleName`
  - `compareSizes()`, which orders sizes in this sequence: XXS→5XL, then kids' ages (0-3M before 3-6M), then numbers (28, 30, 32B), then anything else
  - `sortVariants()` and `variantLabel()` ("32 · Blue")
  - Source: [frontend/src/lib/styles.ts:1-57](frontend/src/lib/styles.ts)
  - Related: `SizeReportScreen.tsx` exists, and `BillName` gives discreet names for innerwear ([Product.cs:126-131](backend/src/GroceryKhata.Api/Catalogue/Product.cs); `Catalogue/BillNames.cs`).
- **Paint**:
  - The shade lives on the bill line, not the product. A tinted line is named "base · shade CODE", and `ShadeHistoryDto(ProductId, Name, Shade, UnitPrice, Quantity, BoughtAt)` serves "repeat last shade". — [Ledger/Trades.cs:80-81, 605-616](backend/src/GroceryKhata.Api/Ledger/Trades.cs); [TradeBill.tsx:96, 145-154](frontend/src/screens/TradeBill.tsx)
  - Paint has no base, finish or brand-tier field. Pack sizes become separate products through master `PackSizes` (e.g. 1/4/10/20 L).
- **Serial-tracked units** (`Serial`): `SerialNo`, `Status`, `ReceivedOn`, `BillId`, `CustomerId`, `SoldOn` and `WarrantyUntil`. — [Catalogue/Serials.cs:15-34](backend/src/GroceryKhata.Api/Catalogue/Serials.cs)
  - Electrical starter items are not serial-tracked. Only sales and service units use the `Unit1` helper with `TracksSerials: true`. — [StarterItems.cs:33-35, 322-338](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)
- **Wattage and size**: no field. These exist only inside names such as "LED bulb 9 W", "Wire 2.5 sq mm (90 m coil)" and "PVC conduit pipe 20 mm". — [StarterItems.cs:322-338](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs)
- **Beauty shade**: no field. The beauty report assumed "shade is a variant, not a row". — [reports/Beauty shop master item list.md:56](reports/Beauty%20shop%20master%20item%20list.md)
  - `StyleName`, `Size` and `Colour` could carry a shade as `Colour`, but nothing in the code uses them for beauty. That is my inference.
- **Beauty services**: `IsService` and `DurationMinutes` mark salon services. — [Product.cs:98-101](backend/src/GroceryKhata.Api/Catalogue/Product.cs); [StoreApp.tsx:350](frontend/src/screens/store/StoreApp.tsx)
- **Filters that exist today**:
  - The store filter drawer has sort (relevance, low, high), in-stock and max ₹. — [StoreApp.tsx:218, 473-484](frontend/src/screens/store/StoreApp.tsx)
  - The bill screen has no filters, only search plus the top 12 or 30 tiles. — [BillScreen.tsx:55-68](frontend/src/screens/BillScreen.tsx)

### Inferences
Candidate per-trade filters from existing fields, with no schema change:

| Trade | Filters available now |
|---|---|
| Medical | Schedule (OTC/G/H/H1/X); expiring soon, from batch `Expiry`; in stock; loose strip (`UnitsPerPack`) |
| Jewellery | Metal; purity/karat; weight bands (`Gross`); hallmarked (`Huid` set); making type; tray; fixed price vs by weight; unsold |
| Clothes | Size (ordered by `compareSizes`); colour; style; price band; 5%/18% GST band at ₹2,500 |
| Sales & service | Serialised; under warranty |
| Paint | Pack size; shade history per customer |
| All trades | Low stock (`ReorderLevel`), offer (`Mrp` > price), wholesale price |

Fields that would need new storage:
- medicine salt, manufacturer, rack location and cold-chain flag
- paint base and finish
- hardware size and material
- electrical wattage and brand
- beauty shade

### Gaps
- I did not check whether a `brand` field exists anywhere. None is listed on `Product`, so brand facets would need parsing from names or a new column.

## 4. What earlier reports say about categories, finding items and filters, per trade

### Takeaway
Each trade report already proposes a category shape, and several propose trade-specific finding aids:
- medical: therapy categories plus body-part devices, and rack locations on the bill
- clothes: a department → category tree with tags
- paint: finish type as category and brand tier as line
- beauty: 11 groups with shade as a variant
- jewellery: no category plan, focus elsewhere
- sales and service: serial-led

### Cited Findings
- **Medical shop master item list.**
  - Proposes about 700 rows in four groups: allopathic, Ayurvedic and wellness, devices and surgical, plus water and heat therapy. — [reports/Medical shop master item list.md:3](reports/Medical%20shop%20master%20item%20list.md)
  - About twenty therapy categories for medicines. — [same file:9-11](reports/Medical%20shop%20master%20item%20list.md)
  - Devices "indexed by body area, which also gives shopkeepers a natural browse path". — [same file:44](reports/Medical%20shop%20master%20item%20list.md)
  - Sexual wellness as its own category. — [same file:40](reports/Medical%20shop%20master%20item%20list.md)
  - Recommends "a generic item plus brand and pack variants". Supports should be one generic item with brand and size (S-XXL) as variants. — [same file:3, 44](reports/Medical%20shop%20master%20item%20list.md)
- **Medicine rack and shelf locations.**
  - Proposes a free-form per-shop location code ("A-R3-S2-B14", "Fridge") built from optional almirah, rack, shelf and box parts.
  - Special zones: Fridge, Schedule X locked cupboard, Expiry/Return shelf and Back store.
  - The location shows next to the name on the billing screen and on pick lists and stock-count sheets sorted by rack. It comes in through Excel import.
  - Chemists group stock by manufacturer, by dosage form or by a mix.
  - Source: [reports/Medicine rack and shelf locations.md:3, 7-13, 26-28](reports/Medicine%20rack%20and%20shelf%20locations.md)
- **Medical shop pharmacy plan.** The item master needs a schedule flag, salt/composition, manufacturer, HSN, GST, pack size, cold-chain flag and rack location. — [reports/Medical shop pharmacy plan.md:15](reports/Medical%20shop%20pharmacy%20plan.md)
- **Hardware and paint master list.**
  - "category = finish type, line = brand tier", with about ten paint categories. — [reports/Hardware and paint master list.md:7](reports/Hardware%20and%20paint%20master%20list.md)
  - Each row is a product line with a pack ladder, not one row per pack, because tinted shades share a base price. — [same file:3](reports/Hardware%20and%20paint%20master%20list.md)
  - Hardware falls into "five clusters". — [same file:50](reports/Hardware%20and%20paint%20master%20list.md)
  - Every row should carry aliases, brand, line, category, pack ladder and so on. — [same file:84](reports/Hardware%20and%20paint%20master%20list.md)
- **Hardware and paint shop strategy.**
  - "Paint shops need shade memory". Backlog items include shade history searchable by customer, phone or site; brand-wide % rate change; and hardware reports including "cycle count by shelf". — [reports/Hardware and paint shop strategy.md:28-32, 50-61](reports/Hardware%20and%20paint%20shop%20strategy.md)
  - The report also notes 337 paint and 319 hardware master rows already exist. — [same file:3](reports/Hardware%20and%20paint%20shop%20strategy.md)
- **Clothing shop management plan.**
  - "A short tree and rich tags, not a deep tree". Tags cover occasion, season and brand.
  - Department table:
    - Women: Ethnic, Western, Innerwear, Nightwear, Winter
    - Men: Topwear, Bottomwear, Ethnic, Innerwear, Winter
    - Kids: Baby 0-2, Girls, Boys, Kids ethnic
  - "One size scale per category, not one global list" (e.g. bra band × cup).
  - Item-level attributes: fabric, pattern and construction (knitted or woven, which drives HSN 61 vs 62).
  - Source: [reports/Clothing shop management plan.md:5-17](reports/Clothing%20shop%20management%20plan.md)
  - Its intro says clothes had "no size or colour fields". The code now has `StyleName`, `Size` and `Colour` ([Product.cs:133-142](backend/src/GroceryKhata.Api/Catalogue/Product.cs)) and a 204-row CSV, so that part of the report is out of date.
- **Beauty shop master item list.**
  - About 800 rows in 11 groups. The salon backbar (professional hair and salon consumables) is kept separate from retail.
  - Shade is treated as a variant, not a row. Sachets and strips are their own rows. — [reports/Beauty shop master item list.md:3, 38-56](reports/Beauty%20shop%20master%20item%20list.md)
  - The CSV ended up with 952 rows in 12 categories, the 12th being "Other".
- **Jewellery winning strategy status.**
  - Covers HUID/QR, girvi, schemes, rate cards and scale. It does not mention categories, browse or filters. — [reports/Jewellery winning strategy status.md:3, 13-25](reports/Jewellery%20winning%20strategy%20status.md)
  - Trays exist for counts. — [JewelleryScreen.tsx:51, 83](frontend/src/screens/JewelleryScreen.tsx)
- **Electrical sales and service shop.**
  - The backbone is the serial number. Stock is "whole units" (serial-captured) versus "loose spares" counted by quantity or kg.
  - No category or browse plan. — [reports/Electrical sales and service shop.md:3, 9](reports/Electrical%20sales%20and%20service%20shop.md)
  - It covers the `SalesService` type, not the `Electrical` wires-and-switches type.
- **Category menu for item ordering**: see section 5.

### Inferences
Per-trade "finding" aids that the menu could carry:

| Trade | Finding aids |
|---|---|
| Medical | Rack location badge; search by salt (needs a field); Schedule chip |
| Paint | Shade lookup by customer |
| Clothes | Size and colour chips plus occasion and season tags |
| Jewellery | Tray and tag-number scan |
| Sales & service | Serial-number search |

### Gaps
- No report covers a category tree for the `Electrical` (wires and switches) trade or for jewellery browsing.
- "Other shop types and expense notebook.md" and "Master list picker redesign.md" were not read in detail.

## 5. Where "Category menu for item ordering" assumed kirana only, and what must change

### Takeaway
The earlier plan is kirana throughout. It describes one bilingual kirana tree of 7 departments and about 20 categories, a `node` column added to `kirana-master.csv` only, kirana keyword rules, and kirana-only filters (Veg, brands, pack sizes). Its component design (rail, tiles, listing, chips, tray, job-specific filter chips) carries over to every trade. The tree, node mapping, filters, tile art, listing row layout and search synonyms must all become per trade.

### Cited Findings
- **Opening claim**: "one bilingual kirana tree with three levels: about 7 departments … 3–5 category tiles under each (Tea, Dal, Dry fruits, Noodles, Fast food and Masala…)". — [reports/Category menu for item ordering.md:3](reports/Category%20menu%20for%20item%20ordering.md)
- **Vocabularies**: it identified three — 16 aisles, 18 Hindi-only master categories and the shop's own table — "none of which covers the whole kirana list". — [same file:3, 25](reports/Category%20menu%20for%20item%20ordering.md)
- **The tree table** is entirely kirana: ★ रोज़ के, रोज़ का राशन, मसाले और मेवे, चाय-कॉफ़ी-पेय, नूडल्स-फ़ास्ट फ़ूड-स्नैक्स, दूध-ब्रेड, घर और देखभाल, पूजा और बच्चे. — [same file:29-44](reports/Category%20menu%20for%20item%20ordering.md)
- **Build step 1**: "Extend `Aisles` into department → category → sub-category … serve it from the existing `GET /api/v1/catalogue/aisles`". This is a single global tree. — [same file:94](reports/Category%20menu%20for%20item%20ordering.md)
- **Build step 2**: add a `node` column to `kirana-master.csv` with keyword rules ("dal", "चाय", "noodles") and a manual pass over 958 rows. Add `MasterItem.Node` and `Category.TreeKey`. — [same file:27, 95](reports/Category%20menu%20for%20item%20ordering.md)
- **Filters** per screen: Bill (in stock, offer, brands, pack size); Supplier order (low stock, this supplier, last ordered); Store (in stock, offer, Veg, bought before); List/picker. It notes the store filter drawer is hidden for kirana and that Veg needs a CSV column. — [same file:75-82](reports/Category%20menu%20for%20item%20ordering.md)
- **Listing layout**: dense rows for bill and supplier screens, and 2-column photo cards for the store. — [same file:52](reports/Category%20menu%20for%20item%20ordering.md)
- **Search**: `rank` in `lib/search.ts` holds a Hindi, Gujarati and Hinglish synonym table oriented to groceries (doodh→milk, haldi→turmeric). — [same file:69](reports/Category%20menu%20for%20item%20ordering.md); [frontend/src/lib/search.ts](frontend/src/lib/search.ts)
- **Effort**: estimated at 10–14 developer days for kirana. Steps 1–4 take about 7 days. — [same file:3, 103](reports/Category%20menu%20for%20item%20ordering.md)
- **Picker precedent**: `PickItemsScreen` already works per trade. It builds categories per `activeTrade`, shows a desktop rail of categories with counts and a "★ Popular" entry, and adds trade chips when the shop has more than one trade. Its search hint is taken from the trade's own items "so a clothes shop isn't told to search for aata". — [PickItemsScreen.tsx:53-93, 164-206](frontend/src/screens/PickItemsScreen.tsx)

### Inferences
What must change to make the plan per trade:
1. **The tree becomes per trade.** `Aisles` (or a new `TradeTree`) is keyed by trade. Serve it as `/catalogue/aisles?trade=x` or include the trade on every node. Kirana's 7-department tree stays as designed. Other trades need their own department groupings:
   - Clothes: Women, Men, Kids, plus Winter, Accessories and Fabric, derived from the " · " prefix.
   - Medical: Medicines by therapy; Ayurveda; Devices by body part; Mother and baby; Wellness and personal care.
   - Paint: finish types such as Interior, Exterior, Enamel and wood, Primer and putty, Waterproofing, Tools.
   - Hardware: the report's five clusters.
   - Beauty: Retail vs salon backbar.
   - Electrical: Wires, Switches and protection, Lights, Fans, Fittings.
   - Jewellery: Gold, Silver, Diamond/stone, then ornament type.
2. **The node column applies to every CSV**, not just kirana. Clothes can be auto-mapped from its prefix. Medical, beauty, paint and hardware need keyword rules per trade. Electrical and jewellery have no CSV, so they need either a CSV or a tree defined straight from the starter rows and `JewelleryPiece.Category`.
3. **Jewellery does not fit the Product-based menu.** Its stock is `JewelleryPiece` rows (one tag each) and its billing runs inside `JewelleryScreen`, not `BillScreen`. The menu would need a jewellery adapter (pieces as items) or a separate data source.
4. **Filters become a per-trade × per-job matrix.** The plan's kirana filters (Veg, brand, pack size) are replaced per trade with the attributes listed in section 3. Size and colour for clothes are already sortable via `compareSizes`.
5. **Listing rows differ by trade.**
   - Clothes: a style row opening a size × colour picker. The bill already groups styles ([BillScreen.tsx:66-68, 813](frontend/src/screens/BillScreen.tsx)).
   - Medical: batch and expiry plus a schedule badge, and a rack badge later.
   - Jewellery: weight, purity and HUID.
   - Serialised units: a serial picker.
   - Paint: base tin → shade entry.
   - The store's `fullPageTrades` already marks trades that need richer product pages.
6. **Search synonyms and tile art need per-trade sets.** `AISLE_ART` in `itemArt.ts` and the grocery `SYNONYMS` are kirana-specific.
7. **Names and languages differ.** Kirana categories are Hindi-only and every other CSV is English-only. The tree's three-script node names fix both, but the non-kirana trades need Hindi and Gujarati names written.
8. **Multi-trade shops** (main type plus `ExtraTypes`) need either merged trees with trade departments in the rail, or a trade switch at the top of the rail as `PickItemsScreen` does.

### Gaps
- Effort for the per-trade extension was not estimated in any report.
- How many live shop products lack `MasterItemId` per trade is unknown, because no database was queried.
