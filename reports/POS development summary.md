# POS development summary

Status as of 27 November 2026, branch `claude/mybillingsystem-app-review-9y3wq0`.

## Where things stand

| Part | Status |
|---|---|
| Phase 1: supermarket counter basics (9 items) | Done, merged, deployed |
| Phase 2: offers, loyalty, prices, supervisor (4 of 5 items) | Done, merged, deployed. Pine Labs card machine blocked |
| Phase 3: tills, mall feed, vouchers, customer display (3 items) | Done, merged, deployed |
| Head office for chains (item 18) | Not started |
| Category menu for every trade | Researched and planned, not built |
| Electrical list and new beauty items | Built and tested on the branch, not yet merged or deployed |

Last full test run: backend 598 passed, frontend 190 passed, lint clean. The production Deploy workflow last ran successfully after PR #11.

## Merged pull requests

| PR | What it brought |
|---|---|
| #7, #8 | App and inventory review fixes, billing tests, SMS sign-in fix |
| #9 | Password reset with an SMS code ("Forgot it? Get an SMS code" on the profile screen) |
| #10 | The Deploy button: `.github/workflows/deploy.yml`, run by hand from GitHub Actions, type DEPLOY to confirm |
| #11 | Phases 1–3 of the supermarket and mall POS |

## Phase 1: counter basics

| # | Feature | What it does |
|---|---|---|
| 1 | Load test | 20 tills, 20,000 items. Went from 2,295 to 5,418 bills a minute; slowest 5% of bills from 1.8 s to 0.39 s |
| 2 | Bill-number lock | The bill number is taken last, so many tills no longer wait on each other |
| 3 | Weighing-scale labels | Scans the price or weight barcode printed by the scale |
| 4 | Several barcodes per item | One item can carry its old and new barcodes, or a box and a piece code |
| 5 | Cashier shifts | Opening float, blind close and the difference, per till |
| 6 | Held bills | A bill held on one till can be picked up on any other |
| 7 | Item list on the device | Only changed items are downloaded: 300 changes are 248 KB instead of 16.5 MB |
| 8 | Cash drawer | Opens on a cash bill through the printer |
| 9 | Offline returns | Credit notes can be made without internet and go up later |

## Phase 2: selling tools

| # | Feature | What it does |
|---|---|---|
| 10 | Offers | Percent and amount off, buy X get Y, quantity slabs, combos, bill-value offers, chosen days and hours. The biggest saving is applied first; offers don't stack |
| 11 | Loyalty points | Earned on the customer's mobile number, spent with an SMS code, taken back if the bill is cancelled |
| 12 | Batch price change | Change many prices at once; changed items wait in a label reprint queue |
| 13 | Supervisor PIN | Voids, refunds and price changes by cashiers need a supervisor's PIN; the approver is saved on the bill |
| 14 | Pine Labs card machine | **Blocked**: needs merchant credentials from Pine Labs |

## Phase 3: big shops and malls

| # | Feature | What it does |
|---|---|---|
| 15 | Mall sales feed | A sales-only key for the mall, daily and bill-level data (also as CSV), and a nightly upload |
| 16 | Registered tills | Each till gets a code (T01, T02…), can be switched off, and shows on each bill |
| 17 | Gift vouchers and customer display | Sell and spend gift vouchers (shown in the cash book and Tally); a second screen shows the customer their bill |

## Item lists and category menus (since PR #11, on the branch)

- **Electrical list**: 258 items in 18 groups (wires and cables, conduit, distribution boards, MCB/RCCB, fuses and stabilisers, switches, sockets, modular plates, lights, fans, geysers, tapes, holders, meters and tools). HSN filled, GST left for the shop or CA.
- **Beauty list**: 65 new items, now 1,017, with a new "Baby and kids care" group and the missing men's grooming, body and salon items.
- **Plans written**: category menus for every trade, hardware with a separate water and plumbing department, paint, electrical and beauty menus, and how categories should flow from the common list to the shop's list to the customer store.

## Reports in this folder

| Report | Topic |
|---|---|
| Supermarket and mall POS plan | The 18-item plan this work followed |
| Category menu for item ordering | Grocery category menu, search and filters |
| Category menu for all shop types | The same menu for every trade |
| Hardware paint and water menu | Hardware, paint, water, electrical and beauty menus |
| Category flow common list to customer | How categories go from the common list to the shop and the store, with a 9-step build plan |
| Inventory test plan and findings, Negative testing findings, Buy and sell workflow review | Testing and review results |

The other reports in this folder are from earlier work (trade plans, master lists, design reviews).

## Not checked on real hardware

Tested in code only, so they need a try in a shop: cash drawer, weighing-scale labels, Bluetooth printing and the nightly mall upload.

## Next steps

1. Merge the branch's new item lists into main and deploy, so shops get the electrical and beauty items.
2. Fix the two problems the research found: seeded items not showing as "new" to existing shops, and the update check matching items by name instead of by their link.
3. Build the category tree and menu (about 10–13 developer days plus 3–4 days of sorting items), then the menu screens.
4. Pine Labs card machine, once credentials are available.
5. Head office for chains, if wanted.
