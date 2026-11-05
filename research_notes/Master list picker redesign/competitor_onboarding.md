# Competitor catalogue onboarding (item library / master list pickers)

Scope note: 11 searches/fetches. Public help docs for most Indian billing apps describe manual entry and Excel import; very few document a true built-in item library. Many named apps (OkCredit, Marg, Zoho Inventory, Petpooja, Blinkit seller, Loyverse) were not covered by sources found. Treat this as partial evidence.

## How do competitors handle catalogue setup (library, category browse, bulk add, inline price, barcode, starter packs, photos)?

### Takeaway
Most mainstream Indian billing apps (Vyapar, myBillBook, Dukaan, Swipe, GoFrugal) still rely on one-by-one forms, Excel/CSV templates or invoice-photo scanning, not a picker over a built-in library. Built-in libraries show up mainly in niche apps (Kirana Fast/Billing Fast, "2 million products preloaded") and in enterprise/ONDC master catalogues. A well-designed master-list picker would be a real advantage for Grocery Khata.

### Cited Findings
- Vyapar: items are added one at a time under Items, or in bulk with an Excel template (Utilities > Import Item). Its kirana guide stresses adding grocery items "along with the product images". I found no documented preloaded library. — [Vyapar general store guide](https://vyaparapp.in/blog/general-store-inventory-management/); [Vyapar inventory guide](https://vyaparapp.in/guides/how-to-manage-stock-inventory-in-vyapar)
- myBillBook: bulk upload of up to 5,000 items per file from a sample template. It can also auto-enter purchase bills from a photo, which creates the new items and tags each line "New" or "Matched" against existing items. — [myBillBook bulk add](https://knowledge.mybillbook.in/articles/0441947-how-to-add-items-in-bulk-using-mybillbook); [myBillBook photo purchase](https://knowledge.mybillbook.in/articles/2564722-how-to-auto-enter-purchase-bills-and-update-stock-from)
- Khatabook (Tally-linked setup): an "Add item quickly" setting shows the full item list from Tally instead of searching one by one. — [Khatabook help portal](https://sites.google.com/khatabook.com/bizahelpportal/how-to-do-data-entry/wants-to-add-itemly-quicky)
- Dukaan: onboarding asks for store name and business category, then "Add Product". Bulk spreadsheet upload works on desktop only, with review before save. The single-product form covers inventory, variants, tax and more. — [Dukaan start store](https://dukaan.help.center/article/5056-how-do-i-start-a-store-on-dukaan); [Dukaan bulk](https://dukaan.help.center/article/5010-how-to-add-products-in-bulk); [Dukaan single product](https://dukaan.help.center/article/5000-how-to-add-a-single-product)
- GoFrugal GoBill: items are added by searching name/code or by barcode scan, with Standard/Serialized/Kit/Matrix item types. I found no preloaded database. Swipe listings describe building your own catalogue. — [search summary of GoFrugal/Swipe listings](https://cdn-download.gofrugal.com/GoFrugal_RPOS6/6.5.8.8/SP21/ReleaseNotes.html); [Swipe Shopify listing](https://apps.shopify.com/swipe-1)
- Kirana Fast / Billing Fast: the listing claims "over 2 million products preloaded" (an FMCG database). It also offers scan-to-log via phone camera or external scanner, a "Business Category Dialog on first login" and "Auto product image suggestion" (v91). Its only stated language is English. — [App Store listing](https://apps.apple.com/us/app/-/id1567324958)
- Square: items are created in the app (More > Items) with photo and category. The Dashboard offers CSV/Excel import and a "Quick Create" that takes just name + price, suggested for small libraries. I found no starter templates. — [Square app items](https://square.com/help/us/en/article/5328); [Square dashboard items](https://squareup.com/help/au/article/5115)
- Mystore (ONDC): an enterprise "master catalogue" lets vendors pick listings from one consolidated catalogue. This is the closest analogue to Grocery Khata's master list. Its grocery guide mentions bulk upload via channel integration. — [Mystore enterprise features](https://www.mystore.in/en/blog/5-powerful-features-of-mystore-seller-app-for-enterprises-to-unlock-ondc-networks-potential); [Mystore grocery](https://www.mystore.in/en/blog/how-to-sell-grocery-on-ondc-)
- JioMart Partner is a B2B wholesale *buying* app for kiranas, not a catalogue builder. JioMart's seller panel checks whether an SKU is already catalogued by entering the EAN/SKU. — [JioMart Partner listing](https://mixrank.com/playstore/apps/com.jio.bapp); [Jio Commerce docs](https://platform.jiocommerce.io/help/docs/openapi/important-links/trace-inventory/jiomart)

### Inferences
- Pick from a library, plus barcode scan, plus a shop type asked at first login (Kirana Fast's pattern) is the strongest first-run pattern I found.
- myBillBook's "New vs Matched" tagging is a good model for showing which picked items already exist in the shop.
- Square's name + price "Quick Create" suggests price entry can be a single inline field at pick time.

### Gaps
- No sources found on OkCredit, Marg, Zoho Inventory, Shopify POS, Loyverse, Petpooja or Blinkit seller onboarding screens.
- No source showed exact picker UI (checkbox grid, category tabs, inline price) for any Indian app; screenshots were not reviewed.

## Price at pick time or later? Pack sizes and variants?

### Takeaway
I found no source documenting this for a library picker. Form-based apps ask for price at creation (Square Quick Create: name + price). Variants sit in a separate detailed form (Dukaan) or item type (GoFrugal Matrix).

### Cited Findings
- Square Quick Create requires only name and price. — [Square](https://squareup.com/help/au/article/5115)
- Dukaan handles variants inside the single-product form. — [Dukaan](https://dukaan.help.center/article/5000-how-to-add-a-single-product)
- GoFrugal uses a "Matrix" item type for size/colour variants. — [GoFrugal listing via search](https://cdn-download.gofrugal.com/GoFrugal_RPOS6/6.5.8.8/SP21/ReleaseNotes.html)

### Inferences
- For kirana use, an optional inline price (prefilled with MRP where known) plus "set price later" would follow the Square minimum while keeping bulk picking fast. Pack sizes are better shown as separate chips under one item than as a variant form.

### Gaps
- No evidence on whether MRP prefill improves completion.

## Onboarding tricks (progress, quick wins, templates per shop type, skip)

### Takeaway
The evidence is thin. The only documented tricks are a business-category choice at signup (Dukaan, Kirana Fast) and shop-type templates via category. I found no published progress-counter patterns for these apps.

### Cited Findings
- Dukaan asks for business category during store creation. — [Dukaan](https://dukaan.help.center/article/5056-how-do-i-start-a-store-on-dukaan)
- Kirana Fast shows a business category dialog on first login. — [App Store](https://apps.apple.com/us/app/-/id1567324958)

### Inferences
- Use the shop type to preselect a "popular items" starter pack (e.g. top ~150 kirana items) that the owner un-ticks rather than ticks. A count ("42 added") and "Finish later" lower the effort; these are general UX practice, not sourced here.

### Gaps
- No sourced data on progress bars or skip rates for Indian billing apps.

## Visual style and language for Indian small merchants

### Takeaway
I found no sourced research in this pass. Most listings stress images ("product images") and simplicity. Kirana Fast lists English only, which leaves Hindi-first labelling as a possible differentiator.

### Cited Findings
- Vyapar promotes adding kirana items with product images. — [Vyapar](https://vyaparapp.in/blog/general-store-inventory-management/)
- Kirana Fast lists English only. — [App Store](https://apps.apple.com/us/app/-/id1567324958)

### Inferences
- Photo-led tiles with bilingual names (English + Hindi) likely help low-literacy owners scan the list quickly.

### Gaps
- No studies on colour or typography trust signals for Indian merchants were found.

## Published metrics or reviews about catalogue setup pain

### Takeaway
I found no published metrics (time to first bill, onboarding completion) and no specific Play Store reviews about item setup pain.

### Cited Findings
- Kirana Fast/Billing Fast: about 58.8K installs. Third-party data shows an iOS average of 1.0 from 2 ratings (unverified estimate). — [AppGoblin](https://appgoblin.info/apps/com.apnidukan.my_app)
- A myBillBook user forum thread complains about app performance (not catalogue-specific). — [myBillBook featurebase](https://mybillbook.featurebase.app/p/billbook-app-performance-issue-need-urgent-fix)

### Inferences
- Vendors steer users to Excel/desktop bulk import (Dukaan: desktop only), which suggests that adding items on mobile is a known friction point.

### Gaps
- No time-to-first-bill or completion metrics found for any app; a direct review-mining pass on Play Store would be needed.
