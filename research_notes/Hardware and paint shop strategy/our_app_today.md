# What Grocery Khata already supports for hardware and paint shops (code audit, 25 Nov 2026)

Sources are files in this repo (paths relative to repo root). Code was read only; nothing was run or changed.

## Shop types and master item lists

### Takeaway
Both trades exist as first-class shop types, each with a seeded master list (about 337 paint rows, 319 hardware rows) and a smaller starter list with GST and HSN. Prices are not seeded as facts.

### Cited Findings
- `ShopType` enum has `Paint` and `Hardware` (also `BuildingMaterials`, `Electrical`, `Woodwork`, which overlap these trades). The enum comment says the type "only changes defaults (units, starter items, bill buttons)" — [Shop.cs](../../backend/src/GroceryKhata.Api/Shops/Shop.cs) (lines ~178-200)
- Master lists load from CSV per trade: `paint-master.csv` -> Paint, `hardware-master.csv` -> Hardware — [MasterItems.cs](../../backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs) (line ~184)
- `paint-master.csv` has 338 lines (337 rows) with columns name, name_hi, category, unit, gst, hsn, packs; packs is a pack ladder such as "1 L|4 L|10 L|20 L". Some rows have a blank GST (for example Birla White tile adhesive) — [paint-master.csv](../../backend/src/GroceryKhata.Api/Catalogue/Data/paint-master.csv)
- `hardware-master.csv` has 320 lines (319 rows) — [hardware-master.csv](../../backend/src/GroceryKhata.Api/Catalogue/Data/hardware-master.csv)
- Old starter categories for hardware and paint (Interior, Exterior, Enamel, Primer & putty) are mapped onto the master list's groups (for example "Interior emulsion", "Enamel and metal paint") — [MasterItems.cs](../../backend/src/GroceryKhata.Api/Catalogue/MasterItems.cs) (lines ~260-268)
- Starter items exist for Paint and Hardware with price 0 (to fill in on "today's rates"), GST and HSN. The code comment says most hardware and electrical goods are 18%, and that "a CA should check both" — [StarterItems.cs](../../backend/src/GroceryKhata.Api/Catalogue/StarterItems.cs) (lines ~6-21, 224-335)
- The planning report recommends about 550 rows, one row per product line with a pack ladder, every price marked "verify with dealer list" or blank, and a short list of HSN headings flagged "check with CA" (9603 brushes, 8201 farm tools, 2520 POP, 6811, 3824) — [Hardware and paint master list.md](../../reports/Hardware%20and%20paint%20master%20list.md); research notes in `research_notes/Hardware and paint master list/` (asian_paints, birla_berger, hardware_core, other_paints_accessories, tata_building_hardware)

### Inferences
- The master list is near the planned size (656 against about 550 planned). Price coverage is unknown from code. The report says fewer than one in five paint rows had a sourced price.
- A shop can hold several types (the Home tile checks `types.includes(...)` / `types.some(...)`), so a hardware-plus-paint shop works — [HomeScreen.tsx](../../frontend/src/screens/HomeScreen.tsx) (lines ~123-124)

### Gaps
- I did not count how many master rows have a blank GST or HSN, or a price.

## Units, pack sizes, variants and shades, barcode

### Takeaway
Units and pack sizes are good. Shade and tinting are partial: the bill has a tinted-line sheet, but shades are not stored as variants or kept in stock. Barcodes exist.

### Cited Findings
- `Unit` enum includes Piece, Kg, G, Litre, Ml, Packet, Bag, Tonne, Brass, Cft, Metre, Pair, Dozen, Box, Jar, SqFt, RunningFt, Quintal — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (lines ~175-200)
- Product has `PackSize` + `PackUnit` (a 4 L tin), `BagSize` + `PurchaseUnit` (bought in cartons or bags, stock kept in the base unit with bags counted beside it), and `WeightKg` (for freight) — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (lines ~22-42)
- Pack options of the same item are grouped by `CatalogueItemId`, with `IsDefaultOption` — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs)
- `StyleName` / `Size` / `Colour` group items into variants (built for clothes). Each variant is its own item with its own stock and price — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (lines ~133-142)
- Tinting: for paint shops, the bill has an "add tinted" sheet. The owner picks the base tin (it comes off stock), types the shade code from the tinting machine and a tinting charge, and the rate is the base price plus the tinting charge — [TradeBill.tsx](../../frontend/src/screens/TradeBill.tsx) (lines ~132-170)
- `Barcode` field (EAN-13) on the product, plus a Barcodes module — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs); [Barcodes.cs](../../backend/src/GroceryKhata.Api/Catalogue/Barcodes.cs)
- `TracksSerials` and `WarrantyMonths` exist (useful for power tools) — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs)

### Inferences
- The tinted line is entered by hand. There is no shade-card master, no colourant (stainer) use from stock, and no saved shade per customer for repeat orders.
- I found no named alternate-unit conversion such as "1 box = 100 screws, sell loose or by box". BagSize/PurchaseUnit covers buy-in-cartons, sell-in-pieces, but selling one item in two units on a bill was not confirmed.

### Gaps
- Whether the tinted line stores the shade code in the bill lines or only in the line name was not checked in the backend.

## Prices: tiers, bulk rate revision, GST/HSN

### Takeaway
Quantity-based wholesale price and a multi-item "today's rates" screen exist. Per-customer price tiers (retail, contractor, dealer) do not. GST and HSN per item exist.

### Cited Findings
- `WholesalePrice` + `WholesaleMinQty`: the bill switches to it by itself once the quantity reaches the minimum. It must be below the regular price — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (lines ~118-124); [StockService.cs](../../backend/src/GroceryKhata.Api/Catalogue/StockService.cs) (lines ~168-226)
- `Mrp` is shown struck through when the shop sells below it — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs)
- `PUT /products/rates` takes a list of per-item rate changes ("today's rates"). Every change goes to the audit log with old and new price, which serves as the rate history — [ProductEndpoints.cs](../../backend/src/GroceryKhata.Api/Catalogue/ProductEndpoints.cs) (lines ~141-144, 195-210)
- Product has `GstRate` and `HsnCode`. Allowed GST rates are 0, 5, 18 or 40% — [Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs); [StockService.cs](../../backend/src/GroceryKhata.Api/Catalogue/StockService.cs) (line ~584)
- A search for CustomerType/PriceGroup/dealer price on customers found nothing — [LedgerEntities.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerEntities.cs)

### Inferences
- Missing: price lists per customer group (contractor, painter, retail) and brand-wide % revisions ("Asian Paints +3% from 1 Dec"). The rates screen sends explicit new prices per item, and I saw no percentage or brand filter.

### Gaps
- Whether the rates screen UI can filter by brand or category was not checked.

## Quotations, challans, contractor/site accounts, credit limits, commission

### Takeaway
This is the strongest area. Estimates, delivery challans billed together into one invoice, site-wise totals, credit limits and credit days, and a painter/electrician/carpenter commission report all exist. Commission payout tracking is missing.

### Cited Findings
- Estimates: list, get, add, mark as billed, cancel. They work for walk-ins without a khata, and lines are product, name, quantity, unit and rate — [Estimates.cs](../../backend/src/GroceryKhata.Api/Ledger/Estimates.cs)
- Delivery challan: goods leave stock now, nothing goes on the khata until challans are made into one invoice. It records site, vehicle number and place of supply. Endpoints: `/challans` and `/challans/invoice` (Pro "Trade" feature) — [Trades.cs](../../backend/src/GroceryKhata.Api/Ledger/Trades.cs) (lines ~12-110)
- Site-wise totals per customer (`/customers/{id}/sites`), plus `credit-limit`, `credit-days`, GSTIN, address and state per customer — [Trades.cs](../../backend/src/GroceryKhata.Api/Ledger/Trades.cs)
- Bill fields `Site`, `VehicleNumber`, `PainterId` — [LedgerEntities.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerEntities.cs) (lines ~236-243)
- Credit limit shows a warning on the bill when balance plus this bill's khata amount goes over the limit — [TradeBill.tsx](../../frontend/src/screens/TradeBill.tsx) (lines ~112-118)
- `Painter` entity (name, phone, CommissionPercent 0-50%). `/painters/commission?month=` returns each painter's bills, sales and commission (total × %), excluding cancelled bills. The screen sends a WhatsApp message. The same feature is offered to paint, electrical and woodwork shops, with the label "Painters, electricians, carpenters & commission" — [Trades.cs](../../backend/src/GroceryKhata.Api/Ledger/Trades.cs) (lines ~43-86, 443-478); [PaintersScreen.tsx](../../frontend/src/screens/PaintersScreen.tsx); [TradeBill.tsx](../../frontend/src/screens/TradeBill.tsx) (lines ~107-129); [en.ts](../../frontend/src/i18n/en.ts) (line ~215)

### Inferences
- Missing: commission paid or settled tracking (no payout entity found in Trades.cs), commission per item or brand (it is on the whole bill), painter loyalty points, and a check that the credit limit is enforced on the server (only the UI warning was seen).
- Estimate-to-bill exists, but I saw no estimate-to-challan path.

### Gaps
- Whether the backend blocks bills over the credit limit was not verified.

## Supplier orders, returns, e-way bill, reports

### Takeaway
Purchase orders, goods tracking (with an e-way bill number on inbound lots), supplier returns and credit notes exist. E-way bill generation does not; there is only a reminder above ₹50,000.

### Cited Findings
- Purchase orders with Draft, Open and Closed (closed short) states — [PurchaseOrders.cs](../../backend/src/GroceryKhata.Api/Ledger/PurchaseOrders.cs); also [GoodsReceipts.cs](../../backend/src/GroceryKhata.Api/Ledger/GoodsReceipts.cs), [SupplierItems.cs](../../backend/src/GroceryKhata.Api/Ledger/SupplierItems.cs)
- Inbound lots record the truck, bilty (LR), e-way bill number (validated as 12 digits) and freight — [GoodsTracking.cs](../../backend/src/GroceryKhata.Api/Ledger/GoodsTracking.cs) (lines ~36, 254)
- Sales e-way: the bill shows a reminder at `EWAY_LIMIT = 50_000`, and the vehicle field is commented "goods over ₹50,000 moved by road need an e-way bill" — [TradeBill.tsx](../../frontend/src/screens/TradeBill.tsx) (line 22); [LedgerEntities.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerEntities.cs) (line ~236)
- Sales returns as numbered credit notes (CN/2026-27/0001) that lower the sale and its tax — [CreditNotes.cs](../../backend/src/GroceryKhata.Api/Ledger/CreditNotes.cs); supplier returns — [SupplierReturns.cs](../../backend/src/GroceryKhata.Api/Ledger/SupplierReturns.cs); GST export — [GstExport.cs](../../backend/src/GroceryKhata.Api/Ledger/GstExport.cs)
- Reports that mention these trades: Hardware and paint master list; Multi trade shop types (says paint and building materials are "the most complete trades" and lists "Shade/size variants" as the paint gap); Grocery Khata leadership plan core features; competitive winning strategy; versus rival apps — files in [reports/](../../reports/)

### Inferences
- Missing: e-way bill JSON or portal generation for sales, e-invoice, and a returned tinted tin (cannot go back to stock as a base). No hardware-specific reports were found (for example dead stock by brand, or painter-wise item mix).

### Gaps
- Whether the frontend reports module (`frontend/src/lib/reports.ts`, 8 matches) has paint-specific reports beyond `SHOP_TYPES` was not checked in detail.

## Summary table

| Area | Status |
|---|---|
| Paint / Hardware shop types | Exists |
| Master lists (paint 337, hardware 319 rows, pack ladders, GST/HSN) | Exists; prices largely unsourced |
| Units, pack sizes, buy in cartons | Exists |
| Sell one item in two units on a bill | Not confirmed / likely missing |
| Size/colour variants | Partial (clothes-style StyleName) |
| Tinting / shade | Partial (manual tinted line; no shade master, no colourant stock) |
| Barcode, serials, warranty | Exists |
| Quantity wholesale price, MRP | Exists |
| Price tiers per customer type | Missing |
| Bulk rate revision | Partial (multi-item rates screen with audit history; no % or brand-wide change) |
| Estimates / quotations | Exists |
| Delivery challans, billed together into one invoice | Exists (Pro) |
| Contractor site accounts | Exists (site per bill, site totals) |
| Credit limit and credit days | Exists (UI warning; server enforcement unverified) |
| Painter/electrician/carpenter commission | Exists (monthly report); payout tracking missing |
| Supplier POs, goods tracking, supplier returns | Exists |
| Sales returns (credit notes) | Exists |
| E-way bill | Partial (₹50k reminder, inbound EWB number); no generation |
