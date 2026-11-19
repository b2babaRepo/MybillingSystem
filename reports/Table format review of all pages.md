# Table format review: every menu page

Date: 19 Nov 2026. All 53 pages in the side menu were checked on a computer (1400 px wide) for how their records are shown. The target is the Sale bills table: tinted, uppercase column heads that sort (▲ ▼, ↕ when not in use), a filter box or menu under the columns worth filtering, a total row, "N of M shown" while filtering, and Download for Excel. Phones keep their cards everywhere.

## One shared table

`components/DataTable.tsx` (already used by 7 pages) now does all of the above. Any page that switches to it gets the same look and behaviour, and the 7 pages already on it (Dues, Day report, Reports, Discounts, Godowns, Staff sales, the cash ledger's daily closes) were upgraded with no other change. `components/TableHead.tsx` holds the sort head and filter box for the hand-built tables.

## Done

| Page | Now on a computer | Filters |
|---|---|---|
| Sale bills | Table (built earlier) | Bill no., customer, items, at least ₹, status |
| Customer list (Khata) | Party · Amount table (earlier) | Sort |
| Customer transactions | Register table (earlier) | Type chips, Excel |
| What to order | Table per supplier, totals | Item, in stock |
| Stock check | Count sheet per category, value of differences | Item, counted / not / differs |
| Balance sheet | Two-sided statement, equal totals | (fixed order) |
| Cash ledger | Cash book with opening and closing rows | Type, party, in / out |
| **Expenses** | Table under the day's summary | Heading, note, paid by |
| **Cheques** | One register, waiting first, Clear / Bounce in the row | Customer, cheque no., bank, status |
| **Bank book** | Passbook-style table, totals with opening | Type, party, paid by, reference |
| **Estimates** | Register, Make bill / Cancel in the row | Customer, status |
| **Sale orders** | Register; clicking an order opens its items below | Customer, status |
| **Orders placed** | Register with a "came" progress bar | Supplier, status |
| **Items** | Table with cost, margin, days left, barcode | Name/barcode, category, stock |
| Dues, Day report, Reports, Discounts, Godowns, Staff sales | Upgraded through the shared table | Sort, Excel where set |

## Done in the second round

| Page | Now on a computer | Filters |
|---|---|---|
| Suppliers list | Supplier · Oldest due · Owed, total we owe | Sort, Excel |
| Stock health | Item, category, rank, movement, stock, value, sold and sales (90 days), last sold, cover | Item, rank, movement |
| Sales by category / supplier / customer | Name, sales, share bar, quantity, bills, profit, totals | Name |
| Money received | Table of what is waiting; clicking one opens its match card | Payer or UTR |
| Goods to come | Table, most overdue first; clicking an order opens chase, calls and lorry | Supplier, stage |
| Exchange slips | Register with amount, still open, valid till, status | Slip no., customer, status |
| Reminders | Everyone owing: ready to send, or why left alone; Send / Agreed / Stop in the row | Customer, status |

## Still cards on a computer

- **Disputes**, **Holds**: short lists, rarely long enough to need a table.
- **Check goods in**: a receiving form (counted quantities per line), not a list.
- Medical pages (**Medicine stock**, **Returns**, **H1 register**): registers by law, worth a printable table next.

Pages that are forms or single summaries (Settings, Online store, Backup, Sync, Posters, P&L statement, GST month, Verify my data) do not need a table. **Landed cost by bill** already shows a table per bill.
