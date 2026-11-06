# Desktop billing/POS screen patterns (India small-shop context)

## How do Indian and global billing/POS products lay out billing, items, ledgers and reports on desktop?

### Takeaway
Indian desktop incumbents (Tally Prime, Marg) are keyboard-first, with function keys mapped to voucher types, a menu home screen and a right-hand button bar. Newer web tools (myBillBook, Zoho POS) use a POS screen with item search (name, barcode or category), a cart, and "hold bill" support. Zoho offers separate Standard (keyboard) and Touch modes.

### Cited Findings
- TallyPrime's home screen has a top menu bar, the open company details, the Gateway of Tally menu, and a button bar on the right with F-key actions. F4 to F10 each have a preset action in transactions, masters and reports. — [Tally Help](https://help.tallysolutions.com/?p=108972)
- TallyPrime: F5 Payment, F6 Receipt, F8 Sales, F9 Purchase and F10 Other Vouchers each open the entry screen directly. Alt+G (Go To) jumps to any report or master by typing its name. Ctrl+A saves. The same key does the same thing on every screen, and no essential task needs a mouse. Vendor claims 200+ vouchers per hour (not verified). — [Tally business guide](https://tallysolutions.com/business-guides/tallyprime-keyboard-first-design-fast-data-entry/)
- TallyPrime's official help says Ctrl+G is "Switch To" (replaces the current screen rather than stacking), paired with Alt+G Go To. Third-party sites conflict on this. — [Tally release notes](https://help.tallysolutions.com/tally-prime/release-notes-tally-prime/)
- Marg ERP: F2 starts a new sale bill without leaving the billing screen, F7 switches to the voucher window, F6 adds or reduces the rate while billing, and F4 is tied to the e-invoice window. — [Marg Care: new sale](https://care.margcompusoft.com/margerp/billing/44648/1/How-to-create-New-Sale); [Marg Care: F7](https://care.margcompusoft.com/margerp/billing/73839/1/How-to-switch-over-to); [Marg Care: E-Invoice F4](https://care.margcompusoft.com/margerp/general-and-a-c/181532/1/If-E-Invoice-window-does-not)
- Marg: Ctrl+W saves and Esc exits (blog source, not official). — [Sewa Diary](https://sewadiary.blogspot.com/2020/07/keyboard-shortcuts-in-marg-erp.html)
- myBillBook web POS: search items by name, barcode or category; click to add and adjust quantity. "+ Hold & Create Another" parks a bill and starts a new one. Save or Save & Print. Zero or negative stock items are highlighted red. — [myBillBook KB](https://knowledge.mybillbook.in/articles/8679710-how-to-use-pos-billing-in-mybillbook-web-app)
- myBillBook marketing says keyboard shortcuts give fast checkout, but publishes no key list. Desktop POS may be tied to the top plan. One user review calls the desktop app a buggy "beta" compared with the web app. — [myBillBook PC page](https://mybillbook.in/s/billing-software-for-pc/); [Software Advice reviews](https://www.softwareadvice.com/retail/flobooks-profile/reviews/)
- Zoho POS desktop billing app (Windows and macOS) offers Standard and Touch billing modes for keyboard or touch devices, plus customizable quick-key shortcuts. A scanned barcode adds the item to the cart. — [Zoho POS sales management](https://www.zoho.com/en-us/pos/features/sales-management.html); [Zoho POS help](https://www.zoho.com/en-ca/pos/resources/help/process-sale-pos.html)
- Zoho Billing (a separate product) uses Shift+/ to show a shortcut cheat-sheet. — [Zoho Billing help](https://www.zoho.com/billing/help/keyboard-shortcuts.html)
- Odoo POS shortcut module shows all shortcut keys on screen while ALT is held. — [Odoo app](https://apps.odoo.com/apps/modules/19.0/eg_pos_keyboard_shortcuts)
- Shopify POS "smart grid" gives configurable tile layouts, per location. Square Retail iPad was reported (by forum users) as limited to a fixed 3x3 item grid. Both are touch/tablet-first. — [Style Factory](https://www.stylefactoryproductions.com/blog/shopify-pos-vs-square-pos); [Square Community](https://community.squareup.com/t5/Orders-Menu-Items-Catalog/Customize-Grid-Layout-on-Square-Retail-iPad/td-p/694578)

### Inferences
- Two families exist. Indian desktop accounting (Tally, Marg, likely Busy) uses dense keyboard grids. Global POS (Square, Shopify, Lightspeed) uses touch tile grids beside a cart. A kirana or clothes counter with a barcode scanner and a PC fits the keyboard family better, while a tile grid helps with loose, unbarcoded items.
- Hold/park bill and multiple bills open at once (myBillBook) is a must-have for a busy counter.

### Gaps
- Could not find official Vyapar desktop shortcut lists or screen layouts (search only surfaced Vyapar TaxOne). Busy and Lightspeed were not researched for lack of time. No screenshots were reviewed.

## Responsive single app vs separate desktop pages: what do products do and what do UX guidelines recommend?

### Takeaway
Material 3 recommends one adaptive app whose layout changes by window size class: one pane on compact/medium widths, two panes (list-detail) on expanded and wider, and a navigation rail rather than a bottom bar beyond compact. Products split both ways: Zoho ships distinct Standard and Touch modes, and myBillBook runs both a web app and a separate desktop app.

### Cited Findings
- M3 canonical layouts are list-detail, supporting pane and feed. In list-detail the list has a fixed width and the detail pane flexes. Recommended pane count: 1 for compact and medium, 2 for expanded and large, 2 to 3 for extra-large. On small widths, panes collapse to stacked navigation. — [SAP Fiori Android canonical layouts (secondary, mirrors M3)](https://www.sap.com/design-system/fiori-design-android/v26-4/foundations/adaptive-layout/canonical-layouts)
- M3 width classes are compact, medium, expanded, large and extra-large. Third-party implementations use a nav rail above compact and two panes from expanded up. — [window_size_classes pkg](https://pub.dev/packages/window_size_classes); [bevy_material_ui](https://docs.rs/crate/bevy_material_ui/latest/source/src/adaptive.rs)
- Zoho POS offers separate Standard (keyboard) and Touch billing modes rather than a single responsive layout. — [Zoho POS](https://www.zoho.com/en-us/pos/features/sales-management.html)

### Inferences
- A practical path is one responsive codebase whose desktop breakpoints (about 840px and up) switch to a sidebar or rail plus list-detail and tables. Avoid just stretching mobile cards.

### Gaps
- Exact M3 breakpoint numbers (commonly cited as 600/840/1200/1600dp) were not confirmed from m3.material.io in this session. Apple HIG and Microsoft Fluent guidance were not fetched.

## Keyboard-first billing, tables vs cards, density, sidebar navigation

### Takeaway
On desktop, the evidence favours keyboard flow (consistent F-keys, type-to-jump, save shortcut, Esc to exit, discoverable cheat-sheet), barcode scans going straight into the cart, and dense tables with user-selectable density.

### Cited Findings
- Consistent keys across screens and command-style "type to go" navigation are core to Tally's speed. — [Tally guide](https://tallysolutions.com/business-guides/tallyprime-keyboard-first-design-fast-data-entry/)
- NN/g has a dedicated video on designing tables for data-heavy desktop apps (the content was not extracted). — [NN/g video](https://www.nngroup.com/videos/designing-tables-desktop-apps/)
- SAP Fiori offers compact and condensed densities for desktop tables, chosen by data volume, and suggests letting users configure density when unsure. — [SAP table usage](https://www.sap.com/design-system/btp/dna-design/floorplans/table/usage)
- A 2026 table guide recommends shipping density as a persisted user setting. — [SetProduct](https://www.setproduct.com/blog/data-table-ui-design)

### Inferences
- For the app: keep the item search/scan field focused by default on the bill screen; add F-key or Alt shortcuts for new bill, hold, save/print and customer; show a "?" cheat-sheet; and use tables for items, ledgers and reports at desktop widths.

### Gaps
- No authoritative 2025-2026 trend data was found. Vendor AI and voice billing features were not researched.
