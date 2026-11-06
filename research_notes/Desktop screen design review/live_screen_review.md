# Desktop screen design review (live, clothes shop, owner)

Viewed live at http://clothes.localhost:5173 in the built-in browser at 1366x768 and 1920x1080 on 2026-11-06. Only looked; nothing submitted or changed. Sources are first-hand observation ("Live view") and the JS measurements noted. Short review: 6 screens seen properly. Kirana shop not checked.

## Layout at desktop width, screen by screen

### Takeaway
At desktop width the app shows a fixed left sidebar (Home, Orders, Bill, Udhaar, Items) plus one centred content column, about 1152px wide at 1920. Most screens are the phone layout made wider. Billing is the only screen with a real two-pane layout, and even that leaves most of the screen empty at 1920.

### Cited Findings
- Shell: a left sidebar with 5 Hindi nav items, a green top bar with the shop name, a sync chip and an avatar. Content sits in a column about 1152px wide at a 1920 viewport, so roughly 200px of empty grey on the right and a sidebar that is mostly blank below its 5 links — Live view (JS: main width 1152)
- Home (#/): a sales card with cash/UPI/udhaar tiles, then 4 large action tiles in a 2x2 grid (New bill, Udhaar, Payment received, Customers), then full-width rows for each list item (today's summary, "my online store", etc.). The rows stretch the whole column with the label on the far left and the value or chevron on the far right, which is a typical stretched-phone pattern — Live view 1366
- Bill (#/bill): on the left, a search box and a grid of 5 small item tiles plus a "+ new item" button. On the right, a cart panel that only says "pick items to add to the bill". At 1366, about 70% of the area below the tiles is empty. At 1920 the tiles stay small (about 5 per row), the cart panel is short, and the bottom two-thirds of the screen is blank — Live view 1366 and 1920
- Items (#/items): 3 summary tiles across the top, a search box, filter chips (All, by category), and a "stock alert" banner. Each item is a card-row with the name and size chips (S/M/XL with counts) on the left, quantity and price in loose middle columns that don't line up between rows, and a "+ stock in" button on the right. A floating "+ add item" FAB sits bottom-right, detached from the content column at 1920 — Live view 1366 and 1920
- #/customers and #/orders both rendered the Home dashboard (sales ₹29,091 card) with a back arrow, instead of a customer or order list. Either the hash routes are different or they fall back to home — Live view 1366
- #/reports and #/settings showed only the top bar and a "…" loading placeholder on a blank page after navigation. They did not render in the time observed — Live view 1366
- Font: body 16px, family "Mukta, Noto Sans Devanagari, system-ui". Devanagari renders cleanly. Buttons measure 44px tall, and the Home action tiles 84px, so the controls are touch-sized — Live view (JS)

### Inferences
- Severity high: Bill at desktop. This is a counter-PC workflow, but it has small tiles, no item table, no visible keyboard focus or shortcuts, and a mostly empty cart pane. It needs a dedicated desktop billing page: an always-focused barcode/search field, a line-item table (item, size, qty, rate, discount, salesperson, amount), a totals and payment panel pinned on the right, and keys such as F2 search, F4 payment, Ctrl+Enter save, Esc clear.
- Severity high: Items. Card rows with misaligned columns should become a real sortable table at 1024px and up (name, category, sizes/stock, MRP, price, actions), with a two-pane list/detail or a side drawer for editing.
- Severity medium: Home. Turn the full-width list rows into a 2–3 column card grid at desktop width, keep the KPIs in one row, and shrink the action tiles.
- Severity medium: Shell. Either use the right-hand space (detail pane) or let tables run full width. Put the FAB as a toolbar button in the content header on desktop.
- Customers and Orders would benefit from a two-pane list/detail layout (list on the left, ledger or order on the right). Reports would suit wide tables and charts. Settings can stay responsive with a settings sub-nav.
- 44px controls are acceptable but oversized for a mouse. A compact density (32–36px) at desktop width would fit more rows on 768px-tall laptops.

### Gaps
- Didn't verify the real routes for customers, orders, reports, staff, sizes, stocktake or the customer ordering app. #/customers and #/orders showed Home, and reports/settings stayed on a loading placeholder. App.tsx grep found only the #/p/ route string, so the routing is defined elsewhere.
- Didn't open the cart with items, the payment panel, or any drawers. Didn't test keyboard or barcode input. Kirana shop not reviewed.

## Which screens most need a dedicated desktop page

### Takeaway
Priority order: Bill (dedicated desktop POS page), then Items (table), then Customers/Orders (two-pane). Home, Reports and Settings can make do with responsive tweaks.

### Cited Findings
- See the screen observations above — Live view

### Inferences
- Barcode scanners act as keyboards, so the desktop bill page must keep focus in the scan field after every add. That isn't visible in the current tile-first layout.

### Gaps
- Didn't test whether a search box focus/Enter-to-add flow already exists.
