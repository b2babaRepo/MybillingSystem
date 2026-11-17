# Grocery Khata already matches most of Vyapar

Date: 17 Nov 2026. Branch: `feature/phase1-walking-skeleton`. Review only; no code was changed.

The second hands-on pass through Vyapar Desktop got much further than the first. It read every **Settings** tab, the full **Reports** list (about **50 report names** in 9 groups), the **Add Item** form, and the Cash & Bank, Utilities, Sync & Backup and Grow Your Business menus. The **Sale screens were never opened**: on a fresh trial company, clicking Sale always brought back the first-sale wizard ("Enter details to make your first Sale"), and getting past it would have meant saving an invoice, which this look-only pass did not do. Checking against the code shows that Grocery Khata (GK) already has most of Vyapar's everyday shop features. That includes some the earlier review marked as missing: **delivery challans convertible to invoices**, **numbered debit notes**, a **customer credit limit**, an **audit log** and **book closing**. GK also goes further in trade depth (medical, clothes, karigar, online store). What GK lacks are Vyapar's *configurability and accounting extras*: print themes and per-field print toggles, bill-number prefixes, last-5-prices and show-profit on the bill, due dates and payment terms, wholesale price, item custom fields, loyalty points, loan accounts, trial balance and cash flow, bill-wise profit, party-wise and category-wise reports, file backup and restore, barcode label printing, bulk item update, and an app passcode. The top three to build are **one-tap PDF bill to WhatsApp, backup and restore to a file, and barcode label printing**. All three are medium-sized and matter to kirana, clothes and medical shops alike. Sources: the walkthrough notes (`research_notes/Vyapar hands-on gap review 2/vyapar_walkthrough.md`), the first review (`reports/Vyapar hands-on gap review.md`), and Grep/Glob checks of `frontend/src` and `backend/src/GroceryKhata.Api`.

## What was seen on screen, and what was not

**Seen this pass.** The *first-sale wizard* has Invoice No. 01, the date, Customer Name, "Add Sample Item", and Invoice Amount / Received / Balance, with a live Tax Invoice preview beside it (SGST 9% / CGST 9%, Amount in Words, Balance Due). Trying to leave it opens a "You are almost done with your first invoice" dialog. *Purchase & Expense* lists Purchase Bills, Payment-Out, Expenses, Purchase Order and Purchase Return / Dr. Note. The empty Purchase Bills screen pushes WhatsApp and email sharing. *Settings* has ten tabs: General, Transaction, Print, Taxes & GST, Transaction Message, Party, Item, Service Reminders, Accounting and Multi Currency. Each was read in full (details in the tables below). The *Print* tab shows a regular/thermal printer switch, at least four named themes (Tally Theme, Landscape 1 and 2, GST Theme 1, more behind an arrow), colour options, and a live preview with an HSN tax summary, TCS and "You Saved". *Transaction Message* sends through Vyapar's own channel by default, with a "100 CREDITS" badge, or through personal WhatsApp after a login. It also has per-transaction auto-message switches and editable templates. The *Add Item* form has Product/Service, Name, HSN search, Unit, Image, Category and Item Code, plus Pricing (sale price with or without tax, discount %, wholesale price, purchase price, tax rate) and Stock (opening qty, at price, as-of date, min stock, location) tabs. *Cash & Bank* has Bank Accounts, Cash In Hand, Cheques and Loan Accounts, and the Banks page promises bank details and a UPI QR on invoices. *Utilities* has Import Items (from barcode with a "100 Mn+" library, from Excel, from the Vyapar library), Set Up My Business, Accountant Access, Barcode Generator, Update Items In Bulk, Import From Tally, Import Parties, Track Your Salesmen, Export To Tally, Export Items, Verify My Data and Close Financial Year. *Sync, Share & Backup* shows staff sharing behind a premium crown, plus Auto Backup, Backup To Computer, Backup To Drive and Restore. *Grow Your Business* has Google Profile Manager, Marketing Tools, WhatsApp Marketing, Online Store and Smart Ads. The *Company* menu has Change Company and Rename. Premium nags show up everywhere: the trial banner, "Get Vyapar Premium", the crown on Sync, and the message credits.

**Never opened.** The whole Sale menu was never opened: the invoice form, POS, estimate, proforma, sale order, delivery challan, credit note and payment-in. The forms behind Purchase Bill, Payment-Out, Purchase Order, Purchase Return and Expense were not opened either. Also not opened: the unit, category and godown screens, stock adjustment, the party form and groups, the *contents* of every report (only names were read), the Ctrl+F Open Anything list, the Shortcuts key list (blocked by the wizard), and the Help and Versions menus (the user interrupted). Other Income has no menu entry because it is switched off by default. Rows below marked **(earlier, not seen)** come from the first review's public-menu list, not from the screen.

## Feature-by-feature: Vyapar against Grocery Khata

Key: **Yes** = present, **Partly** = some of it, **No** = not found in code.

### Sale and billing (Sale screens not opened; rows from settings and the earlier report)
| Vyapar feature | GK | Where / what is missing |
|---|---|---|
| Sale invoice GST / non-GST *(earlier, not seen)* | Yes | `BillScreen.tsx`, `TradeBill.tsx`, `PrintScreen.tsx` |
| Live invoice preview beside the form (seen in the wizard) | No | GK previews only after saving |
| Estimate / quotation | Yes | `EstimatesScreen.tsx` |
| Proforma invoice (setting, on) | No | No "proforma" anywhere in the code |
| Sale order | Yes | `SaleOrdersScreen.tsx` |
| Delivery challan → invoice | **Yes** (corrects the earlier report) | "Save as delivery challan (bill later)" and "Make invoice of N challans" (`i18n/enMore.ts:288-309`, `Ledger/Trades.cs`) |
| Credit note / sale return | Yes | `ReturnsScreen.tsx`, exchange slips |
| Payment-in | Yes | `ReceiptsScreen.tsx` |
| Lite Sale / Full Sale billing type | Partly | GK has one bill screen; trade bills in `TradeBill.tsx` |
| Cash sale by default, add time on bill | Yes | Walk-in bills; entries are time-stamped |
| Billing name, customer PO details on bill | No | No PO no. or separate billing name field |
| Inclusive/exclusive tax on rate | Partly | Prices are tax-inclusive; no per-bill switch found |
| Show last 5 sale/purchase prices of an item | No | Price changes are audited (`ProductEndpoints.cs:188`) but not shown on the bill |
| Free item quantity | Partly | "free {{qty}}" on supplier bills (`enMore.ts:711`), not on sales |
| Transaction-wise tax and discount | Yes | Bill discount, `DiscountsScreen.tsx`, `OffersScreen.tsx` |
| Round-off total | Partly | Round-off on supplier bills and the month statement (`MonthStatement.tsx:21`); no setting |
| E-way bill no. | Yes | `components/EwayBill.tsx`, `lib/ewaybill.ts` |
| Quick entry / skip preview | Partly | Mobile counter flow; no setting |
| Repeat (recurring) invoices | No | Not found |
| Passcode for editing or deleting a bill | Partly | Helper limits and roles (`TeamScreen.tsx`); no passcode |
| Discount during payment; link payments to invoices | Partly | Khata is a running balance; bill-wise payment links only for supplier bills |
| Due dates and payment terms | No | No due date on sale bills (credit limit exists) |
| Show profit while making a sale | No | Margin only in reports (`ItemMarginScreen.tsx`) |
| Terms and conditions on bill | Partly | Fixed slip terms only (`PrintScreen.tsx:528`) |
| Additional fields, transport details, extra charges | Partly | Transport via the e-way bill; delivery charge; no custom bill fields |
| Bill-number prefixes per document | No | Sale bills use FY numbering; no shop-set prefix |
| TCS on invoice | No | Rare for small shops |

### Purchase and expense
| Vyapar | GK | Notes |
|---|---|---|
| Purchase bill | Yes | `GoodsInScreen.tsx`, supplier bills, bill photos |
| Payment-out | Yes | Supplier page statement and payments |
| Purchase order | Yes | `SupplierOrders.tsx`, `OrdersPlacedScreen.tsx`, `GoodsToComeScreen.tsx` |
| Purchase return / debit note | **Yes** (corrects the earlier report) | `Ledger/GoodsReceipts.cs`, migration `20261116034322_DebitNoteNumber` |
| Expenses with categories | Yes | `ExpensesScreen.tsx`, `KharchaMonth.tsx` |
| Other income (setting, off by default) | Partly | Only empty-bag sales count as other income (`enMore.ts:715`); no general entry |
| Fixed assets (setting) | Partly | Balance-sheet items for furniture and equipment (`BalanceSheetScreen.tsx`) |

### Items and stock
| Vyapar (Add Item form and Item settings) | GK | Notes |
|---|---|---|
| Product / Service | Yes | `Product.IsService` |
| Name, HSN, unit, category, image | Yes | `Product.cs`, `CategoriesScreen.tsx` |
| Item code (assign code) | Partly | Barcode field only; no separate item code |
| Sale price with/without tax, purchase price, tax rate | Yes | `SellingPrice`, `CostPrice`, `GstRate` |
| Discount % on sale price, item-wise discount | Yes | Offers and discounts |
| Wholesale price (min qty) | No | No wholesale field in `Product.cs` or the frontend |
| Party-wise item rate | Partly | `RatesScreen.tsx`; no per-customer rate field |
| Opening qty, at price, as-of date | Partly | Stock and cost exist; no as-of opening date |
| Min stock to maintain, low-stock dialog | Yes | `ReorderLevel`, `StockHealthScreen.tsx`, low-stock on Home |
| Location in shop | Yes | Aisles (migration `AislesAndPhotos`) |
| MRP; tax calculated on MRP | Partly | `Mrp` exists; no tax-on-MRP option |
| Serial / IMEI | Yes | `SerialsScreen.tsx`, `TracksSerials` |
| Batch, expiry, mfg date | Yes | `LotsScreen.tsx`, `MedicineStockScreen.tsx` |
| Model no., size | Partly | Size and colour (clothes); no model no. |
| Item custom fields | No | Not found |
| Quantity decimals, default unit | Yes | Decimal quantities and units |
| Barcode scan | Yes | `ScanSheet.tsx` |
| Manufacturing (BOM) | No | Karigar challans cover job-work only (`WorkshopPanels.tsx`) |
| Update sale price from a transaction | Yes | Bulk price update on goods-in (`StockService.cs:527`) |
| Godowns and stock transfer (setting) | Yes | `GodownsScreen.tsx` |
| Stop sale on negative stock (setting) | Partly | GK never blocks; it shows a warning (`Product.cs:69`) |

### Parties
| Vyapar (Party settings, menu) | GK | Notes |
|---|---|---|
| Party details, statement | Yes | `CustomersScreen.tsx`, `SuppliersScreen.tsx` |
| Party grouping | Partly | `TagsScreen.tsx` |
| Shipping address | No | One `Address` field on `Customer` |
| Manage party status | Partly | Customers can be hidden or moved (`CustomerMove.cs`) |
| Payment reminder after N days, reminder message | Yes | `RemindersScreen.tsx`, `DuesScreen.tsx` |
| Additional party fields 1-4, shown in print | No | Not found |
| Loyalty points | No | Not found |
| Credit limit *(earlier, not seen)* | **Yes** | `Customer.CreditLimit` (`LedgerEntities.cs`) |
| WhatsApp Connect | Yes | WhatsApp share across the app; GK uses the shop's own WhatsApp, with no credits |
| Vyapar Network | No | Not relevant |

### Cash and bank
| Vyapar | GK | Notes |
|---|---|---|
| Bank accounts, cash in hand | Yes | `BankBookScreen.tsx`, `CashBookScreen.tsx`, `CashLedgerScreen.tsx` |
| Cheques | Yes | `ChequesScreen.tsx` |
| Loan accounts with loan statement | Partly | A loan is a balance-sheet item and "Loan EMI" is an expense type (`en.ts:262`); no loan ledger |
| UPI QR and bank details printed on the invoice | Partly | UPI pay is widespread (`PayPage.tsx`); check that the QR prints on the bill |
| Multi currency | No | Not needed for these shops |

### Reports (names only; contents not seen)
| Vyapar report | GK | Notes |
|---|---|---|
| Sale, Purchase, Day book, All transactions | Yes | `ReportsScreen.tsx`, `DayReportScreen.tsx` |
| Profit & loss, Balance sheet | Yes | `ProfitLossScreen.tsx`, `BalanceSheetScreen.tsx` |
| Bill-wise profit | No | Not found |
| Cash flow | Partly | Cash book; no cash-flow statement |
| Trial balance | No | Not found |
| Party statement | Yes | Customer and supplier statements |
| Party-wise P&L, Party report by item, Sale/purchase by party (and by group) | No | No by-party report found |
| All parties (balances) | Yes | `DuesScreen.tsx` |
| GSTR-1, GSTR-3B | Partly | Data and Tally files (`GstExportScreen.tsx`); no portal JSON |
| GSTR-2 | Partly | Hint to match against 2B (`enMore.ts:764`); no match report |
| GSTR-9 | No | Only a turnover reminder (`enMore.ts:345`) |
| Sale summary by HSN, SAC report | Partly | HSN is captured; no standalone page confirmed |
| Stock summary, Stock detail, Item detail | Yes | `StockStatementScreen.tsx` |
| Item report by party | No | |
| Item-wise P&L | Yes | `ItemMarginScreen.tsx` |
| Category-wise P&L, Sale/purchase by category, Stock summary by category | No | Only expenses are grouped by category |
| Low stock summary | Yes | `StockHealthScreen.tsx` |
| Item-wise discount, Discount report | No | |
| Bank statement | Yes | `BankBookScreen.tsx` |
| GST report, GST rate report | Partly | Inside the GST export |
| Form 27EQ, TCS receivable, TDS payable/receivable | Partly | Only a karigar TDS check (`enMore.ts:357`) |
| Expense, Expense category, Expense item | Partly | Category yes; no expense-item report |
| Sale orders, Sale order item | Partly | `SaleOrdersScreen.tsx` list; no item report |
| Loan statement | No | |
| Outstanding ageing *(earlier, not seen)* | Partly | Stock ageing and a supplier "owed by companies" view; no customer 0-30/31-60/60+ buckets |

### GST and accounting settings
| Vyapar | GK | Notes |
|---|---|---|
| Enable GST, HSN/SAC, place of supply | Yes | `lib/ewaybill.ts`, `lib/data.ts` |
| Reverse charge | Partly | Printed as "No" only (`enMore.ts:576`) |
| Composition scheme | Yes | Composition handling and CMP-08 |
| TCS / TDS | Partly | TDS check only |
| Tax list | Yes | Per item and category |
| Accounting module, GSTR-1 error check | Partly | "Fix these first" checks before export (`enMore.ts:347`); no double-entry module |

### Print and messages
| Vyapar | GK | Notes |
|---|---|---|
| Regular and thermal printers | Yes | `components/PrintPage.tsx`, `lib/escpos.ts`, `lib/btPrinter.ts` |
| Several themes and colours | No | One layout |
| Paper size, orientation | Partly | A4 / thermal widths in `PrintPage.tsx`; no orientation |
| Company logo on bill | No | No "logo" anywhere in the frontend |
| Text sizes, repeat header, extra top space, min rows | No | |
| Original / duplicate copy | No | |
| Amount in words | No | Not found |
| You saved | Yes | `enMore.ts:699` |
| Party's current balance on bill | Yes | Khata balance on the slip |
| Signature, received by, delivered by | No | |
| HSN tax summary on bill | Partly | GST breakdown on the bill; check the HSN-wise table |
| Auto message per transaction, editable templates | Partly | WhatsApp share and reminders; no per-type auto switches or template editor |

### Settings, sync, utilities
| Vyapar | GK | Notes |
|---|---|---|
| App passcode | No | No passcode or PIN lock found |
| Amount and quantity decimals | Yes | Fixed at 2 |
| Block new items/parties from the bill | Partly | Helper limits |
| Multi firm, change company | Yes | Multi-shop switch (`MyStoreScreen.tsx`) |
| Audit trail | **Yes** | `Persistence/Audit.cs`, `AuditEntries`; no screen to browse it |
| Screen zoom 70-130% | No | |
| Settings search | No | |
| Auto backup, backup to computer or Drive, restore | Partly | Cloud sync and data export (`ExportImportPanel.tsx`, `SyncScreen.tsx`); no restore from a file |
| Staff sharing with roles | Yes | `TeamScreen.tsx`, `StaffScreen.tsx` |
| Import items: Excel | Yes | `ImportScreen.tsx` |
| Import items from a barcode library or item library | Partly | Master item lists per trade; no 100M barcode lookup |
| Import parties | Yes | `CustomerImportScreen.tsx`, supplier import from Tally or Vyapar CSV |
| Import from Tally | Partly | Supplier CSV only |
| Export to Tally | Yes | Tally files for the CA (`GstExportScreen.tsx`, paid plans) |
| Export items | Yes | Export panel |
| Accountant access | Partly | Send to CA; check for a read-only CA login |
| Barcode generator / labels | No | No label layout |
| Update items in bulk | Partly | Price bulk-update on goods-in only |
| Track your salesmen | Yes | Trips, `StaffSalesScreen.tsx`, `TripsScreen.tsx` |
| Verify my data | No | |
| Close financial year | Partly | Month book closing with audit (`Ledger/Books.cs:33`) |
| Service reminders | Yes | `AmcScreen.tsx`, `RemindersScreen.tsx` |
| Online store | Yes | `StoreApp.tsx`, a GK strength |
| Google Business Profile, Smart Ads, WhatsApp marketing | Partly | Offers and WhatsApp sharing; no Google Profile link |
| Open Anything (Ctrl+F), shortcut list | No | Only local key handlers (`BillScreen.tsx`, `CustomerFinder.tsx`) |

## Top 10 gaps for kirana, clothes and medical shops

The pattern is clear. Vyapar wins on **knobs** (about 80 settings toggles), **print polish** and a **long report list**. GK wins on trade-specific depth and WhatsApp-native khata, with no credits and no nags. The ten gaps that cost GK the most in a side-by-side demo are these. (1) A **generated PDF bill shared to WhatsApp** in one tap; Vyapar's empty screens all push "share via WhatsApp or email". (2) **Backup to file and restore**, with a last-backup badge; Vyapar shows the last backup time right in Settings. (3) **Barcode label printing**, from Vyapar's Barcode Generator; clothes and medical shops label stock after every purchase. (4) **Print themes with logo, amount in words, signature and original/duplicate**; the first thing a shop owner compares is the printed bill. (5) **Last-5 prices and show-profit on the bill**; counter pricing for kirana and wholesale. (6) **Wholesale price with a minimum quantity**; kirana sells loose and by the box. (7) **Customer ageing and due dates / payment terms**; credit control. (8) **Party-wise and category-wise sale/profit reports, plus bill-wise profit**; owners ask "which customer or category earns". (9) **App passcode** and a passcode for editing or deleting bills; trust when helpers use the phone. (10) **Bill-number prefixes and custom bill fields** (doctor or patient for medical, PO no. for wholesale).

## Ranked build plan

| # | Build item | Why (kirana / clothes / medical) | Size |
|---|---|---|---|
| 1 | One-tap PDF bill to WhatsApp | All three; replaces "Print → Save as PDF" | Medium |
| 2 | Backup to file + restore + last-backup badge | Trust; Vyapar shows it in Settings | Medium |
| 3 | Barcode label printing (A4 sheets, 50x25 thermal; MRP, price, batch, size) | Clothes and medical, after goods-in | Medium |
| 4 | Print upgrade: logo, amount in words, signature, original/duplicate, 2-3 themes | All; the visible comparison | Medium |
| 5 | Last-5 sale/purchase prices and profit hint on the bill | Kirana, wholesale | Small |
| 6 | Wholesale price with minimum quantity | Kirana | Small |
| 7 | Customer ageing buckets + due dates on credit bills | All credit shops | Small |
| 8 | Reports: party-wise sale/profit, category-wise sale/stock/profit, bill-wise profit | All | Medium |
| 9 | App passcode + passcode for editing or deleting bills | All, staff trust | Small |
| 10 | Bill prefixes + custom bill and party fields (doctor/patient, PO no.) | Medical, wholesale | Medium |
| 11 | Live preview beside the bill form / first-bill onboarding | New users | Small–medium |
| 12 | Ctrl+F Open Anything + desktop shortcut list | Desktop counters | Small–medium |
| 13 | Bulk item update screen (price, GST, category, reorder level) | All, at setup | Medium |
| 14 | HSN summary page, GSTR-1 portal JSON, GSTR-2B match | GST shops, medical | Medium |
| 15 | Loan accounts with EMI ledger and statement | Shops with loans | Medium |
| 16 | Loyalty points | Kirana, clothes | Medium |
| 17 | Proforma invoice; repeat invoices | Wholesale, monthly customers | Small |
| 18 | Other-income entry type; audit-log viewer screen | Accounts | Small |
| 19 | Barcode-library item lookup (name from EAN) | Kirana setup | Large (needs data) |
| 20 | Trial balance, cash-flow statement, GSTR-9, TCS/TDS reports | CA-facing | Large |

What stays uncertain: no Vyapar report contents and no sale form were seen, so the depth of Vyapar's POS, credit note and report filters is still unknown. Several "Partly" rows (UPI QR on print, HSN table on the bill, CA login) need a quick look at the running GK app to settle.

## Conclusion

Most of the gap is *presentation and control*, not missing ledgers. GK already records the data that most missing Vyapar reports need: cost prices, categories, customers on bills, audit entries. So items 5-8 are mostly new report screens over existing tables, and they are cheap wins. The real product risk is the printed and shared bill, plus the backup story. Those are what a shop owner sees in the first five minutes of a Vyapar-versus-GK demo. The next Vyapar pass should save one sample invoice so the Sale menu, POS and report contents can finally be compared.
