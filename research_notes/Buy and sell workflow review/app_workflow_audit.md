# Grocery Khata buy and sell workflow audit (code audit, read-only)

Paths below are relative to the repo root. Tap counts are estimates from reading JSX, not from running the app (the dev servers were not started).

## Shopkeeper selling: bill, udhaar, online orders

### Takeaway
Writing a bill is fast and works offline: a tap on a tile adds the item, and saving goes into the IndexedDB outbox with no wait for the network. A 5-item cash bill takes about 7 taps. Accepting a customer order needs about 4 to 6 taps across 2 to 3 online round trips, all blocking, with no offline path.

### Cited Findings
- Bill screen: a search box sits on top of a 3-column grid of product tiles. One tap adds qty 1 (`frontend/src/screens/BillScreen.tsx:122-141`). +/- buttons per line (`BillScreen.tsx:212-214`). Payment-mode chips (`BillScreen.tsx:272`). Save button (`BillScreen.tsx:279`). After saving, "view bill" goes to `/print/bill/:id` (`BillScreen.tsx:176`).
  - 5-item cash bill, each item qty 1, tiles visible: 5 tile taps + 1 save = 6 taps (7 if the mode must be switched to cash; the default mode was not checked). Each extra unit costs 1 more "+" tap. If an item is not among the visible tiles, add a search (typing plus 1 tap).
- Loose items and medicine strips open a prompt dialog through `ask(...)` (`BillScreen.tsx:90, 208`). That is about 2 extra steps per such line.
- `saveBill` only enqueues to the outbox and returns at once (`frontend/src/lib/data.ts:193-215`). `enqueue` then fires `flush` (`frontend/src/lib/outbox.ts:68-71`). So saving has zero network wait. Flushes are serialised by a single `flushing` promise (`outbox.ts:40, 106-153`).
- Udhaar: picking a customer on the bill records the ledger delta (`total - paidAmount`) in the outbox op (`data.ts:210-211`). That is about 2 to 3 extra taps (open customer picker, choose, set paid amount).
- Order handling (`frontend/src/screens/OrderScreen.tsx`): it first does a GET of the order (`:31`). "Pack" POSTs `/v1/orders/:id/pack` (`:83`). Then you pick Paid or On khata (`:192-193`). "Deliver" POSTs `/deliver` (`:94, 209`). Cancel goes through a confirm dialog (`:95`). Deliver stays disabled while a substitute approval is pending from the customer (`:55`).
  - Accepting an order with no edits: Inbox → tap order (1) → Pack (1) → Paid/Khata (1) → Deliver (1) = about 4 taps and 3 server round trips. None of these go through the outbox: they call `api()` directly through `run()`.
- New orders show up by polling every 30 s (`frontend/src/lib/orderAlerts.ts:36`, `frontend/src/screens/InboxScreen.tsx:16`). There is no push or websocket, so the average delay before an order is noticed is about 15 s and the worst case is 30 s.

### Inferences
- Pack and Deliver are separate steps. For walk-in or self-pickup orders, a single "Pack and deliver (paid)" button would save about 2 taps and 1 round trip.
- Order actions fail when offline, while bills keep working. Adding them to the outbox (OutboxKind already has `'order'`, `outbox.ts:4`) would make the two consistent.
- On the bill screen, a "repeat last bill" button and a qty keypad that opens on long-press would cut taps for bills with several units per item.

### Gaps
- I did not check which payment mode is selected by default, or how the customer picker is laid out, so the udhaar tap count is approximate.

## Shopkeeper buying and stock

### Takeaway
Items come in by bulk-picking from the master list (PickItemsScreen) or by importing. Supplier purchases work online only, but can be filled from a photo of the invoice using AI. The master list is drawn without virtualization.

### Cited Findings
- PickItemsScreen: trade chips → category chips/tiles → rows to multi-select (`frontend/src/screens/PickItemsScreen.tsx:190-220`). Search matches by word prefix (`:33-34`). Pack sizes are chosen per item (`:125`). Images use `loading="lazy"` (`:117, 205`). Master price updates are applied in bulk through `/v1/master-items/updates/apply` (`:140`). Adding N items takes about 3 + N taps.
- Rows are drawn with plain `.map` (`PickItemsScreen.tsx:220`). No virtualization library is installed: `frontend/package.json` lists only react-query, i18next, idb and qrcode, and a grep for "virtual" finds only `lib/pwa.ts`. Several derived sets are recomputed with `filter` and `find` scans on every render (`:52, 75, 83`). The tile computation at `:83` is O(categories × items).
- Suppliers (`frontend/src/screens/SuppliersScreen.tsx`): list → open supplier (`:56`) → purchase form. There is also the invoice-photo path: AI consent (`:189-196`), then the photo is shrunk to 1600 px and POSTed to `/v1/suppliers/purchases/photo` (`:95`), then the draft is reviewed and saved with POST `/v1/suppliers/purchases` (`:134`). Paying a supplier is a direct POST (`:153`). All of these are direct `api()` calls, not the outbox, so they fail when offline.
- Stock writes do go through the outbox (`OutboxKind 'stock'`, `outbox.ts:4`). The backend has a StockMovement index on (ShopId, ProductId, OccurredAt) (`backend/src/GroceryKhata.Api/Persistence/AppDbContext.cs:1131`).

### Inferences
- On a 1000-row master list, drawing every row plus the O(n·m) memo-less scans could cost noticeable jank on low-end Android. I estimate 100 to 300 ms per keystroke, but I did not measure it. A windowed list or deferred rendering (`useDeferredValue`) would fix it.

### Gaps
- I did not open StocktakeScreen, ImportScreen or ItemsScreen in detail, so their tap counts are not covered.

## Customer flow

### Takeaway
The customer flow is: pick a shop → build the list from aisles → press Send. Order tracking needs a manual refresh, except for the substitute approve/decline prompts.

### Cited Findings
- ListScreen: pick or change the shop (`frontend/src/screens/planner/ListScreen.tsx:82-94`) → open an aisle → tap an item to add it (`:124, 169`) → +/- (`:209-211`) → Send navigates to `/send/:kind` (`:228`). A 5-item list takes about 1 (shop) + 1 (aisle) + 5 + 1 (send) + the confirm step on the send page = about 9 taps.
- The catalogue is cached for 1 h (`frontend/src/lib/planner.ts:211`, `CommonListScreen.tsx:26`). The default staleTime is 30 s with `networkMode: 'always'` and `retry: false` (`frontend/src/main.tsx:14`).
- MyOrdersScreen offers OK / Leave out buttons for substitutes (`frontend/src/screens/planner/MyOrdersScreen.tsx:82-83`). I found no refetchInterval in that file, so status changes appear only after a refetch.

### Inferences
- If the customer picked a shop last time, it could be pre-selected and Send could go straight through, saving 1 to 2 taps.

### Gaps
- I did not read the send page or the khata view for customers.

## Latency sources

### Takeaway
The worst server hotspot is `GET /v1/catalogue`. On every request it loads two whole tables into memory and runs a nested linear match that includes a `Contains` string scan. Other latency sources are polling, uncached images, and the master list drawn without virtualization. Routes are already lazy-loaded.

### Cited Findings
- `backend/src/GroceryKhata.Api/Catalogue/CatalogueEndpoints.cs:35-47`: loads all CatalogueItems plus all MasterItems that have photos. Then, for each item, it does up to 3 `FirstOrDefault` scans, the last being `Name.Contains(..., OrdinalIgnoreCase)`. That is O(items × photos) per request with no server cache. With 1000 × 1000 rows the worst case is about 3M string comparisons per call. The endpoint has no OutputCache or response compression (grep of Program.cs found neither).
- `CatalogueEndpoints.cs:92` loads every active priced product for options.
- Program.cs:85-91 logs a "SLOW" warning for requests over the threshold, measured with a Stopwatch.
- Indexes exist for Bills (ShopId, OccurredAt) (`AppDbContext.cs:1154`), LedgerEntries (ShopId, CustomerId, OccurredAt) (`:1300`), Orders (ShopId, Status, CreatedAt) (`:396`), Products (ShopId, Name/Category/CatalogueItemId) (`:482-484`), and MasterItems Name (`:322`). There is no index on `MasterItems.CatalogueItemId`. That does not matter today only because the code matches in memory.
- Polling: orders every 30 s in three places (`orderAlerts.ts:36`, `InboxScreen.tsx:16`, `store/StoreApp.tsx:320`). That is about 120 requests per hour per open device, and possibly duplicated if Inbox and alerts run together.
- Bundle: about 45 screens are `React.lazy` in `frontend/src/App.tsx:16-60`. There is a `budget` script (`frontend/package.json:11`). The dependency set is small.
- Outbox: one serial flush with no batching (`outbox.ts:106-153`). N queued ops means N sequential round trips. After a day offline this means a slow catch-up.

### Inferences
- Caching the catalogue DTO in memory (invalidated when a master photo is saved), or precomputing `PhotoMasterId` in the database, would turn this endpoint into roughly an O(1) read. Adding an ETag would let clients skip the download entirely.
- A batch endpoint for the outbox (for example POST `/v1/sync/batch`) would cut catch-up time roughly N-fold.

### Gaps
- No runtime measurements were taken: no bundle sizes, no SLOW log contents, no row counts.

## Reporting

### Takeaway
There is a monthly summary, a P&L, a GST export, a financial-year summary, a cash book, dues, and some trade-specific registers. I found no daybook, stock or valuation report, item-wise margin, GSTR-ready returns view, or supplier ageing.

### Cited Findings
- ReportsScreen (`frontend/src/screens/ReportsScreen.tsx`) has month navigation (`:47-49`). It shows sales and bill count, cash, UPI, udhaar given, khata collected (`:57-65`), estimated or net profit with returns, stock loss and expenses (`:71-79`), stock value and supplier due (`:86-87`), outstanding (`:91`), a daily sales bar chart (`:97-100`), top items (`:110`), links to P&L and GST export (`:122-123`), and an FY turnover/GST/cash block (`:126-129`).
- Other report-like screens are routed in App.tsx: ProfitLossScreen, GstExportScreen, CashBookScreen, DuesScreen, PharmacyMonthScreen, H1RegisterScreen and ExpensesScreen (`App.tsx:30-58`). The home digest shows old dues and supplier due (`HomeScreen.tsx:23`).
- A grep for daybook, GSTR and "stock report" under the screens folder found no dedicated screen. The matches were incidental strings in other screens.

### Inferences
- Missing reports: a daily daybook (all bills, payments and expenses for one day), a stock-on-hand and low-stock report, item-wise margin, supplier ledger ageing, customer dues ageing buckets, staff-wise sales, and order fulfilment time.

### Gaps
- I did not read the server report endpoints or the GstExportScreen output format.
