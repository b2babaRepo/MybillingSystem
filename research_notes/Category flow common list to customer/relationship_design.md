# Per-trade category tree: master list → shop items → customer store (relationship design)

Scope: entities, keys and rules for one category tree per trade, used by the master (common) list, a shop's items and the customer store. Based on the repo as of 2026-11-27 (no code changed) and on the two earlier reports. Repo facts cite files. Counts come from read-only Python over the seven master CSVs. Web sources are for general patterns only. Note: www.postgresql.org could not be reached from this session (the network blocks it), so the ltree facts below come from Neon's docs and from search summaries.

**Starting point in the code (the facts every option depends on):**
- `MasterItem` is global and has no RLS. Its fields include `int Id`, `Trade` (string), free-text `Category` (max 60 characters, "Other" if blank), `Name`/`NameHi`, `PackSizes List<string>`, `CatalogueItemId`, `IsActive` (never deleted), `ImageAt`, `UpdatedAt`/`CreatedAt` — [MasterItems.cs:14-54](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). `MasterSuggestion` is also global, carrying `ShopName` and having no RLS (lines 60-86).
- `Product` is shop-owned (`ITenantOwned`, `ShopId`). It has free-text `Category`, a copy of the shop category's name kept in step on rename or merge, plus `CategoryId → Category`, `MasterItemId → MasterItem`, `CatalogueItemId`, `StyleName/Size/Colour`, `PackSize/PackUnit`, `IsActive`, `SellingPrice` (0 = "not set"), `Stock/Reserved`, `Uncounted` and `OutOn`. `OnSale(today)` = not marked sold out today && (uncounted || stock − reserved > 0). There is **no per-product "show online" flag** — [Product.cs:5-172](backend/src/GroceryKhata.Api/Catalogue/Product.cs).
- **A shop-owned `Category` table already exists.** Its fields are `ShopId`, `Name`, `NormalisedName` (unique per shop among unmerged rows), `ParentId` (**at most two levels**, enforced by `ParentProblemAsync`), `SortOrder`, `IsActive`, `MergedIntoId` (a merged row stays as a pointer for offline phones), and the defaults `DefaultGstRate/DefaultHsn/DefaultUnit/TargetCoverDays/SlowAfterDays`. Typing a new name creates one (`ResolveAsync`, an `INSERT ... ON CONFLICT DO NOTHING` that takes the shop from `current_setting('app.shop_id')`). Endpoints: `GET/POST /api/v1/categories`, `PUT /{id}`, `POST /{id}/merge`, `POST /order` — [Categories.cs:15-276](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). Every product create, update or import calls `Categories.LinkAsync` — [StockService.cs:147, 212, 484](backend/src/GroceryKhata.Api/Catalogue/StockService.cs).
- Picking from the master list: `POST /api/v1/products/from-master` → `AddToShopAsync` builds one `ImportRow` per pack size. Each row carries the name, plus the size for pack items, `Category = master.Category` and `MasterItemId = master.Id`. Clothes sizes become `StyleName + Size`; grocery pack sizes stay separate items. The name is Hindi when the owner's language is Hindi — [MasterItems.cs:336-360](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs). `LinkByNameAsync` back-fills `MasterItemId` by name for older items (line 502).
- Seeding: `SeedListsAsync` reads seven embedded CSVs (`name,name_hi,category,unit,gst,hsn,packs`). It only adds new names and never overwrites an admin's edit, and it re-homes rows still in old categories through `StarterGroups` (lines 178-275).
- There is a global grocery-only tree in code: `Aisles` (16 aisles in 4 sections, keys such as `atta-rice` and `dal`, Hindi and English names), used by `CatalogueItem.Aisle` — [CommonCatalogue.cs:1-60](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs).
- RLS helper: `migrationBuilder.EnableShopIsolation(tables)` runs ENABLE + FORCE ROW LEVEL SECURITY and creates `CREATE POLICY shop_isolation USING/WITH CHECK (shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid)` — [MigrationExtensions.cs](backend/src/GroceryKhata.Api/Persistence/MigrationExtensions.cs). It has been used in 211 migrations, for example `jewellery_pieces` and `metal_rates` in [20261120092811_Jewellery.cs:195-203](backend/src/GroceryKhata.Api/Persistence/Migrations/20261120092811_Jewellery.cs).
- Store: `GET /api/v1/store/{slug}` returns products where `IsActive && SellingPrice > 0`, ordered by `Category` then `Name`. Available stock is 0 if sold out today, a special "uncounted" value, or `Stock − Reserved`. The photo falls back to `MasterItem.ImageAt`, so the master link is already joined at read time. Jewellery pieces are a separate list gated by `Shop.JewelleryShowOnline` — [StoreEndpoints.cs:80-140](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs).
- Multi-trade shops: `Shop.Type` + `string[] ExtraTypes` — [Shop.cs:157](backend/src/GroceryKhata.Api/Shops/Shop.cs). There are 19 `ShopType` values (Shop.cs:196-217). Only 7 have master CSVs. The rest have only `StarterItems` (6–22 rows each), and Dairy and Other have none — [StarterItems.cs:12-30](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs).
- The earlier reports already settled on these: a `node` column in every CSV, `MasterItem.Node`, `Category.TreeKey`, and the resolution order "master link → category key → name rule → Other". They also settled on a trade switch (not a merged tree) for multi-trade shops, `TRADE_MENUS[trade]` with `rowKind`, multi-parent nodes for medical (Diabetes under Chronic and Ayurveda), and shared type keys for clothes (jeans under Men/Women/Girls/Boys) — [Category menu for all shop types.md](reports/Category%20menu%20for%20all%20shop%20types.md); [Category menu for item ordering.md](reports/Category%20menu%20for%20item%20ordering.md). The hardware/paint/water report adds a separate Water & plumbing department, electrical's 18 groups in 9 rail entries, and beauty's Women | Men | Kids & baby | Salon top switch with a "for" field — [Hardware paint and water menu.md](reports/Hardware%20paint%20and%20water%20menu.md).

## Category tree tables: global CategoryNode vs free text; levels; where the user's lists fit

### Takeaway
Add one **global `category_nodes` table** (no RLS, like `master_items`) with a stable text key per node. Store it as an **adjacency list (`parent_id`) plus a cached path**, and cap it at **three levels: department (rail) → group (tile) → type (chip)**. Keep free-text `Category` and the shop `Category` table as the shop's own vocabulary, and map them onto nodes rather than replacing them. Every list the user gave fits within three levels. Beauty's Women/Men/Kids/Salon and clothes' age bands are the two places where facet-or-tree is a real choice (see the next section).

### Cited Findings
- Today, the master list's categories are free text. Across the seven CSVs there are 3,940 rows in 120 category strings: beauty 1,017/13, clothes 204/20, electrical 258/18, hardware 319/14, kirana 958/18, medical 847/24, paint 337/13 (Python count over [Catalogue/Data/*.csv](backend/src/GroceryKhata.Api/Catalogue/Data/)). Kirana's are Hindi-only merged strings ("आटा, चावल, दाल" 99 rows, "चाय, कॉफ़ी, दूध" 49). Only clothes encodes two levels, as "Women · Sarees" (160 of 204 rows; the 44 flat ones are Winter wear 21, Accessories 13, Sportswear 5 and Fabric 5).
- The shop `Category` table already supports two levels and has hide (`IsActive`), sort (`SortOrder`), rename-with-cascade, and merge with a kept pointer — [Categories.cs](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). The global grocery tree `Aisles` is two levels and lives in code — [CommonCatalogue.cs](backend/src/GroceryKhata.Api/Catalogue/CommonCatalogue.cs).
- Trade-offs between tree models: ltree is efficient for read-heavy trees, and GiST indexes speed up ancestor/descendant lookups — [Neon ltree docs](https://neon.com/docs/extensions/ltree). An adjacency list needs only the parent reference, while ltree needs the full path recalculated, which makes moves dearer — [DEV comparison](https://dev.to/dowerdev/implementing-hierarchical-data-structures-in-postgresql-ltree-vs-adjacency-list-vs-closure-table-2jpb). `WITH RECURSIVE` in PostgreSQL is efficient, and a blended design keeps an ltree path column materialised from the recursive query — [Cybertec](https://www.cybertec-postgresql.com/en/?p=18978). Npgsql's EF Core provider has an `LTree` type that converts implicitly to a string — [Npgsql API](https://www.npgsql.org/efcore/api/Microsoft.EntityFrameworkCore.LTree.html). ltree binary support needs PostgreSQL 13 or later, per a maintainer reply in a GitHub issue reported second-hand — [Lightrun mirror of npgsql issue](https://lightrun.com/answers/npgsql-efcore-pg-unable-to-use-ltree-type-in-the-entity).
- The user's lists, with counts from the reports:
  - **Hardware**: 7 rail entries. Water & plumbing has 7 tiles (pipes, fittings, valves, taps & bathroom, drainage & traps, solvent & Teflon, tanks & pumps). The others are Fasteners, Door & furniture, Tools, Adhesives & sealants, Safety and Building material.
  - **Paint**: 6 rail entries.
  - **Electrical**: 18 CSV groups under 9 rail entries.
  - **Beauty**: a Women/Men/Kids & baby/Salon switch over area rails (Face, Body, Hair, Makeup, Lip & nail; Shaving & beard for Men).
  - **Clothes**: departments Women, Men, Kids Girls/Boys, Baby girl/boy, School, Winter, Accessories and Fabric, each with group tiles and type chips.
  - **Kirana**: 7 departments and about 20 tiles. **Medical**: 9 departments over 24 groups. **Jewellery**: 8 metal departments × body-part tiles × style chips.
  - Sources: [Hardware paint and water menu.md](reports/Hardware%20paint%20and%20water%20menu.md); [Category menu for all shop types.md](reports/Category%20menu%20for%20all%20shop%20types.md).

### Inferences
- **Proposed entity `CategoryNode`** (table `category_nodes`, global, no RLS, never deleted; mirrors `MasterItem`):
  - `Id int` PK (identity, used for FKs).
  - `Key text` UNIQUE: stable, ASCII and lower case, for example `hardware/water/valves` or `clothes/women/bottoms/jeans`. This is what CSVs, the frontend registry and synonym packs refer to. **It is an identity, not a live path**: if a node later moves, its key stays and only `ParentId` changes. Otherwise every CSV, override and cached client would break.
  - `Trade text` (same strings as `MasterItem.Trade`, through `ShopTypes.Name`).
  - `ParentId int?` → category_nodes. `Level smallint` (1 department, 2 group, 3 type; CHECK ≤ 3).
  - `NameEn`, `NameHi`, `NameGu text?`, `Icon text?` (an SVG or emoji key from `itemArt.ts`), `SortOrder int`, `IsActive bool`.
  - `MergedIntoId int?` (same pattern as `Category.MergedIntoId`).
  - `TypeKey text?`: a shared cross-department type ("jeans") for search, filters and reports across Men/Women/Kids.
  - `LinkToId int?`: a "shortcut" tile that shows another node's items. This covers the medical "Diabetes under Chronic and Ayurveda" case without a full DAG.
  - `Aliases text[]`: Hinglish and Devanagari words and the old category strings, used for mapping and search.
  - `Path ltree` (optional): cached and recomputed on move. Skip it in v1, because the trees are tiny (estimated 40–150 nodes per trade, so a recursive CTE or an in-memory build is trivial).
- **Recommendation: adjacency list now, ltree later if ever.** With fewer than about 1,500 nodes across all trades and no per-shop branching of the global tree, the whole tree for a trade can be loaded and cached in memory (like `Aisles.All`), so subtree queries never reach SQL. Adding ltree means `CREATE EXTENSION ltree` plus migration and test-container setup, for little gain. Postgres arrays and identity keys are already used (`ExtraTypes string[]`, `ExtraBarcodes string[]`, `PackSizes List<string>`), so this follows house style.
- **Alternative A: keep free text plus a code-only registry** (extend `Aisles` into `TradeTrees` in C#, keyed by trade, and add `MasterItem.NodeKey string`). Cost is lowest (no table or admin UI), and it is the same pattern as `Aisles`. The downside is that every rename or new tile is a deploy, and Gujarati names live in code. This is acceptable as **phase 1**: seed `category_nodes` from a C# or CSV file at start-up exactly as `SeedListsAsync` does, so a table-backed admin editor can come later without a data change. *Recommended path: a table seeded from an embedded `category-nodes.csv` per trade, with the same "only add, never overwrite admin edits" rule as `SeedListsAsync`.*
- **Alternative B: per-shop trees only** (extend shop `Category`). This is rejected: the master list and store would then have no shared vocabulary, which is the "three vocabularies" problem the earlier report names.
- **Three levels is enough for every list given.** Clothes is the deepest (department "Women" → group "Bottoms" → type chip "Jeans"). Fit, fabric and similar are filters, not a 4th level. Trades with only starter items (building materials, water supply, sweets, toys, woodwork, banquet, restaurant, lodge, sales & service) get 1-level trees made from their starter categories (4–8 nodes each) plus an `other` node. Jewellery's nodes are referenced by `JewelleryPiece.Category` in a later adapter.
- Every trade gets one reserved `<trade>/other` node (level 1, sorted last), so "unmapped" is never null.

### Gaps
- Live database node counts and real shop category strings could not be checked: there was no DB access, and RLS stops cross-shop reads anyway.
- Gujarati names: no source or translation exists in the repo for any trade (grep found only Hindi in the CSVs, in kirana and clothes). Beauty, electrical, hardware, paint and medical CSVs have no Hindi names (`name_hi` blank on 100% of their rows).
- The PostgreSQL ltree primary doc page could not be fetched (host blocked).

## Master item ↔ node: primary node, extra nodes, facets and attributes

### Takeaway
Give each master item **exactly one primary node** (`NodeId`), plus an **optional small array of extra nodes** for "also shown under". Put cross-cutting qualities in a **JSONB `Attrs` column**, checked against **per-trade attribute definitions**. These qualities are: for (women/men/kids/baby/unisex), use (retail/salon), age band, sq mm, amps, watts, size in mm/inch, material, shade, skin type, concern, finish and pack. The rule of thumb: a **tree node is something a counter browses to and that has its own size scale or shelf**. A **facet is a property that cuts across many nodes or narrows within one.**

### Cited Findings
- Beauty data: in [beauty-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/beauty-master.csv), 53 item names contain a men word (men/man/beard/shave/razor/aftershave). 40 are in "Men's grooming" and 13 sit elsewhere (for example "Nivea Men Deodorant", "Fogg Body Spray Men", "Garnier Men Oil Clear Face Wash"). 22 rows are in "Baby and kids care". 209 rows are in the three salon groups (Salon consumables 78, Facial/bleach 75, Salon professional hair 56). About 145 rows look unisex (shampoo, soap, sunscreen, deo, talc, body wash, outside Men's grooming). Only 8 names say women/ladies. Regex counts, so treat them as rough.
- The hardware/paint/water report proposes beauty rules: Men = the Men's grooming group plus "men" in the name; Kids = the baby group; Salon = the salon groups; Women = everything else. It adds that an item can be pinned to more than one (shampoo under Women and Men) "once the item gets a 'for' field" — [Hardware paint and water menu.md](reports/Hardware%20paint%20and%20water%20menu.md).
- Electrical: sq mm, amps and watts are written in names ("Wire 1.5 sq mm (90 m coil)"). In the 258 electrical rows my regex found sq mm in 20 names, an amps pattern in 27, watts in 17 and mm in 13. Electrical GST is blank on all 258 rows ([electrical-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/electrical-master.csv)). In hardware, only 19 of 319 names carry a parseable size ([hardware-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/hardware-master.csv)). The earlier report says the electrical names "follow one pattern", so the chips can be filled by parsing until fields exist — [Hardware paint and water menu.md](reports/Hardware%20paint%20and%20water%20menu.md).
- Clothes sizes: 53 distinct size labels across the CSV (Free size, XS–3XL, waist 26–46, band × cup 30B–40C, 75–110, 0-3M, 3-6M…), stored in the `packs` column. When picked, each label becomes `Product.Size` under one `StyleName` — [clothes-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/clothes-master.csv); [MasterItems.cs:352-356](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs).
- Name collisions: 25 names appear in more than one trade's CSV (for example Dettol antiseptic liquid, Johnson's baby powder, Mysore Sandal soap), and `MasterItem` already says "an item sold in two trades has a row in each" — [MasterItems.cs:10-13](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs).
- Pattern support: JSONB with a GIN index is usually simpler and faster than EAV for varied product attributes. GIN is used only with containment (`@>`) queries, `jsonb_path_ops` is a smaller and faster index for containment only, and a B-tree expression index suits 1–3 fixed fields — [AiiDA 1.0 paper](https://arxiv.org/pdf/2003.12476) (EAV → JSONB: about 2 orders of magnitude less storage, about 75× faster attribute queries); [Postgres Weekly / "Replacing EAV with JSONB"](https://habr.com/en/articles/475178); [SitePoint](https://www.sitepoint.com/postgresql-jsonb-query-performance-indexing/). Caveat: these are blogs plus one scientific project, not a rigorous independent benchmark.

### Inferences
- **Proposed columns on `MasterItem`:**
  - `NodeId int?` (FK category_nodes, primary). Required for new rows; null only during migration.
  - `ExtraNodeIds int[]` (default `{}`, at most about 3, all in the same trade). An int array follows the house pattern. A join table `master_item_nodes(master_item_id, node_id, is_primary)` is the alternative if node counts or FKs on extras matter; it is costlier, but integrity is enforced.
  - `Attrs jsonb` (default `{}`), for example `{"for":["women","men"],"sqmm":1.5,"amps":16,"watts":9,"sizeMm":20,"material":"cpvc","use":"salon","skin":["oily"],"concern":["dandruff"],"finish":"matte"}`.
  - Keep `Category` text for one release as a fallback and audit trail, then make it a computed display copy of the node name (like `Product.Category` is for shop `Category`).
- **Tree vs facet decision per trade (recommendation):**

| Trade | Tree (node) | Facet / attribute (`Attrs`) | Reason |
|---|---|---|---|
| Beauty | Area: Face, Body, Hair, Makeup, Lip & nail, Shaving & beard, Baby bath & body, Fragrance; salon groups as their own department "Salon use" | `for` = women/men/kids/baby/unisex (multi) → drives the top 4-button switch; `use` = retail/salon; skin type, concern, shade, finish, SPF, ingredient | Most items are unisex. As a tree, Women/Men would duplicate about 145+ items; as a facet, one value per item drives the switch. Salon is a department *and* `use=salon`, because backbar stock is kept apart |
| Clothes | Department = gender/age (Women, Men, Kids-Girls, Kids-Boys, Baby girl, Baby boy, School, Winter, Accessories, Fabric) → group → type, with `TypeKey` shared (jeans) | Size or age band (already `Product.Size`), colour, fit, fabric, sleeve, neck, occasion, pattern, season, stitched | Gender/age changes the size scale and the shelf, so it is a tree here (the opposite of beauty). Age band is a size label, not a node |
| Hardware | Water & plumbing / Fasteners / Door & furniture / Tools / Adhesives & sealants / Safety / Building material → tiles → chips (PVC/CPVC/UPVC/GI) | Size (mm and inch pair), material, finish, length, SDR/class, unit of sale, hot/cold, brand | Pipe type can be a chip (level 3) because the counter asks for it first. Size is always a facet or ladder |
| Paint | Interior, Exterior, Enamel & wood, Primer & putty, Waterproofing, Tools & thinner | Brand, range tier, finish, surface, pack (from `PackSizes`), shade code (bill line, not item) | Shade stays on the bill line as today |
| Electrical | 9 rail entries → 18 groups → optional chips (house wire / flexible) | sqmm, amps, poles, curve, ways, watts, light colour, base, sweep, BLDC, litres, coil length, colour, insulation, brand | All numeric, so facets. Parse from names at import |
| Kirana | 7 departments → about 20 tiles → sparse chips (तूर · मूंग) | Pack size (from `PackSizes`), brand, veg mark | — |
| Medical | 9 departments → 24 groups; `LinkToId` shortcuts for concern overlaps | Schedule (already `Product.Schedule`), form, salt, manufacturer, cold chain | Salt is a facet and the substitution key |

- **Per-trade attribute definitions**, two options. (1) A **C# registry** `TradeAttributes[trade] = [{key:"sqmm", type:"number", unit:"sq mm", values:[0.75,1,1.5,2.5,4,6], nodes:["electrical/wires/*"], quick:true}, ...]` served by the tree endpoint. Cheapest; this is the backend twin of the frontend `TRADE_MENUS` registry. (2) A global table `attribute_defs(trade, key, type, unit, allowed jsonb, applies_to_node_ids int[], quick bool, sort, names en/hi/gu)`. **Recommend (1) for v1.** Validate `Attrs` keys against it on admin save and CSV import. Unknown keys are rejected, and values for "for" are restricted to the enum.
- **CSV shape:** add columns `node` (key) and optional `attrs` (`for=men;sqmm=1.5` or JSON) to every master CSV. `SeedListsAsync` fills `NodeId` and `Attrs` only when empty, keeping the "never overwrite an admin's edit" rule. The admin import (`MasterImportRequest`) gets the same two columns.
- **Index:** GIN `jsonb_path_ops` on `master_items.attrs` only if server-side filtering is needed. The store and bill already ship the full item list to the device (the store endpoint returns all priced items), so facet filtering can run client-side and no index is needed in v1.

### Gaps
- No source confirms how Indian beauty counters split items by gender. The Women/Men/Kids/Salon switch is the user's own request.
- Allowed facet values (fan sweep, lighting base, beauty concerns) are earlier-report recommendations or researcher knowledge, and still need checking with shopkeepers.

## Shop product ↔ master: inheritance, overrides, shop categories, node changes, pack-size families

### Takeaway
Keep `Product.MasterItemId` as the link and **do not copy the node onto the product**. Work out the product's node at read time:
**effective node = `Product.NodeId` (shop override) → `MasterItem.NodeId` → the shop `Category`'s `NodeId` → name keyword rule → `<trade>/other`.**
A product therefore follows the master's re-homing automatically unless the shop overrode it, the same way photos already follow `MasterItem.ImageAt`. Pack-size and style "families" need no new entity: `MasterItemId` (pack ladder) and `StyleName` (clothes and shades) already group them.

### Cited Findings
- Photos are already inherited at read time, not copied: the store returns `p.ImageAt ?? (p.MasterItem != null ? p.MasterItem.ImageAt : null)` — [StoreEndpoints.cs:93-95](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs); [Product.cs:167-170](backend/src/GroceryKhata.Api/Catalogue/Product.cs).
- GST/HSN changes on the master are **not** applied automatically. `GET /master-items/updates` lists differences and `POST /updates/apply` takes them, and matching is by the name picking gave ("Items the shop renamed or made itself never match") — [MasterItems.cs:425-450, 532](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs).
- Picking copies `master.Category` text into `ImportRow.Category`, which `Categories.LinkAsync` turns into a shop `Category` row of that name — [MasterItems.cs:353-356](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs); [StockService.cs:147](backend/src/GroceryKhata.Api/Catalogue/StockService.cs). As a result, shop category names today are mostly master category strings, which makes mapping them easy.
- Pack sizes: 731 of 1,017 beauty, 918 of 958 kirana, 270 of 337 paint (150 with 4+ packs), 37 hardware and 7 electrical master rows have `packs`. If shops took every size, the 3,940 master rows would become about 7,298 shop items (beauty 1,698, kirana 1,958, paint 981, clothes 1,177, hardware 373, electrical 264, medical 847). Python count, assuming all sizes are picked.
- The earlier report's ladder row ("Elbow CPVC" with 15·20·25·32·40·50 mm chips) and style row (`groupStyles`, `compareSizes`) — [Category menu for all shop types.md](reports/Category%20menu%20for%20all%20shop%20types.md).

### Inferences
- **New columns:**
  - `Product.NodeId int?`: an explicit shop override. Null = inherit. A bool such as `NodeOverridden` is not needed, because null means "follow".
  - `Category.NodeId int?` on the existing shop table: "items in my category X belong under global node Y". This is the `Category.TreeKey` the earlier report named, but as an FK.
- **Shop-created categories as children of a global node:** reuse the existing shop `Category` rows. A shop category whose `NodeId` points at a level-1 or level-2 node, and whose normalised name is **not** that node's name or alias, shows in the menu as a **shop-only tile or chip under that node** ("Sintex tanks" under `hardware/water`). If the name equals the node's name or alias, it is just an alias and adds no tile. No new table is needed.
- **Resolution function** `Nodes.Resolve(product, master, category, tradeTree)`: a pure C# function plus a TS twin, used by the tree, bill, orders and store endpoints. It runs in memory over the already-loaded product list: products join `MasterItem` (already joined for photos) and `Category` (a small per-shop table).
- **When the master item's node changes:** linked products follow at once, with no job, unless `Product.NodeId` is set. When an admin **merges or deactivates a node**, `MergedIntoId` is followed during resolution (as `Categories.ResolveAsync` does for merged names), so overrides and shop `Category.NodeId` pointing at an old node keep working. Recommend a "moved" note in the existing `/master-items/updates` feed only if a shop overrode the node, so the owner can drop the override.
- **Free-text `Product.Category` stays.** It remains the shop's own label on reports (stock statement, P&L, stock health, all grouping by category; [Categories.cs](backend/src/GroceryKhata.Api/Catalogue/Categories.cs) keeps it in sync). The node is a separate browse dimension. Neither replaces the other in v1, which avoids touching the roughly 20 frontend screens that group by `category`.
- **Picking keeps the node:** `POST /products/from-master` needs no change for node inheritance, because `MasterItemId` is already set. Optionally, at pick time it can set `Category.NodeId` for the auto-created shop category from the master's node when that category has none yet. This also maps the shop's own later hand-typed items in that category.
- **Families:** a pack ladder = products with the same `MasterItemId` (and `PackSize`) in the same node, and a style = same `StyleName`. Hand-typed packs (shop's own, no master) can be grouped by `StyleName` too. Beauty shades reuse `StyleName + Colour`, as the earlier report proposed. No `ProductFamily` table is needed.
- **Multi-node for shop items:** extra nodes come only from the master (`ExtraNodeIds`). A shop can change the primary node but not add extras in v1, which keeps the UI small.

### Gaps
- How many existing shop products lack `MasterItemId` is unknown without a DB. The earlier report flagged the same gap.
- Whether owners want overrides at the product level or only at the category level was not researched. The proposal supports both, but the UI could ship category-level only.

## Shop-level category settings and RLS

### Takeaway
Make one new shop-owned table, `shop_node_settings`, for hide/rename/reorder of global nodes. Give it RLS through the existing `EnableShopIsolation` helper. Shop-only nodes reuse the existing shop `categories` table (via `Category.NodeId`), and that table is already shop-owned.

### Cited Findings
- `EnableShopIsolation` is the documented rule: "Every new shop-owned table needs this in its migration". It enables and forces RLS and creates `shop_isolation` with USING and WITH CHECK on `app.shop_id` — [MigrationExtensions.cs](backend/src/GroceryKhata.Api/Persistence/MigrationExtensions.cs).
- `Categories.ResolveAsync` inserts with `shop_id = NULLIF(current_setting('app.shop_id', true), '')::uuid`, relying on RLS context — [Categories.cs:95-101](backend/src/GroceryKhata.Api/Catalogue/Categories.cs). Global tables (`master_items`, `master_suggestions`, `catalogue_items`) deliberately have no RLS — [MasterItems.cs:10-13, 56-59](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs).

### Inferences
- **`ShopNodeSetting : Entity, ITenantOwned`** (table `shop_node_settings`):
  - Columns: `ShopId`, `NodeId int`, `Hidden bool` (everywhere in this shop's menus), `HiddenOnline bool` (store only), `Name text?` (the shop's own label; the en/hi/gu labels still back search), `SortOrder int?`, `Icon text?`.
  - Unique `(shop_id, node_id)`. The migration calls `migrationBuilder.EnableShopIsolation("shop_node_settings")`. Writes go through `ShopOwner` policy endpoints, like `/categories`.
- Hiding a node hides its subtree in menus but **never** hides items from search or the bill (items stay sellable). Recommendation: the bill menu ignores `HiddenOnline`, and the store applies both flags.
- Reorder: `POST /api/v1/catalogue/tree/order { trade, parentKey, keys[] }`, mirroring `POST /categories/order`.
- The new global table `category_nodes` and the new `MasterItem` columns need **no** RLS. `Product.NodeId` and `Category.NodeId` live on tables that are already protected.
- Admin cross-shop review of unmapped shop categories cannot read shop rows under FORCE RLS. Use an aggregate with no shop identity (see the migration section) rather than bypassing RLS.

### Gaps
- Whether staff roles (cashier/supervisor) may hide or rename nodes is not decided. Following `/categories`, owner-only is assumed.

## Customer store: visible nodes, rules, multi-trade, one tree for bill / supplier order / store

### Takeaway
Serve one tree per trade, merged with shop settings and counted per **context**:
- `bill`: active items, priced or not.
- `order`: active, counted items, with a low-stock or supplier filter.
- `store`: active, priced, not hidden online, plus the stock rule.

In each context a node shows **only if it, or a descendant, has at least one visible item**, with the count rolled up. Multi-trade shops get a trade switch. The node of a hand-typed item in a multi-trade shop comes from its category's node, or from name rules over the shop's trades.

### Cited Findings
- Today's store rule is `IsActive && SellingPrice > 0`. Stock does not filter: available is sent as 0 when sold out (`OutOn == today`) or the computed stock, and the client decides — [StoreEndpoints.cs:88-92](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs). `Product.OnSale(today)` exists — [Product.cs:92](backend/src/GroceryKhata.Api/Catalogue/Product.cs). The store is gated by the `Billing.Features.Store` entitlement (StoreEndpoints.cs:83).
- Jewellery pieces have their own online toggle (`Shop.JewelleryShowOnline`) and list — [StoreEndpoints.cs:125-140](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs); [Shop.cs:112](backend/src/GroceryKhata.Api/Shops/Shop.cs).
- The earlier report: a trade switch, not a merged tree (a merged kirana+sweets rail would reach 14 departments, past Baymard's threshold of about 10). `PickItemsScreen` already builds categories per active trade. The store needs medical compliance flags: Rx/H1 badges, and per the 2018 draft rules, never notified, prescription upload for H/H1 and no Schedule X online — [Category menu for all shop types.md](reports/Category%20menu%20for%20all%20shop%20types.md).

### Inferences
- **Visibility rules (store):**
  - node active, not hidden by `Hidden`/`HiddenOnline`
  - item `IsActive`
  - `SellingPrice > 0`
  - stock rule: a new shop setting `StoreShowOutOfStock` (default true = greyed "sold out", which keeps today's behaviour; false = hide when `!OnSale(today)`)
  - optional new `Product.HideOnline bool` (there is no per-item online toggle today)
  - medical: hide `Schedule == X` online; show H/H1 with an Rx badge
- **Counts**: compute per node in the endpoint from the resolved list, rolling up to ancestors and hiding zero nodes. For the store, count "orderable now" separately so the tile can say "12 items · 9 in stock".
- **Multi-trade**: tree nodes carry `Trade`. A product's trade = its effective node's trade. Master-linked products know their trade; hand-typed ones get it from `Category.NodeId`, else name rules tried over the shop's trades in order (main type first), else `<mainTrade>/other`. The 25 names that exist in more than one trade cause no conflict, because the product links to one specific master row.
- **One tree, three screens**:
  - Bill menu: tree plus all active items, `rowKind` from the registry.
  - Supplier order screen: the same tree with job chips (low stock, this supplier) and only counted items (`!Uncounted && !IsService`).
  - Store: the same tree with the store rules.
  - The frontend `<CategoryMenu>` takes `context` and gets nodes plus counts from one endpoint. Item lists already on the device (store payload, bill catalogue cache) get `nodeKey` added per item, so the device can group without a second call.

### Gaps
- No data on how customers browse these small-shop stores (no analytics in the repo). Whether to hide or grey out-of-stock is a product call; greying is the current behaviour.
- E-pharmacy rules are still draft. The legal position for online H/H1 sales needs a lawyer's check (noted in the earlier report too).

## Migration of existing data: mapping rules, counts, admin review

### Takeaway
Map **master rows first** (3,940 rows, 120 category strings) with a node column in each CSV. **About 82–85% map by category string alone** (my estimate), and the rest need keyword rules or a hand pass. Then shop products resolve mostly through `MasterItemId` and through shop category names that equal master strings. Rule order: **product override → master link → shop category → category name/alias → name keyword rules → `<trade>/other`**, with an admin "unmapped" review queue.

### Cited Findings (Python over the CSVs; regex heuristics, so approximate)
- **Clothes (204)**: 160 rows split automatically on " · " into department and group. The 44 flat rows are top-level departments (Winter wear 21, Accessories 13, Sportswear 5, Fabric by metre 5), so 100% maps by string. "Kids · Baby (0–2 years)" (12) must split into Baby girl/boy by name, or `for` — [clothes-master.csv](backend/src/GroceryKhata.Api/Catalogue/Data/clothes-master.csv).
- **Electrical (258)**: 18 groups map 1:1 to level-2 nodes under 9 rail entries, so 100% maps by string. Attributes are parsed from names (sq mm, amps, watts).
- **Medical (847)**: 24 groups map 1:1 to level-2 nodes under 9 departments, so 100% maps by string. Overlaps such as Diabetes are handled with `LinkToId` shortcuts.
- **Paint (337)**: 13 categories map N:1 onto 6 departments. 7 rows are in "Other" and need a hand pass.
- **Hardware (319)**:
  - Most of the 14 categories map 1:1 or N:1.
  - **"Pipes and fittings" (71) must split into the 7 water tiles.** A first keyword pass matched 60 of 71: pipes 15, fittings 13, taps & bath 11, solvent & tape 9, valves 8, drainage 4. Eleven were unmatched; they mainly belong elsewhere — adhesives (Araldite, Fevicol Marine, SR 998, PU foam, WD-40, grease), tile adhesive or wall putty (likely paint/building), a toilet seat cover (sanitary), and Tata Tiscon Readybuild (steel).
  - Five more rows in "Other", "Door and furniture fittings" (4) and "Chain, rope and welding" (1) need hand moves (Tower bolt, Hinge, Zanjeer, Hook/D-shackle, Cutting wheel, MS plate, Wall putty).
- **Kirana (958)**: 18 Hindi merged strings onto about 20 tiles. Merged strings need keyword splits: "आटा, चावल, दाल" 99 (atta / rice / dal), "चाय, कॉफ़ी, दूध" 49, "इंस्टेंट, नाश्ता" 54, "डेयरी, ब्रेड" 41, and "मसाले" 100 (masala vs dry fruits is already separate). About 240 rows need a name rule; the rest (about 720) map by string.
- **Beauty (1,017)**: the 13 categories map to area nodes, except "Skin care" (167) and "Bath, body and personal care" (72), which split into Face vs Body by keyword, and 11 "Other" rows. `for` facet: 40 rows come from Men's grooming, 13 from men words elsewhere, 22 from the baby group, and the rest default to `["women","men"]` (unisex) or `["women"]` for makeup and nails by category.
- **Starter items** (outside the CSVs, about 216 rows across 15 trades) use old names ("Plumbing", "Interior", "Hinges", "Pumps", "Mithai"). `StarterGroups` already maps 21 old names for medical, hardware, paint and clothes — [MasterItems.cs:261-275](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs); [StarterItems.cs](backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs). The 9 starter-only trades get 1-level nodes straight from their starter categories (4–8 each).
- Rough total (my estimate): of 3,940 CSV rows, about 3,250–3,350 (82–85%) map from the category string alone. About 550–650 need name rules (kirana about 240, beauty about 240 skin/body splits, hardware 71, clothes baby 12). About 30–40 need a hand decision (the Other buckets: beauty 11, paint 7, hardware 2, plus hardware misfits 11 and starter oddities). Add the beauty `for` facet over about 1,000 rows, mostly by category default.

### Inferences
- **Migration steps (data):**
  1. Write `category-nodes.csv` per trade (key, parent key, level, en, hi, gu, icon, sort, aliases including the old category strings).
  2. Add `node` and `attrs` columns to the 7 master CSVs, filled by a one-off script: category alias → node; then ordered keyword rules per split category; then `<trade>/other`. Review the "other" rows by hand (about 40) and commit.
  3. Seed: `SeedListsAsync` sets `NodeId` and `Attrs` where null. For admin-edited rows whose category string is no longer in the CSV, use the node alias table. This mirrors the `StarterGroups` logic.
  4. Shop side, run per shop under its tenant context (like `LinkByNameAsync`, which runs with the owner's update check): first `LinkByNameAsync` to back-fill `MasterItemId`; then set `Category.NodeId` where the shop category's normalised name matches a node alias or name in the shop's trades. Products themselves get **no stored node**, because resolution is live.
- **Admin review of unmapped items:**
  - (a) Master: `GET /api/v1/platform/catalogue/unmapped?trade=` lists master items whose node is null or `<trade>/other`, with a bulk "move to node". It sits behind `PlatformAdmin`/`PlatformOpenShop` like the master-item admin.
  - (b) Shop categories across shops, without breaking RLS: a global, identity-free aggregate table `unmapped_category_names(trade, normalised_name, shops int, items int, last_seen)`. A per-shop job fills it under each shop's own context, inserting names only and no shop id. The admin then adds an alias to a node and every shop's category resolves on its next refresh. This mirrors `MasterSuggestion` being deliberately global (but that table keeps `ShopName`; this one should not).
  - (c) Per shop: the owner's Categories screen shows "not in the menu yet" categories with a node picker (the "विभाग" picker from the grocery plan).

### Gaps
- Live counts of shop products without a master link, and of distinct shop category strings, are unknown (no DB).
- The keyword-split percentages are first-pass regex results, not a reviewed mapping. Expect ±10% on the rule-mapped buckets.

## API shape sketch and build order with effort

### Takeaway
Four read endpoints and three write endpoints cover it. Building takes about **9–12 developer days** for the relationship layer (tables, seed, resolution, endpoints, admin review), not counting the menu UI already sized in the earlier report's steps 3–11. Mapping content work comes on top (about 3–4 days across trades).

### Cited Findings
- Existing endpoints to extend: `GET /api/v1/master-items?trade=` (ShopListAsync), `POST /api/v1/products/from-master`, `/api/v1/platform/master-items` (+ `/import`), `GET/POST /api/v1/categories`, and `GET /api/v1/store/{slug}` — [MasterItems.cs:122-140](backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs); [Categories.cs:62-71](backend/src/GroceryKhata.Api/Catalogue/Categories.cs); [StoreEndpoints.cs:58-64](backend/src/GroceryKhata.Api/Planner/StoreEndpoints.cs).
- The earlier plan sized "trade-keyed tree + registry" at 2 days and "kirana node mapping + MasterItem.Node, Category.TreeKey, nodeOf()" at 2 days — [Category menu for all shop types.md](reports/Category%20menu%20for%20all%20shop%20types.md).

### Inferences
- **API sketch:**
  - `GET /api/v1/catalogue/tree?trade=hardware&context=bill|order|store` (ShopMember). Returns `{ trade, nodes:[{key, parentKey, level, name, nameEn, nameHi, nameGu, icon, sort, typeKey, linkTo, count, inStock, shopOnly?:bool, hidden?:bool}], attributes:[{key,type,unit,values,quick,nodes}] }`. It merges `category_nodes` + `shop_node_settings` + shop-only `Category` tiles, with counts per context. Without `trade`, it returns every trade of the shop as `{trades:[...]}`.
  - `GET /api/v1/master-items?trade=`: each `MasterItemDto` gains `nodeKey`, `extraNodeKeys`, `attrs`. The picker groups by node instead of the category string.
  - `POST /api/v1/products/from-master` is unchanged, since products inherit via `MasterItemId`. Optionally it fills `Category.NodeId` on the auto-created shop category.
  - Product DTOs (`/products`, bill catalogue sync) gain `nodeKey` (resolved) and `nodeOverride` (nullable). `PUT /products/{id}` accepts `nodeKey` (null = follow master).
  - `PUT /api/v1/categories/{id}` gains `nodeKey`, which maps a shop category onto a node or makes it a shop-only tile.
  - `PUT /api/v1/catalogue/tree/{key}/settings { hidden, hiddenOnline, name, icon }` and `POST /api/v1/catalogue/tree/order { trade, parentKey, keys[] }` (ShopOwner).
  - `GET /api/v1/store/{slug}`: each `StoreItemDto` gains `nodeKey` and the response gains `tree:[...]` (context=store, already filtered and counted), so the store renders the rail with no second call.
  - Admin: `/api/v1/platform/catalogue/nodes` (GET/POST/PUT, merge), `/api/v1/platform/catalogue/unmapped?trade=`, `POST /api/v1/platform/catalogue/unmapped/move { masterIds[], nodeKey }`, `GET /api/v1/platform/catalogue/unmapped-categories` (the aggregate), and `POST /nodes/{key}/aliases`.
- **Build order (rough developer-day estimates, mine):**

| # | Step | Effort |
|---|---|---|
| 1 | `category_nodes` entity + migration (no RLS), `category-nodes.csv` per trade, seeder (only-add rule), in-memory `TradeTrees` cache | 1.5–2 d |
| 2 | `MasterItem.NodeId/ExtraNodeIds/Attrs` + CSV `node`/`attrs` columns + seed/import changes + `TradeAttributes` registry and validation | 1.5–2 d |
| 3 | Mapping script + hand review per trade (content work: kirana and beauty are the largest, hardware pipes split, Others) | 3–4 d (content) |
| 4 | `Product.NodeId`, `Category.NodeId` + `Nodes.Resolve` (C# + TS) + DTO fields + per-shop back-fill of `Category.NodeId` | 1.5–2 d |
| 5 | `shop_node_settings` + `EnableShopIsolation` + settings/order endpoints + `GET /catalogue/tree` with contexts and counts | 2 d |
| 6 | Store: `nodeKey` + `tree` in payload, visibility rules (`StoreShowOutOfStock`, optional `HideOnline`, Schedule X) | 1 d |
| 7 | Admin unmapped queue + identity-free unmapped-category aggregate + alias editing | 1.5–2 d |
| 8 | Tests: RLS isolation test for `shop_node_settings`, resolution order, master node change followed or not, merged node pointer | 1 d |

- Steps 1–2 and 4 can ship before the menu UI. Steps 5–6 sit where the earlier plan's steps 3–4 (menu shell, bill and supplier orders) meet, and they should land together.
- Risk notes: (a) keep `Product.Category` untouched to avoid regressions on about 20 screens; (b) the node `Key` must never be reused or renamed once shipped, since it is in CSVs and clients; (c) jewellery stays outside this layer until the adapter (earlier step 11).

### Gaps
- The effort figures are my estimates, extrapolated from the earlier report's sizing, not measured.
- Translating Gujarati and Hindi node names, and checking counter words with shopkeepers, is not included in the estimates.
