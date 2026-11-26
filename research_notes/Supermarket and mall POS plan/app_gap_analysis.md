# Apna Kaarobar / Grocery Khata: how far it is from a supermarket and mall-store POS (code gap analysis)

Method: I read the code on the current branch (HEAD `573401f`), plus `docs/runbook.md`, `loadtest/evening-rush.mjs` and the earlier reports. Sources are repo-relative file paths, with function or type names where they help. "Has" means the code does it. "Partly" means some of it exists but not what a multi-counter supermarket or mall store needs. "Lacks" means I found nothing in a grep across backend and frontend. Sizes are rough: Small is days, Medium is 1–3 weeks, Large is more than that or needs new architecture. Nothing was run. All of this comes from reading the code.

## 1. Checkout: scanning, weighing, keyboard, held bills, multi-counter, speed, offline

### Takeaway
The single-counter checkout is already good for a desktop or phone counter. It has a USB or camera scanner, a Web Serial scale, F-key shortcuts, held bills and an offline outbox. It was not built for many tills in one shop. There is one bill series per shop per financial year, and its row lock is held for the whole bill transaction. Held bills stay on one device. Weight-embedded barcodes are not supported, and the full catalogue downloads to every device.

### Cited Findings
- **USB scanner (keyboard wedge): Has.** `onSearch` treats 8–14 digits typed into the search box as a barcode and adds the item straight away (`addByBarcode`). An unknown code shows a "scan miss" prompt. — [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx) (around lines 74–95, 595–606); [frontend/src/components/UnknownBarcode.tsx](../../frontend/src/components/UnknownBarcode.tsx)
- **Camera scanning: Has.** `ScanSheet` scans continuously. It uses the native `BarcodeDetector` and falls back to the `barcode-detector` ponyfill with `zxing-wasm`. — [frontend/src/components/ScanSheet.tsx](../../frontend/src/components/ScanSheet.tsx); [frontend/package.json](../../frontend/package.json)
- **Barcode normalisation: Has.** UPC-A is padded to EAN-13 and EAN-8/13 check digits are verified (`Barcodes.Normalize`, `CheckDigitOk`). — [backend/src/GroceryKhata.Api/Catalogue/Barcodes.cs](../../backend/src/GroceryKhata.Api/Catalogue/Barcodes.cs); [frontend/src/lib/barcode.ts](../../frontend/src/lib/barcode.ts) `normBarcode`
- **Barcodes per item: Partly.** Each product has one `Barcode` string. I found no table for several barcodes per item, which matters when the same SKU arrives from different packers. — [backend/src/GroceryKhata.Api/Catalogue/Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (line ~145)
- **Weighing scale over serial/USB: Has, desktop Chrome only.** `readScale` uses Web Serial, waits for two equal stable readings and parses formats like Essae/Aczet (`parseWeight`). The file's own comment says Android Chrome cannot do this. The bill screen imports it on demand (around line 401). — [frontend/src/lib/scale.ts](../../frontend/src/lib/scale.ts); [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx)
- **Barcode-embedded weight or price labels (the "2xxxx" codes printed by label scales): Lacks.** A grep for embedded, price-embedded or weight barcode parsing found nothing. `addByBarcode` only does an exact match and always adds quantity 1. — [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx) `addByBarcode`. Size: **Small–Medium** (a prefix and PLU parser plus a PLU field per product).
- **Keyboard shortcuts and desktop layout: Has.** F2 search, F3 customer, F4 paid, F6 all on khata, F7 hold or new bill, F8 remove last line, F9 bill discount, Ctrl+Enter save, Ctrl+P print, `2*` for quantity, Esc, and `?` for the key list. Desktop has its own layout from 1024 px. — [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx) (lines ~527–549, 621); [reports/Session summary desktop and billing.md](../../reports/Session%20summary%20desktop%20and%20billing.md)
- **Held or parked bills: Partly.** `holdBill` and `resumeBill` keep held carts in `localStorage` under the key `held-bills:<shopId>`. The code comment says "Kept on this device only". A bill parked at one till cannot be picked up at another. — [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx) (lines ~110–118, 163–178). Size to make them server-side: **Small–Medium**.
- **Bill numbering with many counters: Partly, with a bottleneck.** `NextBillNumberAsync` upserts one `bill_counters` row per `(shop_id, financial_year)`. It runs right after `BeginTransactionAsync` in `CreateBillAsync`, so the counter row stays locked through all the stock-movement, ledger, batch and balance writes until commit. Two tills "queue on the counter row", in the code's own words. Concurrent bills in one shop are therefore serialised. There is one series per shop (plus an optional `BillPrefix`). I found no per-counter or per-register series. — [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs) (lines ~268–270, 652–666); [backend/src/GroceryKhata.Api/Persistence/DocumentNumbers.cs](../../backend/src/GroceryKhata.Api/Persistence/DocumentNumbers.cs); [backend/src/GroceryKhata.Api/Shops/Shop.cs](../../backend/src/GroceryKhata.Api/Shops/Shop.cs) `BillPrefix`
- **Counter identity: Partly.** A bill carries a free-text `Counter`, at most 30 characters. It comes from the device's `localStorage` key `gk-counter` (`counterName()`). It is a label with no registered terminal entity behind it. — [frontend/src/lib/data.ts](../../frontend/src/lib/data.ts) `counterName`/`setCounterName` (line ~307) and line 224; [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs) line ~237
- **Bill size limit:** 1 to 200 lines per bill (`request.Lines is { Count: > 0 and <= 200 }`). — [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs) line ~29
- **Speed of saving a bill: Has.** Bills go into the IndexedDB outbox first, so saving never waits on the network. `BillSpeed` records the seconds from first item to save and reports medians by bill size (`/reports/bill-speed`). — [frontend/src/lib/outbox.ts](../../frontend/src/lib/outbox.ts); [backend/src/GroceryKhata.Api/Ledger/BillSpeed.cs](../../backend/src/GroceryKhata.Api/Ledger/BillSpeed.cs); [backend/src/GroceryKhata.Api/Ledger/Reports.cs](../../backend/src/GroceryKhata.Api/Ledger/Reports.cs) line ~222; [reports/Buy and sell workflow review.md](../../reports/Buy%20and%20sell%20workflow%20review.md)
- **Offline billing and sync: Has, with caveats.** Every write is queued with its own record ID as the idempotency key. Kinds include bill, khata, product, stock and expense. A provisional receipt can print before sync (`provisionalBill`). Offers are cached in `localStorage` so they apply offline (`useOffers`). The outbox flushes one op at a time, and the earlier review flagged N sequential round trips after a long time offline. Credit notes and exchange slips are not outbox kinds, so returns need a connection. — [frontend/src/lib/outbox.ts](../../frontend/src/lib/outbox.ts) (`OutboxKind`, `enqueue`, `flush`); [frontend/src/lib/provisionalBill.ts](../../frontend/src/lib/provisionalBill.ts); [frontend/src/lib/offers.ts](../../frontend/src/lib/offers.ts); [reports/Buy and sell workflow review.md](../../reports/Buy%20and%20sell%20workflow%20review.md)
- **Offline bill numbers:** the number is assigned on the server when the op syncs (`NextBillNumberAsync`), so an offline receipt has no final invoice number. That is inferred from the code path; I did not run it. — [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs)
- **Catalogue size:** `useProducts` downloads the whole `/v1/products` list, and `StockService.ListAsync` returns every product with no paging. Barcode lookup is a linear `products.find` in memory. — [frontend/src/lib/data.ts](../../frontend/src/lib/data.ts) line ~77; [backend/src/GroceryKhata.Api/Catalogue/StockService.cs](../../backend/src/GroceryKhata.Api/Catalogue/StockService.cs) line ~18

### Inferences
- For one or two counters the checkout is close to POS grade. For 5–20 tills in one shop, the shop-wide bill counter row, held as a lock for the whole bill transaction, is the main scaling risk. Taking the number in a short separate step, or using per-counter series such as `C1/2026-27/0001`, would remove it. Size: **Medium** (it touches GST numbering rules and the export's first/last bill-number checks).
- Downloading the whole catalogue and matching barcodes linearly is probably fine at a few thousand SKUs. At 20–50k supermarket SKUs it needs an indexed local store (an IndexedDB index on barcode) and a delta sync. Size: **Medium**.
- Real terminals (a registered device, a per-terminal series, server-side held bills) are one coherent **Medium–Large** piece of work.

### Gaps
- I took no measurements of bill-save time with many lines, or with 20k products loaded.
- I did not check whether a scanner's Enter suffix clashes with "Enter adds best match" for codes shorter than 8 digits.

## 2. Counter operations: cashier PIN, shifts, cash, drawer, printing, customer display

### Takeaway
Staff PIN login and a day-end cash count exist. The cash count covers the whole shop's drawer per day, not one till per shift. Receipts print through the browser dialog or through Bluetooth ESC/POS raster. Cash-drawer kick, shift open/close and a customer-facing display are all missing.

### Cited Findings
- **Cashier login with PIN: Has.** The owner adds staff with phone plus a 4–6 digit counter PIN. Easy PINs are refused (`Pins.Problem`). Staff sign in with phone and PIN, and a new PIN clears a lockout. — [backend/src/GroceryKhata.Api/Identity/Pins.cs](../../backend/src/GroceryKhata.Api/Identity/Pins.cs); [backend/src/GroceryKhata.Api/Shops/StaffEndpoints.cs](../../backend/src/GroceryKhata.Api/Shops/StaffEndpoints.cs) (lines ~23–35, 122–139)
- **App lock and bill passcode: Has.** There is an app passcode, and a passcode guards editing and cancelling bills (`RequireBillPasscode` on credit-note creation). — [frontend/src/lib/appLock.ts](../../frontend/src/lib/appLock.ts); [backend/src/GroceryKhata.Api/Shops/BillPasscode.cs](../../backend/src/GroceryKhata.Api/Shops/BillPasscode.cs); [backend/src/GroceryKhata.Api/Ledger/CreditNotes.cs](../../backend/src/GroceryKhata.Api/Ledger/CreditNotes.cs) line ~112
- **Fast cashier switching on a shared till:** I found no quick in-session "switch cashier by PIN". Signing in is per device session, so this is unverified.
- **Shifts: Lacks.** The "shift" hits in the code are for dairy morning/evening collection and salon staff. There is no cashier shift entity. — grep across `backend/src/GroceryKhata.Api` (Dairy.cs, DairyChecks.cs)
- **Opening cash and day-end count: Partly.** `CashCount` has one `Day` with Opening, Expected, Counted and Difference. Every recount is kept, and the next day opens with the last count. It has no counter or user dimension: the comment calls it "the evening count of the cash box (galla)". — [backend/src/GroceryKhata.Api/Ledger/CashCounts.cs](../../backend/src/GroceryKhata.Api/Ledger/CashCounts.cs)
- **Per-till day summary: Partly.** `DayReport` groups bills by `(Counter, By)` into a `TillDto` showing bills, sales, cash, UPI, card, bank, khata and cancellations. The code comment says "so two phones billing at once can close separately". The cash count itself is still shop-wide. — [backend/src/GroceryKhata.Api/Ledger/DayReport.cs](../../backend/src/GroceryKhata.Api/Ledger/DayReport.cs) (lines ~26–66); [frontend/src/screens/DayReportScreen.tsx](../../frontend/src/screens/DayReportScreen.tsx)
- **Cash movements and limits:** files exist for cash in/out moves and cash limits. — [backend/src/GroceryKhata.Api/Ledger/CashMoves.cs](../../backend/src/GroceryKhata.Api/Ledger/CashMoves.cs); [backend/src/GroceryKhata.Api/Ledger/CashLimits.cs](../../backend/src/GroceryKhata.Api/Ledger/CashLimits.cs)
- **Cash drawer kick: Lacks.** There is no ESC p (0x1B 0x70) or drawer command in `escpos.ts` or `btPrinter.ts`. — [frontend/src/lib/escpos.ts](../../frontend/src/lib/escpos.ts); [frontend/src/lib/btPrinter.ts](../../frontend/src/lib/btPrinter.ts)
- **Receipt printing: Partly.** Bluetooth BLE thermal printers work through Web Bluetooth (not on iPhone). The receipt is drawn as a raster image so Hindi and ₹ print, on 58 mm (384 dots) or 80 mm (576 dots) paper. USB and LAN printers only work through the browser print page (Ctrl+P), and there is no silent printing. — [frontend/src/lib/btPrinter.ts](../../frontend/src/lib/btPrinter.ts); [frontend/src/lib/escpos.ts](../../frontend/src/lib/escpos.ts) `dotsFor`, `drawReceipt`; [frontend/src/components/PrintPage.tsx](../../frontend/src/components/PrintPage.tsx); [frontend/src/lib/paper.ts](../../frontend/src/lib/paper.ts)
- **Payment: Partly.** A bill can be split across up to 4 parts of cash, UPI or card, and a UPI part can carry its UTR. The UPI QR shows the exact amount. I found no card-terminal or UPI soundbox integration, so card and UPI are recorded by hand. — [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs) (lines ~204–214); [frontend/src/screens/BillScreen.tsx](../../frontend/src/screens/BillScreen.tsx) (line ~1230)
- **Customer display (second screen or pole display): Lacks.** A grep found nothing.

### Inferences
- A supermarket needs cashier shifts with an opening float per till, a blind close and over/short per cashier. The data is half there: `Counter` and the creator are on every bill. A `Shift` entity tied to the counter and user, with `CashCount` keyed to the shift, is **Medium**.
- A drawer kick needs a USB printer path. A browser can only send raw ESC/POS through WebUSB, Web Serial or a local print helper. Adding the kick byte to the Bluetooth path alone is **Small**. Silent USB printing plus the kick is **Medium**, and may need a small local agent.
- A customer display could be a second browser window showing the cart through BroadcastChannel. **Small–Medium.**

### Gaps
- I did not check whether `PrintLook` supports a printed GST invoice layout for thermal paper with HSN rows. — [backend/src/GroceryKhata.Api/Shops/PrintLook.cs](../../backend/src/GroceryKhata.Api/Shops/PrintLook.cs) exists, but I did not read it in full.

## 3. Pricing and promotions

### Takeaway
The MRP cap, wholesale price by minimum quantity, timed percent or rupee sale offers, a staff discount cap and price-tag or barcode label printing are all built and enforced on the server. Combo or buy-X-get-Y offers, slab or bill-value promotions, loyalty points and gift vouchers do not exist.

### Cited Findings
- **MRP enforcement: Has.** The server rejects a line priced above `product.Mrp` and cites Legal Metrology. — [backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs](../../backend/src/GroceryKhata.Api/Ledger/LedgerService.Bills.cs) (lines ~73–77)
- **Wholesale price: Has.** `WholesalePrice` and `WholesaleMinQty` live on Product. The client moves the line price across the threshold (`priceFor`/`fitPrice`), and the server lets staff sell at that price. — [backend/src/GroceryKhata.Api/Catalogue/Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) line ~122; [frontend/src/lib/wholesale.ts](../../frontend/src/lib/wholesale.ts); LedgerService.Bills.cs lines ~80–83
- **Sale offers: Has, but only simple ones.** `SaleOffer` is Percent or Amount off each piece, scoped to all items, a category or chosen items, with start and end dates and an early stop. The bill applies the best running offer automatically (`bestOffer`), and the server re-checks it (`SaleOffers.Covers`, `PriceFor`). Each offer reports its bills, quantity, sales and amount saved. — [backend/src/GroceryKhata.Api/Ledger/SaleOffers.cs](../../backend/src/GroceryKhata.Api/Ledger/SaleOffers.cs); [frontend/src/lib/offers.ts](../../frontend/src/lib/offers.ts); [frontend/src/screens/OffersScreen.tsx](../../frontend/src/screens/OffersScreen.tsx)
- **Combo, buy-X-get-Y, mix-and-match, quantity slab, bill-value slab: Lacks.** A grep for combo, bogo, buyX or slab found nothing related to promotions. The "slab" hits are garment GST and meter readings. Offers only support `Percent` and `Amount`. — SaleOffers.cs `SaleOfferKind`
- **Bill discount and staff limits: Has.** Staff are capped at `StaffMaxDiscountPercent` (default 10%) through `DiscountProblem`; the owner is uncapped. `ListPrice` keeps a record of every discount, and there is a discounts report. The F9 key gives a bill discount. — [backend/src/GroceryKhata.Api/Shops/Shop.cs](../../backend/src/GroceryKhata.Api/Shops/Shop.cs) line ~59; LedgerService.Bills.cs lines ~27, 80–84, 101; [backend/src/GroceryKhata.Api/Ledger/Reports.cs](../../backend/src/GroceryKhata.Api/Ledger/Reports.cs) `/reports/discounts`; [frontend/src/screens/StaffCapSetting.tsx](../../frontend/src/screens/StaffCapSetting.tsx)
- **Loyalty points: Lacks.** A grep for "loyalty" found nothing. The earlier Vyapar review listed it as build item #16, Medium. — [reports/Vyapar hands-on gap review 2.md](../../reports/Vyapar%20hands-on%20gap%20review%202.md)
- **Gift vouchers: Lacks** as a sellable or redeemable tender. Prepaid and salon cards exist for service trades. — [backend/src/GroceryKhata.Api/Ledger/Prepaid.cs](../../backend/src/GroceryKhata.Api/Ledger/Prepaid.cs); [backend/src/GroceryKhata.Api/Ledger/SalonCards.cs](../../backend/src/GroceryKhata.Api/Ledger/SalonCards.cs) (I did not read these in depth)
- **Shelf and barcode label printing: Has.** It supports A4 sticker sheets with 65, 40 or 24 labels and thermal rolls of 50×25 or 38×25 mm. Barcodes print as EAN-13 or Code 128, and the server makes shop-own `2…` codes. — [frontend/src/lib/labels.ts](../../frontend/src/lib/labels.ts); [frontend/src/lib/barcode.ts](../../frontend/src/lib/barcode.ts); [frontend/src/screens/TagsScreen.tsx](../../frontend/src/screens/TagsScreen.tsx)
- **Price change history:** I found no dedicated price-history table outside the generic audit log, and no "reprint labels for items whose price changed" flow. — [backend/src/GroceryKhata.Api/Persistence/Audit.cs](../../backend/src/GroceryKhata.Api/Persistence/Audit.cs)

### Inferences
- A promotion engine with combo, BXGY and slabs on quantity or bill value needs to run the same way in the browser for offline billing and in the server check. **Large.**
- Loyalty points, with earn and burn tied to the mobile number on the bill, are **Medium**.
- Gift vouchers can reuse the exchange-slip design: a numbered note with a code, a balance, an expiry and an atomic spend. **Medium.**
- A price-change batch with its label reprint queue is **Small–Medium**.

### Gaps
- I did not verify whether the bulk item update screen exists. Vyapar review build item #13 asked for it.

## 4. Inventory

### Takeaway
Inventory is broad for a small shop. It has variants (style, size and colour), batches with expiry, godowns with transfers, purchase orders, goods receipts, supplier returns, stocktake and reorder signals. All of it sits inside a single shop.

### Cited Findings
- **Variants and sizes: Has.** Each Product has a `Size` and `Colour`, grouped into a style. Stock can be received by size grid, and there is a sizes sold/left report. — [backend/src/GroceryKhata.Api/Catalogue/Product.cs](../../backend/src/GroceryKhata.Api/Catalogue/Product.cs) (lines ~135–142); [frontend/src/screens/SizeReportScreen.tsx](../../frontend/src/screens/SizeReportScreen.tsx); [frontend/src/lib/styles.ts](../../frontend/src/lib/styles.ts)
- **Batches and expiry: Has.** You can receive a batch, list batches expiring soon, and the bill allocates from the batch that expires first. Expired stock stops the bill. The comment describes this for "medicines kept by batch". — [backend/src/GroceryKhata.Api/Catalogue/Batches.cs](../../backend/src/GroceryKhata.Api/Catalogue/Batches.cs); LedgerService.Bills.cs line ~360
- **Godowns and stock transfer: Partly.** A `Godown` and a numbered `StockTransfer` record exist. Stock is still "one number for the whole shop". A godown holds part of it, and bills sell from the shop shelf. There is no transfer between separate stores. — [backend/src/GroceryKhata.Api/Catalogue/Godowns.cs](../../backend/src/GroceryKhata.Api/Catalogue/Godowns.cs)
- **Purchase orders: Has.** Create, update, place, close, cancel, import, and a register. — [backend/src/GroceryKhata.Api/Ledger/PurchaseOrders.cs](../../backend/src/GroceryKhata.Api/Ledger/PurchaseOrders.cs)
- **GRN or goods receipt: Has.** Includes cancellation and a short-supply claim. Goods are counted against the PO. — [backend/src/GroceryKhata.Api/Ledger/GoodsReceipts.cs](../../backend/src/GroceryKhata.Api/Ledger/GoodsReceipts.cs); [backend/src/GroceryKhata.Api/Ledger/Suppliers.cs](../../backend/src/GroceryKhata.Api/Ledger/Suppliers.cs) (line ~812)
- **Supplier bills, landed cost and returns: Has.** — Suppliers.cs (landed cost, around line 358); [backend/src/GroceryKhata.Api/Ledger/SupplierReturns.cs](../../backend/src/GroceryKhata.Api/Ledger/SupplierReturns.cs); [backend/src/GroceryKhata.Api/Ledger/PurchaseUploads.cs](../../backend/src/GroceryKhata.Api/Ledger/PurchaseUploads.cs)
- **Stock counts: Has.** Stocktake runs item by item. Each difference becomes a "count fix" adjustment, the operation is idempotent by stocktake ID, and the history is kept. — [frontend/src/screens/StocktakeScreen.tsx](../../frontend/src/screens/StocktakeScreen.tsx)
- **Reorder: Has.** There is a `ReorderLevel`, stock health (rate per day, days on the shelf, ageing buckets) and stock alerts. Recent commits add supplier case size and minimum order, and skip items that never sell. — [backend/src/GroceryKhata.Api/Catalogue/StockHealth.cs](../../backend/src/GroceryKhata.Api/Catalogue/StockHealth.cs); [backend/src/GroceryKhata.Api/Ledger/StockAlerts.cs](../../backend/src/GroceryKhata.Api/Ledger/StockAlerts.cs); git log `174ae35`, `92cae5f`
- **Stock holds or reservations: Has.** — [backend/src/GroceryKhata.Api/Ledger/StockHolds.cs](../../backend/src/GroceryKhata.Api/Ledger/StockHolds.cs)

### Inferences
- For one supermarket outlet the inventory side is mostly there. What is missing is scale: partial stocktakes per zone run by several counters at once, and a cycle-count schedule. **Small–Medium.** Stock by location within the shop is a separate **Medium** item.
- Batch and expiry handling is built for pharmacy. It would need checking for FMCG items with a best-before date.

### Gaps
- I did not test stock movement throughput at high bill rates or the effect of negative stock. Commit `cb295d8` mentions "items below zero" reporting.

## 5. Returns, exchanges, credit notes and slips across counters

### Takeaway
This is the most POS-ready area. The design in the earlier exchange-slip report has been built on the server: a numbered CN or RN with a secret 4-digit code or QR, an expiry, a cap on what staff may accept, customer binding, and an atomic spend. The exception is that returns cannot be done offline.

### Cited Findings
- **Credit notes: Has.** They are numbered `CN/<FY>/<n>`, or `RN/…` when no GST applies. Each records the reason and whether stock went back on the shelf. A refund goes to the khata or out of the drawer by cash, UPI, card or bank. Creation requires the bill passcode. — [backend/src/GroceryKhata.Api/Ledger/CreditNotes.cs](../../backend/src/GroceryKhata.Api/Ledger/CreditNotes.cs)
- **Exchange slip redeemable at any counter: Has.** A slip is found by number plus code or by the QR `GKSLIP:<id>:<code>`. Without the code the answer is "not found". It can be listed, given a new code (with a phone check) or cancelled. Validity is 90 days by default (`Shop.ExchangeSlipDays`). — [backend/src/GroceryKhata.Api/Ledger/ExchangeSlips.cs](../../backend/src/GroceryKhata.Api/Ledger/ExchangeSlips.cs); [frontend/src/screens/ExchangeSlipsScreen.tsx](../../frontend/src/screens/ExchangeSlipsScreen.tsx); BillScreen.tsx (lines ~326, 576–578)
- **Atomic spend: Has.** `ExecuteUpdateAsync` runs only `WHERE Spent == exchange.Spent AND Spent + credit <= Total`. If no row changes, the bill fails, so two counters cannot spend the same slip. The slip must belong to the bill's customer, and staff need the owner above `ExchangeSlipHelperMax` (default ₹5,000). — LedgerService.Bills.cs (lines ~119–146, 341–342); Shop.cs line ~77
- **Leftover value:** a credit larger than the bill stays on the slip ("no cash change"), per the comment around line 145. A separate branch records an `Expense` refund when `exchange.Total > total` (line ~273). The two comments seem to disagree. I have not verified which behaviour wins at run time.
- **Offline returns: Lacks.** Credit notes and slips are not in `OutboxKind`. — [frontend/src/lib/outbox.ts](../../frontend/src/lib/outbox.ts)
- **The design brief this builds on:** [reports/Exchange slips across billing counters.md](../../reports/Exchange%20slips%20across%20billing%20counters.md)

### Inferences
- Cross-counter exchange inside one store is done. Using slips across stores needs the multi-store work in section 6.

### Gaps
- The possible double handling of leftover credit (stays on the slip, or a cash refund expense) needs a test.

## 6. Multi-store, head office, reports, user roles

### Takeaway
Each shop is a separate tenant. A user can belong to several shops and switch between them, but nothing works across shops: no consolidated reports, shared catalogue, central pricing or inter-store transfer. Reporting for a single store is rich. Roles are coarse: Owner, Staff and Accountant, plus per-staff permission blocks.

### Cited Findings
- **Multi-shop: Partly.** "Everyone with more than one shop can switch here." The session holds one `shopId` at a time. Tenant isolation uses `app.shop_id` in Postgres (see the SQL in `DocumentNumbers`). — [frontend/src/components/AccountMenu.tsx](../../frontend/src/components/AccountMenu.tsx) (lines ~29–32); [backend/src/GroceryKhata.Api/Identity/TenantMiddleware.cs](../../backend/src/GroceryKhata.Api/Identity/TenantMiddleware.cs); [backend/src/GroceryKhata.Api/Persistence/DocumentNumbers.cs](../../backend/src/GroceryKhata.Api/Persistence/DocumentNumbers.cs)
- **Head-office or chain control: Lacks.** A grep for branch, head office or chain found no such feature. — grep across backend and frontend
- **Roles: Partly.** `ShopRole` is Owner, Staff or Accountant (read-only). `StaffLimits` can block a helper from udhaar, reports, purchases, items, expenses or cancel. There is no store manager, supervisor or cashier hierarchy, and no manager-PIN override for a single action. Bill cancellation uses the bill passcode instead. — [backend/src/GroceryKhata.Api/Identity/IdentityEntities.cs](../../backend/src/GroceryKhata.Api/Identity/IdentityEntities.cs) (lines ~93–100); [backend/src/GroceryKhata.Api/Identity/StaffLimits.cs](../../backend/src/GroceryKhata.Api/Identity/StaffLimits.cs); [backend/src/GroceryKhata.Api/Identity/ReadOnlyAccess.cs](../../backend/src/GroceryKhata.Api/Identity/ReadOnlyAccess.cs)
- **Day report: Has.** It shows every bill, cancelled bills separately, money by mode compared with yesterday, items sold, the drawer close and per-till rows. — [backend/src/GroceryKhata.Api/Ledger/DayReport.cs](../../backend/src/GroceryKhata.Api/Ledger/DayReport.cs)
- **Sales by item, category or party: Has.** `/reports/profit-by` takes a grouping of `party`, `bill` or `category`. There are also sales insights by range, `items.csv`, month, year, P&L, staff sales and commission, item margins, discounts and dues. — [backend/src/GroceryKhata.Api/Ledger/Reports.cs](../../backend/src/GroceryKhata.Api/Ledger/Reports.cs) (lines ~209–232, 489); [backend/src/GroceryKhata.Api/Ledger/SalesInsights.cs](../../backend/src/GroceryKhata.Api/Ledger/SalesInsights.cs) (lines ~52–54)
- **GST exports: Has.** A month summary with checks, a Tally import `export.zip`, `sales-register.csv` (which includes the counter and the user) and Tally ledger mapping. GSTR-3B/CMP-08 working and the GSTR-1 HSN table (Table 12) are net of credit notes. — [backend/src/GroceryKhata.Api/Ledger/GstExport.cs](../../backend/src/GroceryKhata.Api/Ledger/GstExport.cs) (lines ~51–55, 562); [backend/src/GroceryKhata.Api/Ledger/GstReturns.cs](../../backend/src/GroceryKhata.Api/Ledger/GstReturns.cs)
- **E-invoicing (IRN):** a grep for "IRN" or e-invoice matched three files: GstReturns.cs, InvoicePhotos.cs and Reports.cs. I did not confirm that real IRN generation exists. An e-way bill helper exists. — [frontend/src/lib/ewaybill.ts](../../frontend/src/lib/ewaybill.ts)

### Inferences
- A supermarket chain needs an "organisation" level above shops: a shared item master, price lists per store, consolidated reports, inter-store transfers with stock in transit, and slips valid at any store. This is **Large**, because row-level tenancy is keyed on one `shop_id`.
- Splitting roles into Cashier, Supervisor and Manager, with a supervisor PIN for voids, refunds, price overrides and drawer-open, is **Medium**.

### Gaps
- I did not confirm whether a GSTR-1 portal JSON export exists. The Vyapar review listed it as #14. I saw only the HSN table and the 3B working.

## 7. Mall tenant sales reporting

### Takeaway
The app has owner-only CSV exports for one day or one month, but no machine API for a mall's system to pull from and no scheduled automatic push. Mall daily-sales (GTO) integrations normally require one or the other. The building blocks exist: DayReport totals by mode and per till, a sales register CSV and an audit trail of cancellations.

### Cited Findings
- **Sales exports: Partly.** `sales-register.csv` (GST feature), `items.csv`, `month.csv`, `year.csv` and `pnl.csv` all require an owner JWT. — [backend/src/GroceryKhata.Api/Ledger/GstExport.cs](../../backend/src/GroceryKhata.Api/Ledger/GstExport.cs) line ~53; [backend/src/GroceryKhata.Api/Ledger/Reports.cs](../../backend/src/GroceryKhata.Api/Ledger/Reports.cs) (lines ~215–228); [backend/src/GroceryKhata.Api/Ledger/SalesInsights.cs](../../backend/src/GroceryKhata.Api/Ledger/SalesInsights.cs) line ~53
- **API keys or tenant-to-mall integration: Lacks.** "ApiKey" and webhook hits are only for outside services the app calls (Claude, Razorpay, WhatsApp), not for third parties calling in. — grep result: ClaudeJson.cs, RazorpayCheckout.cs, WhatsAppOrders.cs
- **Scheduled jobs exist** and could host a nightly push. — [backend/src/GroceryKhata.Api/Common/JobRuns.cs](../../backend/src/GroceryKhata.Api/Common/JobRuns.cs) (not read in depth)
- **Bill integrity:** cancelled bills are never hidden in the day report, there is an audit log, and bill numbers have no gaps (the number is taken inside the transaction). These properties support honest turnover reporting. — DayReport.cs; Audit.cs; LedgerService.Bills.cs `NextBillNumberAsync` comment
- **What malls require (external, to confirm the meaning only):** mall rent is often tied to gross turnover (GTO) reported daily, and integrations send each day's sales automatically, for example at 00:00 the next day — [Shopline GTO](https://help.shopline.com/hc/en-001/articles/18237228352537-Automated-GTO-Transmission-for-Malls); [EasyStore](https://blog.easystore.co/en-my/shopping-mall-gto). Indian POS vendors market "ADSR" (automated daily sales reporting) integrations to mall management — [QueueBuster ADSR](https://queuebuster.co/pos-software-partners/adsr); [VasyERP malls](https://www.vasyerp.com/retailing/pos-software-shopping-malls). Some providers say malls mostly pull the data rather than tenants pushing it — [Mallsense](https://project7421800.tilda.ws/mallsense-sales-data-capture). These are vendor pages, not lease standards.

### Inferences
- A generic "mall export" would need three things: a read-only API token scoped to one shop, per-bill and per-day sales endpoints (gross, net of returns, tax and tender), and a nightly push adapter (SFTP, HTTP or email) with a format per mall. **Medium** for the generic part, plus **Small** for each mall format.

### Gaps
- I found no source on how common or how standardised GTO reporting clauses are in Indian mall leases.

## 8. Performance and scale: the load test

### Takeaway
The only load test simulates 25 shops with 2 cashiers each. The latest recorded runs pass on a laptop, at about 30 req/s overall and a bill p95 around 82 ms. That works out to roughly 13–14 bills a second across all shops, but only about half a bill a second per shop. No test drives 10 or more counters in one shop, which is exactly where the shop-wide bill counter lock would matter.

### Cited Findings
- **The script:** `loadtest/evening-rush.mjs` signs up N shops (default 25), each with 40 products and 25 customers. Each cashier (default 2) loops with a random 1–2 s pause. 45% of actions are a bill of 1–5 lines, 40% of them on khata. The rest are khata entries (20%), the customer list (15%), popular items (10%), statements (5%) and today's dashboard (5%). The pass mark is p95 under 500 ms with under 1% errors. — [loadtest/evening-rush.mjs](../../loadtest/evening-rush.mjs)
- **Recorded results** (Windows laptop, API and Postgres on one box). Before the fix: 26.6 req/s, p95 1137 ms, fail. After one connection per request: 25×2 gave 29.1 req/s and failed, with bill p99 at 3.5–5.9 s, while 10×2 passed at 12.3 req/s. On 29 Oct, after batching a bill's stock update: 25×2 gave 30.3 req/s, 76 ms overall and 82 ms for bills, a pass, with a p99 tail of 1.5–4 s blamed on the laptop. A repeat run swung from 75 to 562 ms. The runbook says to "decide the server size from a staging run only", and the chosen start size is KVM 2 (2 vCPU, 8 GB). — [docs/runbook.md](../../docs/runbook.md) (lines ~73–96)
- **Bills per minute (my arithmetic from the above):** 30 req/s × 45% ≈ 13.5 bills/s ≈ 800 bills a minute across 25 shops, or about 32 bills a minute per shop from 2 cashiers. The rate is limited by the 1–2 s pause, not by measured capacity. — derived from [loadtest/evening-rush.mjs](../../loadtest/evening-rush.mjs) and [docs/runbook.md](../../docs/runbook.md)

### Inferences
- A busy supermarket might run 10–20 tills and 1–3 bills a minute per till, with 20–60 lines per bill. That is a different shape from this test: many concurrent bills in one shop, all contending for the same `bill_counters` row and the same popular products' stock rows. A test with `--shops 1 --cashiers 20`, larger bills and a 20k-product catalogue is needed before claiming supermarket readiness. Writing it is **Small**. Fixing whatever it finds could be **Medium**.

### Gaps
- No staging or VPS results are recorded, and no single-shop many-counter run exists.

## Summary table (for the report writer)

| Capability | Status | Size of missing work |
|---|---|---|
| USB scanner, camera scan | Has | – |
| Several barcodes per item | Lacks | Small |
| Serial/USB scale (desktop Chrome) | Has | – |
| Weight or price embedded barcodes (PLU labels) | Lacks | Small–Medium |
| Keyboard-first desktop billing | Has | – |
| Held bills | Partly (device-only) | Small–Medium |
| Many counters: numbering and concurrency | Partly (one shop-wide series; lock held for the whole bill) | Medium |
| Registered terminals or counters | Partly (free-text label) | Medium |
| Offline billing and outbox | Has (returns online-only; serial flush) | Small–Medium |
| Large catalogue (20k+ SKUs) on device | Partly (full download, linear match) | Medium |
| Cashier PIN login | Has | – |
| Shifts, per-till float and close | Lacks (day-level shop count; per-till sales only) | Medium |
| Cash drawer kick | Lacks | Small (BT) / Medium (USB) |
| Thermal receipt | Partly (BT raster; USB through the print dialog) | Medium for silent USB |
| Customer display | Lacks | Small–Medium |
| Card terminal or UPI soundbox integration | Lacks (recorded by hand) | Medium per provider |
| MRP cap, wholesale price, staff discount cap | Has | – |
| Timed % or ₹ sale offers | Has | – |
| Combo, BXGY, slab promos | Lacks | Large |
| Loyalty points | Lacks | Medium |
| Gift vouchers | Lacks | Medium (reuse slip model) |
| Label and price-tag printing | Has | – |
| Price change batch and label reprint | Lacks | Small–Medium |
| Variants, batches and expiry, godowns, PO, GRN, stocktake, reorder | Has (single store) | – |
| Returns, CN, cross-counter exchange slips | Has (online only) | Small for offline |
| Multi-store head office | Lacks (switch-only) | Large |
| Granular roles and supervisor override | Partly | Medium |
| Day report, category reports, GST and Tally exports | Has | – |
| Mall GTO export API or push | Lacks (manual CSVs only) | Medium |
| Load test at supermarket shape | Lacks (25 shops × 2 tills only) | Small to write |
