# Vyapar design review: bill form, bill table and list pages

Date: 18 Nov 2026. Seen on this PC in Vyapar Desktop (trial company, two sample sale invoices and one purchase), look only: nothing in Vyapar was saved, changed or deleted. Compared with Grocery Khata as running locally today (GST demo shop and Sharma General Store).

## Summary

Vyapar feels simple for three reasons, none of them about having more features:

1. **One layout everywhere.** Sale, Purchase, Estimate and the rest use the *same* form: party on the left, number/date on the right, one item grid, totals bottom-right, Save bottom-right. Learn one screen, know them all.
2. **The bill is a table.** Each item is one row with every number in its own column (Qty, Unit, Price, Discount %/₹, Tax %/₹, Amount), and a total row under the columns. What is typed looks like what prints.
3. **Every list page answers "how much is unpaid?" first.** A period filter, then *Paid + Unpaid = Total* chips, then the table with a coloured status (Paid / Partial / Unpaid).

Grocery Khata has more depth (khata, trades, offline, GST checks), but its bill screen is a tile grid plus a cart list, its printed bill hides GST inside the item cell, and its bill list has no paid/unpaid summary or status. Ranked fixes are at the end; the first three are small to medium and give most of the "looks like a proper invoice" effect.

## What was seen in Vyapar

### First-sale screen (your screenshot)
- Left: a **short form in three labelled groups** with round blue icons: *Invoice Details* (number 01, date, read-only), *Bill To* (Customer Name, the only required field, red star), *Invoice Calculation* (Invoice Amount, Received).
- A dashed card shows the sample item as **Quantity · Subtotal · Tax Amt · Total Amount**, with an "Edit Item/s" link.
- A tinted **Balance ₹800.00** strip (green) under the form, then one big red pill button *Create Your First Invoice*.
- Right: a **live A4 preview** that changes as you type: *Tax Invoice*, Bill To / Invoice Details side by side, a purple header row table, Amount in Words box on the left, Sub Total / **Total (purple bar)** / Balance Due on the right.

### Sale and Purchase form (same layout)
- Top bar: **Sale | Credit ⇄ Cash switch** (one tap decides udhaar or cash). Each open bill is a **tab** ("Sale #1", "+" for another).
- Party search with phone beside it; Invoice Number (auto, 3), Invoice Date (calendar), State of supply on the right.
- **Item grid:** `# | ITEM | QTY | UNIT | PRICE/UNIT (Without Tax ▾) | DISCOUNT % · AMOUNT | TAX % · AMOUNT | AMOUNT | (+ add column)`, two empty rows ready, **ADD ROW**, and a **TOTAL** row summing qty, discount, tax and amount under their own columns.
- Below: Add Terms & Conditions, Add Description, Add Image, Add Document; Payment Type (Cash ▾, "+ Add payment type"); **Round Off ☑** with the amount; **Total** box.
- Footer: Share ▾ and a blue **Save**. Closing with changes asks "Current changes will be discarded. Do you wish to continue?".

### List pages (Sale Invoices, Purchase Bills)
- Filter row of **pill chips**: This Month ▾ · date range · All Firms ▾ · All Users ▾.
- **Summary**: Sale: *Total Sales ₹9,450 · ↑100% vs last month · Received ₹5,550 · Balance ₹3,900*. Purchase: **Paid ₹20,000 + Unpaid ₹2,30,000 = Total ₹2,50,000** as three coloured chips with + and = between them.
- **Table** with a filter icon on every column header: Date · Invoice no · Party · Transaction · Payment type · Amount · Balance · **Status** (*Paid* green, *Partial* orange) · Actions (print, share, ⋮).
- Search, chart, Excel and print icons above the table. **+ Add Sale** (red) and **+ Add Purchase** (blue) always in the top bar.
- Print goes straight to the Windows print dialog with no preview (a weak spot).

## Side by side

| Area | Vyapar | Grocery Khata today | Gap |
|---|---|---|---|
| Form layout | Same form for every document; party left, number/date right | Bill, estimate, sale order, purchase each laid out differently | **Partly** |
| Item entry | One grid row per item, all numbers in columns, Add row, total row | Tap tiles; cart list with qty stepper and a small price box; GST only in the summary | **Yes, on desktop** |
| Cash or credit | Credit ⇄ Cash switch at the top | Pay mode buttons and "Udhaar, name later" further down | Small |
| Live preview | A4 preview beside the form | Preview only after saving (print page) | Medium |
| Multiple open bills | Tabs | Hold bill (one at a time) | Small |
| Printed bill table | `# · Item · Qty · Price/Unit · GST ₹(%) · Amount`, total row with qty | `Item · Qty · Rate · Amount`; HSN and GST in grey under the name; no #, no qty total | **Yes** |
| Bill header | *Bill To* left, *Invoice Details* (No., Date) right | Shop band, one subtitle line with number, date and customer | **Yes** |
| Totals block | Sub Total, coloured **Total** bar, **Balance Due**, Amount in words in a box | Total, Paid, On khata, words line; GST table below | Partly |
| List summary | Paid + Unpaid = Total chips, vs last month | Find a bill: search and dates only | **Yes** |
| Status | Paid / Partial / Unpaid pill per row | Only "Cancelled" | **Yes** |
| Per-column filters, Excel of the list | Yes | No | Medium |
| Mobile, offline, Hindi/Gujarati, khata | Weaker | Strong | Ours |

## Design-system notes (tokens and patterns worth copying)

- **Colour roles:** one action colour per document type (red = Sale, blue = Purchase) on the Add buttons; **status colours** green = Paid, orange = Partial, red = Unpaid; one tint (light purple) for the table header and the Total bar. Our app already has `brand`, `due` (red), `udhaar` (orange), `jama`/green tokens: map Paid → green, Partial → `udhaar`, Unpaid → `due`.
- **Numbers:** right-aligned, ₹ with two decimals on documents, totals bold. Ours prints whole rupees in some places and two decimals in others on the same bill.
- **Labels:** small uppercase grey column heads in the grid; values in normal ink. Required fields with a red star.
- **Grouping:** form sections with an icon and a bold title ("Invoice Details :", "Bill To :"). Ours are mostly unlabelled stacks.
- **Empty state:** two blank grid rows already there, so the first action is typing, not finding a button.

## Ranked changes for Grocery Khata

1. **Printed bill as a proper table** (small): columns `# · Item (HSN) · Qty · Rate · GST % · GST ₹ · Amount`, a total row with total qty, *Bill To* left and *Bill No. / Date* right in the header, Total on a tinted bar, **Balance due** line, amount in words in its own box. Keep the narrow receipt layout for 58/80 mm rolls.
2. **Bill list with status and summary** (medium): turn Find a bill into "Sale bills": period chips (Today, This month, range), **Received + Due = Total** chips, a Paid / Partial / Due pill on each row from the bill's khata part, print and share per row, Excel download.
3. **Desktop table entry on the bill screen** (medium): on screens 1024 px and wider, show the cart as a grid (Item · Qty · Unit · Rate · Disc % · GST % · Amount) with Enter moving across cells and a blank row always at the bottom; keep tiles and the stepper on phones.
4. **Credit ⇄ Cash switch at the top of the bill** (small): one control that decides udhaar or paid now; customer becomes required (red star) when Credit.
5. **Live bill preview beside the form on desktop** (medium): reuse the print component in a side panel, updating as lines change.
6. **One form layout for estimate, sale order, challan and purchase** (large): same header, grid, totals and footer as the bill.
7. **Bill tabs on desktop** (small to medium): several open bills at once, instead of hold-and-resume.
8. **Per-column filters on lists** (medium).

## Not seen

- **Expense page and Vyapar's success / error messages** (toasts after Save, field errors such as a missing party): asked for, not reached. Each click on a row's print icon queued a Windows print window, which this review can't close (it belongs to Windows, not Vyapar), and they kept coming back. To finish this part: close Vyapar's print windows (or restart Vyapar) and either share screenshots of the Expense page, a saved-bill message and a Save with the party left empty, or ask for another pass.
- The only messages seen: closing a form with changes asks *"Close Sale — Current changes will be discarded. Do you wish to continue?"* with Cancel / OK.
- Vyapar's printed invoice for the saved bills: its print button skips any preview and opens the Windows print dialog, which this review could not drive. The preview seen is the first-sale sample (your screenshot).
- Item, party and report screens were reviewed earlier (see `reports/Vyapar hands-on gap review 2.md`); this pass looked at design only.
