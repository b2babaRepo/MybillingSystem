# Grocery Khata: what a clothes shop gets today (code audit)

Sources are repo paths (relative to C:\Users\Ravi Mishra\source\repos\Grocery_app). B = backend/src/GroceryKhata.Api.

## Where does 'clothes' appear, and is there a master list?

### Takeaway
`clothes` is a first-class shop type with labels, an 8-item starter list and an import sample, but there is NO clothes master CSV (only beauty, hardware, kirana, medical, paint exist).

### Cited Findings
- Enum value `Clothes` — [B/Shops/Shop.cs:106]
- Frontend ShopType / SHOP_TYPES / STARTER_TYPES include 'clothes' — [frontend/src/lib/reports.ts:65-68]
- Labels "Clothes" / "कपड़े" — [frontend/src/i18n/en.ts:544], [frontend/src/i18n/hi.ts:546]
- Import sample: "Cotton shirt (M), piece, 699, 450, Shirts, 10, 3" (size baked into the name) — [frontend/src/screens/ImportScreen.tsx:24]
- Starter items: 8 rows in 4 categories (Men 3, Women 2, Kids 1, Accessories 2): Men's shirt 6205, Men's trousers 6203, T-shirt 6109, Kurti 6204, Saree 5407, Kids' frock 6209, Socks (pair) 6115, Handkerchief (dozen) 6213, all GST 5% — [B/Catalogue/StarterItems.cs:20, 200-211]
- Catalogue/Data contains only beauty-, hardware-, kirana-, medical-, paint-master.csv; no *clothes*.csv — [B/Catalogue/Data/]

### Inferences
- A clothes shop gets the Suggested/one-tap-add experience only for these 8 generic items.

### Gaps
- Gap: clothes master list (~300-600 items: men/women/kids/ethnic/innerwear/accessories, with HSN). Effort: medium (data work plus CSV loader already exists for other trades).

## Can a product have variants (size, colour)?

### Takeaway
No. Each size/colour must be a separate Product. The only grouping mechanism is grocery-style "options" of one common catalogue item (CatalogueItemId + PackSize/PackUnit + IsDefaultOption).

### Cited Findings
- Product fields: Name, Category (free text), CatalogueItemId, PackSize, PackUnit, IsDefaultOption, Unit, SellingPrice, GstRate, HsnCode, CostPrice, Stock, Reserved, ReorderLevel, Mrp, Barcode, MasterItemId, TracksSerials, etc. No Size, Colour, Style/Article no., Brand, Season or parent-product field — [B/Catalogue/Product.cs:5-104]
- PackSize/PackUnit documented as "what one pack holds (5 kg bag)"; IsDefaultOption is for "atta, any brand" — [B/Catalogue/Product.cs:20-32]
- Single Barcode and Mrp per product — [B/Catalogue/Product.cs:88-91]
- GstRate and HsnCode per product — [B/Catalogue/Product.cs:43-46]

### Inferences
- A shirt in 5 sizes x 3 colours = 15 manually created products; reports, search and Suggested lists would be cluttered.

### Gaps
- Gap: style -> variant model (size/colour matrix, per-variant SKU, barcode, MRP, stock), bulk matrix create. Effort: large (schema, API, billing, stock, purchase, reports, offline outbox).

## Billing: scan, discounts, GST, returns, exchanges, salesperson, tags

### Takeaway
Bill supports search-and-add, editable line price, per-line GST snapshot and CGST/SGST/IGST, printing of the bill and credit notes. Missing: barcode scanning at the counter, discounts, exchange flow, salesperson, tag/label printing.

### Cited Findings
- BillScreen is 444 lines; add is via text search (aria-label search) — [frontend/src/screens/BillScreen.tsx:126]
- Line unit price editable — [frontend/src/screens/BillScreen.tsx:184-195]
- Print bill link `/print/bill/{id}` — [frontend/src/screens/BillScreen.tsx:176]
- Barcode is only used in ProductScreen to look up a product photo (`/image/barcode`), not to scan into a bill — [frontend/src/screens/ProductScreen.tsx:266-320]
- Bill entity: Total, PaidAmount, PaymentMode, Gstin, BuyerGstin, BillOfSupply, cancel fields; no discount, no salesperson/staff field — [B/Ledger/LedgerEntities.cs:105-163]
- BillLine: ProductId, Name, Quantity, Unit, UnitPrice, Amount, GstRate, HsnCode, UnitCost, Batch, TaxableValue (+Cgst/Sgst/Igst); no discount — [B/Ledger/LedgerEntities.cs:165-200]
- GST snapshotted from product at billing; taxable = Amount/(1+rate) (GST-inclusive pricing) — [B/Ledger/LedgerService.cs:276, 668-670]
- "discount" appears only in supplier purchase rows (trade discount %) and packages, not bills — [frontend/src/screens/SuppliersScreen.tsx:15-16]
- Credit notes exist: CreditNote (BillId, Reason, Total, Restocked) and CreditNoteLine (BillLineNo, Quantity, Amount, GstRate, HsnCode), numbered CN/FY/nnnn — [B/Ledger/CreditNotes.cs:14-79]

### Inferences
- Returns work (credit note + restock); an exchange must be done as credit note + new bill manually.
- Salesperson commission could reuse ideas from salon StylistPay (B/Ledger/StylistPay.cs) but nothing is wired to bills.

### Gaps
- Barcode/camera scan into bill (Product.Barcode exists): small-medium.
- Line and bill discount (% / Rs) with GST on post-discount value: medium.
- Exchange flow (return lines + new lines, net to pay/refund in one screen): medium.
- Salesperson on bill + sales/commission report: medium.
- Price tag / barcode label printing (MRP, size, code): medium.

## Stock: per-variant, stocktake, purchases with sizes

### Takeaway
Stock is per Product only (Stock, Reserved, ReorderLevel, StockMovement); a stocktake screen and supplier purchases exist, but neither knows sizes/colours.

### Cited Findings
- Product.Stock/Reserved/ReorderLevel; StockMovement ledger with kind/reason/bill/cost — [B/Catalogue/Product.cs:55-64, 149-202]
- StocktakeScreen route — [frontend/src/App.tsx:35, 369]
- Purchase / PurchaseLine entities — [B/Ledger/Suppliers.cs:22, 43]; purchase rows carry quantity, unitCost, batch, expiry, MRP, free, discount, GST — [frontend/src/screens/SuppliersScreen.tsx:16]
- Supplier returns exist — [B/Ledger/SupplierReturns.cs]

### Gaps
- Size-wise purchase entry (size-run grid per style) and size-wise stock/ageing/dead-stock reports: large (depends on variant model).

## GST: rate choice, price-dependent apparel GST

### Takeaway
GST is a fixed per-product rate (manual or from starter/master); nothing computes rate from the per-piece sale value. A code comment acknowledges the rule (5% up to Rs 2,500/piece, 18% above) but leaves it to the shop to set per item.

### Cited Findings
- Comment: "Garments: 5% when sold at up to ₹2,500 a piece, 18% above (set per item by its price)" and all starter items hard-coded 5% — [B/Catalogue/StarterItems.cs:200-210]
- Grep for 2500 / price-slab logic finds only that comment — [B/ (grep)]
- Bill line takes product.GstRate unchanged — [B/Ledger/LedgerService.cs:276]

### Inferences
- A Rs 2,000 MRP shirt sold at Rs 2,600 after no discount, or an item priced over Rs 2,500 but discounted below it, will be taxed wrongly; once discounts exist the slab must be evaluated on the final per-piece transaction value.

### Gaps
- Price-slab GST rule per HSN chapter (61/62/63 etc., threshold per piece on post-discount value) applied at billing: small-medium (rule table + check in LedgerService line build + UI hint). Threshold value should be confirmed by a CA.
