# Session summary: clothes features, desktop version and faster billing

Grocery Khata: Indian shop billing PWA (React + Tailwind frontend, .NET 10 + Postgres backend).
Branch: `feature/phase1-walking-skeleton` (nothing pushed; `main` left at `2116300` as asked).
Dates: up to 7 November 2026.

## 1. Commits made in this session (newest first)

| Commit | What it did |
|---|---|
| `423ed9c` | English as the default language; bill search in Hindi or English; `2*` quantity; Enter adds best match; Print bill / Ctrl+P |
| `7fde811` | Desktop follow-ups: Hold bill, one-row cart lines, no lingering old screen, Stocktake/GST/Expenses tidy, table test |
| `18bd053` | Desktop layout (9 steps) plus the desktop design review report |
| `df32031` | Salesperson on each bill line, staff commission, staff sales report |
| `4a02daf` | Barcode billing, price tags, exchanges linked to the original bill |
| `660d5ed` | Clothes size and colour: styles, size-grid stock in, sizes sold/left report |
| `1139c27` | Discreet bill names (innerwear / nightwear) |
| `4582867` | Clothes GST by piece price; bill discounts with a staff limit |
| `6860e68` | Clothes master list and clothing plan report |
| `2116300` | Items "Suggested" view |

`appsettings.Development.json` was kept out of every commit.

## 2. Clothes shop features (earlier in the session)

- **GST by piece price** for garments (5% up to ₹2,500 a piece, 18% above), with a warning on the bill line.
- **Discounts**: 5% / 10% chips per line; helpers limited to a staff discount limit (default 10%) set by the owner.
- **Discreet bill names**: innerwear and nightwear print as "Innerwear" / "Nightwear".
- **Size and colour**: items grouped into styles, a size picker when billing, stock received by size grid, a sizes sold/left report.
- **Barcode billing**: scanner or camera adds items; printable price tags; exchanges linked to the original bill.
- **Salesperson and commission**: "sold by" for the bill and each line, a commission % per staff member, a monthly staff sales and commission report.

## 3. Desktop version

The user chose **one responsive app** (works on phone and desktop). From 1024 px wide:

**Design review** — `reports/Desktop screen design review.md` reviewed 19 screens, compared Tally, Marg, myBillBook, Zoho, Square and Shopify, and set a 9-step plan. Recommendation: one app with about 10 screens getting their own desktop layout; no separate desktop app.

**The 9 steps built:**
1. Side menu with Money and Shop groups linking every page; page name in the top bar; readable page widths.
2. Bill: search stays ready after each item; keys F2, F4, Ctrl+Enter, Esc, `?`; pop-ups open as centred windows (Esc closes) across the app.
3. Shared sortable table (`DataTable`) and list-plus-detail layout (`ListDetail`); staff sales report as a table.
4. Customers list beside the khata; Dues as a sortable table with a total row.
5. Items in aligned columns; item picker with categories on the left and 2–3 items per row.
6. Reports, Profit & loss and Cash book in two columns.
7. Orders list beside the open order, packing bar pinned in the panel.
8. Settings form in two columns, Staff with "Add staff" on the right, Suppliers list beside the open supplier.
9. Polish: Home tiles in one row; picker's chosen-items bar moved clear of the category list.

**Follow-ups:**
- Hold bill: parks the current bill on that device; held bills come back with one click.
- One-row cart lines on desktop.
- A page change shows loading at once instead of the previous screen.
- Stocktake shows the difference against the books (shortfalls in red); GST and Expenses at a readable width.
- Checked: F4, Dues table (by automated test), Settings / Staff / Suppliers at phone size.

## 4. Faster counter billing and language

- **English is the default language**; anyone who chose Hindi keeps it.
- **Search in either script**: "aata" finds आटा, "cheeni" finds चीनी, "बासमती" finds Basmati (`lib/nameMatch.ts`, with tests).
- **Quantity first**: `2*` or `1.5x` before an item or barcode.
- **Enter adds the best match** (a clothes style opens its sizes).
- **Print bill** button and **Ctrl+P** after saving open the bill with the print dialog.

## 5. Checks

- Typecheck passes; app tests: **36 pass**.
- Checked in the browser at 1366 × 768 and phone size in the clothes and kirana test shops.
- No bills were saved during testing; test carts were emptied.
- The backend stopped once and was restarted.

## 6. Not done or not checked

- ~~Printing not tried live~~ — **checked 7 Nov 2026**: a ₹45 khata test bill for Test Customer (memo 2026-27/0005) was saved with Ctrl+Enter; Ctrl+P opened the cash memo and the print dialog once; the action buttons are hidden on paper.
- ~~Dues table not seen with real rows~~ — **checked 7 Nov 2026**: the Test Customer row shows ₹45 in 0–7 days with a total row, and clicking it opens their khata. Spacing after "Total due" fixed. The ₹45 test bill stays in the kirana test shop (it can be cancelled from the bill page).
- The customer ordering app (online store, household lists) still uses the phone layout on desktop.
- Two test orders remain "Placed" in the kirana test shop.
- Other untracked folders (`.claude/`, `design-canvas/`, `marketing/`, earlier reports) are not committed.

## 7. Possible next builds

1. Desktop layout for the customer ordering app.
2. Dues and reminders checked with real test data (needs approval to add test customers).
3. More trade plans from `reports/` (salon, restaurant, furniture and others).
