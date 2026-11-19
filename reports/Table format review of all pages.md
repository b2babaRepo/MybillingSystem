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

## Still cards on a computer (next, in order)

1. **Money received** (receipts tray): date, customer, amount, mode, matched / not.
2. **Goods to come** and **Check goods in**: supplier, order, item, expected, came.
3. **Suppliers list**: name, phone, owed, oldest due, last bill (like the customer table).
4. **Stock health**, **Sales by category**: already numbers per row, easy wins.
5. **Reminders**, **Disputes**, **Holds**, **Exchange slips**, **Lots**: short registers.
6. Medical pages (**Medicine stock**, **Returns**, **H1 register**): registers by law, worth a printable table.

Pages that are forms or single summaries (Settings, Online store, Backup, Sync, Posters, P&L statement, GST month, Verify my data) do not need a table.
