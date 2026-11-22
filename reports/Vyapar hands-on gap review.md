# Vyapar hands-on gap review

Date: 17 Nov 2026. Branch reviewed: `feature/phase1-walking-skeleton`. Review only, no code changed.

## How much of Vyapar was actually seen

The hands-on part was **limited**. Vyapar Desktop opened on this PC, but nearly every click failed because the tool's safety check kept erroring ("no verdict"). One click got through and did not open the Sale menu. What was seen on screen:

- **Fresh company, nothing set up.** The header shows "Enter Business Name". The left panel shows "6 days Free Trial left" and a "Get Vyapar Premium" button. The company has no data.
- **Home screen is a first-sale wizard.** It is titled "Enter details to make your first Sale" and has Invoice Number 01, today's date, Customer Name (required), an "Add Sample Item" box, Invoice Amount, Received, and a green Balance strip. A "Create Your First Invoice" button stays greyed out until those are filled in. On the right is a live sample Tax Invoice preview with columns # / Item name / Qty / Price per unit / GST / Amount, then Amount in Words, Sub Total, SGST@9%, CGST@9%, Total and Balance Due. The tagline reads "1Cr Vyaparis have created invoices on Vyapar".
- **Left menu:** Open Anything (Ctrl+F), Home, Parties, Items (+), Sale, Purchase & Expense, Grow Your Business, Cash & Bank, Reports, Sync Share & Backup, Utilities, Settings, Plans & Pricing, My Company. The top bar has Company / Help / Versions / Shortcuts, plus a support phone number and "Get Instant Online Support".

Sub-screens (report list, settings pages, print themes) **could not be opened**. Their rows below come from Vyapar's standard desktop menu as it is publicly documented. They are marked *(menu, not seen)* and should be re-checked in a second session with working clicks. On a fresh trial company, Vyapar may also keep menus locked until the first invoice is saved, which this review was not allowed to do.

Lessons from what was seen:
1. **First-run onboarding goes straight to a bill.** Vyapar gets a new user to a printed invoice on the very first screen and shows a live preview beside the form. Grocery Khata starts on a Home list.
2. **Ctrl+F "Open Anything"** is a global jump to any screen, party or item.
3. The trial nag and premium banner are always visible. Grocery Khata's lack of ads and nags is still a selling point.

## Feature-by-feature comparison

Key: Yes = present, Partly = some of it, No = not found in code.

### Sale and billing
| Vyapar feature | Grocery Khata | Where / what is missing |
|---|---|---|
| Sale invoice (GST, non-GST), live preview | Yes | `BillScreen.tsx`, `TradeBill.tsx`, `PrintScreen.tsx`. No side-by-side live preview |
| Estimate / quotation, convert to sale | Yes | `EstimatesScreen.tsx` |
| Sale order | Yes | `SaleOrdersScreen.tsx` |
| Delivery challan, convert to invoice | Partly | Challan wording in `TradeBill.tsx`/`CustomerTrade.tsx`. Check whether there is a separate challan document and conversion |
| Sale return / credit note | Yes | `ReturnsScreen.tsx`, exchange slips, credit/RN return notes |
| Payment-in receipt | Yes | `ReceiptsScreen.tsx` |
| POS / quick counter billing, keyboard-first | Partly | Bill screen is mobile-first, with no desktop keyboard shortcut layer (F-keys, Ctrl+F) |
| Barcode scan at billing | Yes | `ScanSheet.tsx`, camera reader in `BillScreen.tsx:71` |
| Thermal print | Yes | `lib/escpos.ts`, `lib/btPrinter.ts` |
| PDF invoice share | Partly | Browser "Save as PDF" (`PrintScreen.tsx:26`). No one-tap generated PDF to WhatsApp |
| Round-off line | Partly | Present on the month statement (`MonthStatement.tsx:21`). Check the bill itself |
| Additional charges (freight, packing) on the invoice | Partly | Delivery charges exist. No general "extra charge" lines |
| Bill-wise discount and per-line discount | Yes | `DiscountsScreen.tsx`, `OffersScreen.tsx` |
| TCS / cess on invoice *(menu, not seen)* | No | Rare for kirana. Cess matters for tobacco/pan items |

### Purchase and expense
| Vyapar | GK | Notes |
|---|---|---|
| Purchase bill | Yes | Supplier bills, `GoodsInScreen.tsx` |
| Purchase order | Yes | `SupplierOrders.tsx`, `OrdersPlacedScreen.tsx`, `GoodsToComeScreen.tsx` |
| Purchase return / debit note | Partly | Check whether a numbered debit note document exists |
| Payment-out | Yes | Supplier page statement and payments |
| Expenses with categories | Yes | `ExpensesScreen.tsx`, `KharchaMonth.tsx` |
| Other income *(menu, not seen)* | No | Not found as its own entry type |

### Items and stock
| Vyapar | GK | Notes |
|---|---|---|
| Items, categories, units, unit conversion | Yes | `ItemsScreen.tsx`, `ProductScreen.tsx`, `CategoriesScreen.tsx`. Master lists go further than Vyapar |
| Batch / expiry / MRP | Yes | `LotsScreen.tsx`, `MedicineStockScreen.tsx` |
| Serial / IMEI | Yes | `SerialsScreen.tsx` |
| Godowns, stock transfer | Yes | `GodownsScreen.tsx` |
| Stock adjustment / stocktake | Yes | `StocktakeScreen.tsx` |
| Barcode label printing *(Utilities, not seen)* | Partly | No sheet/label printing layout (A4 38-up, 50x25 mm) found |
| Wholesale price / party-wise price list | Partly | `RatesScreen.tsx`. Check whether a minimum-quantity wholesale rate exists |
| Item-wise low-stock alert | Yes | `StockHealthScreen.tsx` |

### Parties
| Vyapar | GK | Notes |
|---|---|---|
| Customers/suppliers, opening balance, statement | Yes | `CustomersScreen.tsx`, supplier statement (commit 475066a) |
| Party groups, credit limit *(not seen)* | Partly | `TagsScreen.tsx` for grouping. Check credit-limit block |
| Payment reminders | Yes | `RemindersScreen.tsx`, `DuesScreen.tsx` (WhatsApp share) |
| Import parties from Excel/contacts | Yes | `CustomerImportScreen.tsx` |
| Loyalty points *(Grow Your Business)* | No | |

### Cash and bank
| Vyapar | GK | Notes |
|---|---|---|
| Cash in hand / cash book | Yes | `CashBookScreen.tsx`, `CashLedgerScreen.tsx` |
| Bank accounts, bank book, transfers | Yes | `BankBookScreen.tsx` |
| Cheques (open/closed) | Yes | `ChequesScreen.tsx` |
| Loan accounts with EMI/interest schedule | Partly | Loans are a balance-sheet line and an expense category only (`BalanceSheetScreen.tsx:12`). No loan ledger with EMIs |

### Reports
| Vyapar report | GK | Notes |
|---|---|---|
| Sale / purchase / day book / all transactions | Yes | `ReportsScreen.tsx`, `DayReportScreen.tsx` |
| Profit & loss, balance sheet | Yes | `ProfitLossScreen.tsx`, `BalanceSheetScreen.tsx` |
| Party statement, party-wise P&L, outstanding/ageing | Partly | Statements and dues exist. Check ageing buckets (0-30/31-60/60+) |
| Stock summary, item-wise P&L, low stock, stock detail | Yes | `StockStatementScreen.tsx`, `ItemMarginScreen.tsx`, `StockHealthScreen.tsx` |
| Sales by staff | Yes | `StaffSalesScreen.tsx`, `SalesByScreen.tsx` |
| GSTR-1, GSTR-3B, GSTR-2 / 2B reconciliation, GSTR-9 | Partly | `GstExportScreen.tsx` gives GSTR-1/3B/CMP-08 data and due dates. No GSTR-2B purchase match, no GSTR-9 |
| HSN summary, tax rate report | Partly | HSN is captured. Check that a standalone HSN summary page exists |
| Expense report, category-wise | Yes | Expenses/Kharcha |
| Export to Excel from every report | Partly | Export panel exists (`ExportImportPanel.tsx`), not on every report |

### GST
| Vyapar | GK | Notes |
|---|---|---|
| E-way bill JSON | Yes | `components/EwayBill.tsx`, `lib/ewaybill.ts` |
| E-invoice (IRN + QR via IRP) | No | Only needed above Rs 5 Cr turnover. Low priority for kirana |
| GSTR JSON upload file | Partly | Export zip only. Check whether the GSTR-1 JSON matches the offline-tool format |

### Settings, sync and utilities
| Vyapar | GK | Notes |
|---|---|---|
| Print themes, invoice field toggles, custom fields | Partly | One print layout. No theme picker or extra custom fields on the bill (vehicle no., PO no.) |
| Tax rates and groups | Yes | Per item/category |
| Multi-firm (several businesses, one login) | Yes | Multi-shop (`MyStoreScreen.tsx`, shop switch) |
| Users / roles with permissions | Yes | `TeamScreen.tsx`, `StaffScreen.tsx`, helper limits |
| Sync across devices | Yes | Cloud API plus offline outbox (`SyncScreen.tsx`) |
| Local backup to file / auto backup / restore | Partly | Data export exists. No "Backup now → file" and "Restore from file", and no last-backup indicator |
| Recycle bin / restore deleted bill | No | Reversals keep history, but there is no Recycle Bin screen |
| Tally export | Partly | See `reports/Tally export for paid shops.md` |
| Import items from Excel | Yes | `ImportScreen.tsx` |
| Online store / catalogue | Yes | `StoreApp.tsx` (GK is stronger) |
| Global search "Open Anything" (Ctrl+F) | No | No command palette |
| Keyboard shortcuts list | No | |
| First-sale onboarding with live invoice preview | No | |

## Ranked build plan (kirana, clothes and medical first)

1. **One-tap PDF bill to WhatsApp** (generated PDF, not "Print → Save"). Every shop type shares bills on WhatsApp. *Medium.*
2. **First-run "make your first bill" screen with a live invoice preview**, Vyapar's strongest observed pattern. *Small–medium.*
3. **Backup to file, restore from file, and a "last backed up" badge**, a trust gap rivals advertise. *Medium.*
4. **Barcode label printing** (A4 sheet and 50x25 thermal, with MRP, price and batch). Clothes and medical shops need it after purchase. *Medium.*
5. **Purchase return as a numbered debit note**, plus the GSTR-2B purchase match. Medical stores return expiry stock to the distributor all the time. *Medium.*
6. **Outstanding ageing report** (0-30/31-60/61-90/90+) for customers and suppliers. *Small.*
7. **Ctrl+F "Open Anything" search plus desktop keyboard shortcuts** for counter billing. *Small–medium.*
8. **Invoice print themes and custom bill fields** (vehicle no., PO no., doctor/patient for medical). *Medium.*
9. **Delivery challan as a document of its own, convertible to an invoice**, for wholesale and clothes on approval. *Small–medium.*
10. **Recycle bin for cancelled bills**, with restore. *Small.*
11. Excel export button on every report. *Small.*
12. Additional-charge lines (freight, packing, loading) on bills. *Small.*
13. Loan accounts with EMI and interest postings. *Medium.*
14. Loyalty points. *Medium.*
15. Other-income entry type. *Small.*
16. Cess on items (tobacco/pan). *Small.*
17. GSTR-9 annual summary. *Large.*
18. E-invoice via IRP. *Large; only for shops above Rs 5 Cr, so last.*

## Open items

- A second Vyapar pass with working clicks is needed to confirm each *(menu, not seen)* row and to read the full report list and settings pages. That pass also needs a saved sample invoice, which this review was not allowed to make.
- Rows marked "Check ..." need a quick code read to settle "yes" or "partly".
