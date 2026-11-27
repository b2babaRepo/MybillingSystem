# Indian Paint Dealer Workflows and Pain Points

Scope: Asian Paints, Berger, Nerolac, Indigo and Birla Opus dealers in India, as of late 2026. Most sources are brand pages, App Store listings and vendor marketing, so feature claims are only as good as those sources. No YouTube vlogs or trade articles were checked in this pass.

## Tinting machines: shade codes, base + colorant, formula records, data export

### Takeaway
At the counter the customer gives a shade code, and the dealer tints a base (white or a tint base) with colorants from a brand-supplied dispensing machine. The shade formulas sit in the brand's tinting software, and dealers can add custom shades there. I found no source saying a dealer can export tinting data to outside billing software, so a "shade used on which bill" log is a gap that software can fill.

### Cited Findings
- Asian Paints tells customers to bring any shade code to their local Colour World dealer, who will match it or order the exact colour. Not all shades are available in all products. — [Asian Paints](https://www.asianpaints.com/content/id.html)
- Asian Paints dealers can add shade formulations in the customised shade section of the tinting machine. — [Asian Paints My Profile](https://www.asianpaints.com/my-profile.html)
- Asian Paints says shades shown on its website are indicative only. — [Asian Paints](https://www.asianpaints.com/content/ap.html)
- A third-party guide says the same shade name can carry different codes in different product lines and finishes, so the code must be checked for the exact product. This is not verified against an Asian Paints source. — [aapkapainter](https://aapkapainter.com/blog/explore-the-latest-asian-paint-shade-cards-for-your-home-aapkapainter)
- Berger's 1000-shade Colour Bank was developed with Italy's Ital Tinto. This is a historical article. — [Business Standard](https://www.business-standard.com/article/specials/paint-on-tap-197091001159_1.html)
- Global tinting software includes Innovatint (Vibrantz), which links factory and point-of-sale formula data, and newer versions send orders to machines through a web browser. Others are FAST & FLUID Tintelligence and the Datacolor electronic formula book. — [Vibrantz Innovatint](https://vibrantz.com/innovatint); [Innovatint 4 brochure](https://vibrantz.com/wp-content/uploads/2023/08/Vibrantz_CS_Innovatint-4_Brochure.pdf); [Tintelligence](https://www.directindustry.com/prod/fast-fluid/product-132039-2795713.html); [Datacolor](https://www.datacolor.com/wp-content/uploads/2022/01/Paint-specsheet-EN.pdf)
- Generic dispensers sell in India for about Rs 30,000 to Rs 1.75 lakh (one example is a 100 L automatic unit at Rs 1,75,000). — [IndiaMART](https://dir.indiamart.com/impcat/paint-dispenser.html)
- Indian dealer software already sells this feature. BluePrimes has a mixing log that records date, shade, pack size, customer or project and formula reference "for audit and repeat orders". BUSY claims tinting recipes and stock by shade and batch. — [BluePrimes](https://blueprimes.com/?p=210); [BUSY paint shop](https://busy.in/accounting-software/paint-shop/)

### Inferences
- The brand machine and its software are a closed system, so a third-party app can't easily read formulas from it. The practical feature is a "shade note" on each bill line: brand, product, base, shade code or name, pack, and an optional colorant recipe or photo of the machine slip. A repeat order is then found by customer, phone or site.
- Repeat orders matter because shade codes differ by product line and the shade must match years later for touch-ups.

### Gaps
- Whether Asian Paints, Berger or Nerolac tinting machines export CSVs or offer an API to dealers. No source found.
- Who owns the machine (brand-placed or dealer-bought) and the deposit terms. Not found.
- Which software each brand uses (in-house or Innovatint or Corob). Not confirmed.

## Painter and contractor loyalty, and dealer-side painter tracking and commission

### Takeaway
Every major brand runs a painter or contractor app. Points are earned through QR coupons inside packs or through purchases the dealer uploads. The dealer's own informal painter commission ("painter cut") and "which painter brought this customer" are not tracked by the brand apps. They are an obvious feature for shop software.

### Cited Findings
- Asian Paints Masterstrokes (India) is a loyalty app for AP contractors. It shows points earned and redeemed, tier status and a dedicated Masterstrokes officer. Dealers upload points to contractors, and contractors get an SMS. Some reviews complain about login and redemption failures. — [App Store Masterstrokes](https://apps.apple.com/in/app/masterstrokes/id6445817469); [Scribd doc](https://www.scribd.com/document/640014869/Untitled)
- I found no evidence of an Asian Paints program called "Colour Idea". The India contractor program found is Masterstrokes. — [search results above](https://apps.apple.com/in/app/masterstrokes/id6445817469)
- Birla Opus ID is an engagement program for painters and contractors. Members earn benefits on each Opus purchase by scanning a QR coupon. The App Store rating is 4.3 from 12 ratings. "Birla Opus Dealer Connect" is listed as another app by the same developer, but no details were found. — [App Store Birla Opus ID](https://apps.apple.com/in/app/birla-opus-id/id6476536889)
- Birla Opus Paintcraft Pulse shows about 3.4K Android installs and a 2.45 rating (third-party tracker data). — [AppGoblin](https://appgoblin.info/apps/com.nowappstech.birla_opus_paintcraft_pulse)
- Nerolac RRK is an exclusive loyalty program for Nerolac paint contractors, who earn points by buying and syncing purchases. — [App Store Nerolac RRK](https://apps.apple.com/us/app/id1462264410)
- Berger has a contractor program, Berger Value Club, an Express Painting app, and a page for becoming a painter. — [Berger Value Club](https://apps.apple.com/us/app/id1533057353); [Berger become a painter](https://bergerpaints.com/become-a-painter); [Berger Express Painting blog](https://www.bergerpaints.com/blogs/ultimate-guide-to-using-the-express-painting-app)
- Indigo Paints has a painter app, DecorPlus, with schemes, contests or leaderboards, and refer-and-earn. The only source is the developer's case study. — [Antino case study](https://antino.com/case-studies/indigo-paints)
- An overview of Indian painter loyalty programs (a vendor blog) — [almonds.ai](https://almonds.ai/tag/painter-loyalty-program/)
- BluePrimes keeps separate profiles for painters, contractors and interior designers. BUSY claims automatic commission calculation. — [BluePrimes](https://blueprimes.com/?p=210); [BUSY](https://busy.in/accounting-software/paint-shop/)

### Inferences
- Painters drive a large share of paint purchase decisions, which is why every brand pays them directly. The dealer has to keep the painter loyal to the shop, not just to the brand. Useful features: a painter field on each bill, a per-painter monthly sales report, a commission rate (percentage or Rs per litre) with a payable ledger, and a WhatsApp statement to the painter.
- A painter often buys on credit for a customer's site. Credit gets tracked by site and painter, and a payment can come from either the homeowner or the painter.

### Gaps
- Typical painter commission rates paid by dealers. No reliable source.
- Point values per litre in each brand program. Not published in the sources found.

## Dealer schemes, rebates, cash discounts, credit notes and ordering portals

### Takeaway
Brand discounts and incentives to dealers are large enough that the Asian Paints auditors list them as a key audit matter. Ordering happens on brand dealer apps such as Asian Paints MyAwaaz. I found no public source on the exact slab or cash-discount mechanics.

### Cited Findings
- Asian Paints' auditors list "discounts and incentives" to dealers as a key audit matter, administered through various schemes, with liabilities accrued. — [Asian Paints FY25 standalone statements](https://www.asianpaints.com/content/dam/asianpaints/website/secondary-navigation/investors/annual-reports/2024-2025/Standalone-Financial_Statements.pdf)
- MyAwaaz is the Asian Paints dealer app, with a dashboard, place order, order tracking, reports, transactions and a product information hub. It is rated about 3.9 in India, and reviews mention login failures and crashes. — [App Store MyAwaaz](https://apps.apple.com/in/app/myawaaz/id1077292745)
- Other Asian Paints apps are Unnati, Home Decor MyAwaaz (for decor partners) and DecorPulse (stock check for shop staff). — [Unnati](https://apps.apple.com/us/app/id1516759996); [DecorPulse](https://apps.apple.com/us/app/decorpulse/id1548474160)
- Berger eSambandh lets contractors buy online with delivery to site. This is a secondary source. — [almonds.ai](https://almonds.ai/tag/painter-loyalty-program/)

### Inferences
- Scheme credit notes arrive from the company after the period ends. The dealer needs to log the expected rebate per scheme and match it to the credit note received, and net profit should include it. That is a supplier credit note entry plus a scheme tracker.
- Brand apps cover ordering from the company, so shop software should not duplicate this. It should import or log the purchase invoice instead.

### Gaps
- Actual cash discount percentages, quantity slab structures, dealer margins, and Berger or Nerolac or Indigo dealer portals. Not found in public sources.

## Pack sizes, damaged and expired stock, GST and HSN

### Takeaway
Paint under HSN 3208 (solvent-based) and 3209 (water-based) is taxed at 18% GST. It was cut from 28% in 2018 and left unchanged by the September 2025 revision. Thinner (3814), brushes and rollers (9603) are also 18%. Stock is kept by product, shade or base, and pack size.

### Cited Findings
- 3208 and 3209 are at 18% since 2018 and were not affected by the September 2025 revision. Some pages still show an outdated 28%. — [ClearTax](https://cleartax.in/s/paint-hsn-code-and-gst-rate); [Tally](https://tallysolutions.com/gst/paint-and-coatings-hsn-code-gst-rate/); [Accountune](https://www.accountune.com/hsn-code-paint); [BUSY (conflicting tables)](https://busy.in/gst-rates/paints/)
- 3208 covers synthetic or solvent paints and enamels, and 3209 covers water-based paints (emulsions). Spray cans follow the paint inside. Thinner 3814 and brushes 9603 are at 18%. — [GimBooks](https://www.gimbooks.com/blog/paint-hsn-code-3208-gst-rate); [ClearTax 3209](https://cleartax.in/s/paints-varnishes-dissolved-aqueous-medium-gst-rates-hsn-code-3209)
- BUSY markets stock by shade and batch, pack sizes and multi-godown storage for paint shops. — [BUSY](https://busy.in/accounting-software/paint-shop/)

### Inferences
- Common pack ladders are 200 ml, 500 ml, 1 L, 4 L, 10 L and 20 L for emulsions, and 1 kg to 40 kg for putty. Each product needs pack-wise MRP and stock, and a tinted can is a made-to-order item (base plus colorant cost). Not sourced. This is based on general trade knowledge.
- Damaged or dented cans and expired (shelf-life) batches need a write-off or return-to-company entry with batch and expiry dates.

### Gaps
- Brand return policy for damaged or expired stock. Not found.
- Official CBIC notification not checked directly. Verify before invoicing.

## Site estimation: area to litres and house quotations

### Takeaway
The standard method: paintable area (walls minus doors and windows) divided by coverage per litre, times the number of coats. Primer and putty are separate lines. Coverage figures in India vary from 80 to 170 sq ft per litre per coat depending on product and surface, so a calculator should take coverage from the product master.

### Cited Findings
- Formula: litres = (paintable area / coverage per litre) x coats. Example: 360 sq ft at 130 sq ft per litre for 2 coats is about 2.8 L. — [NoBroker](https://www.nobroker.in/blog/how-much-paint-is-required-to-paint-a-room)
- Asian Paints and Nerolac each have an online quantity or budget calculator and coverage guidance. — [Asian Paints calculator](https://www.asianpaints.com/resources/tools/paint-budget-calculator); [Asian Paints blog](https://www.asianpaints.com/blogs/how-much-paint-is-needed-to-paint-house-walls.html); [Nerolac coverage](https://www.nerolac.com/bn/wall-paint/what-is-paint-coverage)
- Coverage is 120 to 170 sq ft per litre per one source, and about 100 sq ft per litre on smooth primed walls (80 to 100 on rough walls) per another. A forum post gives inconsistent figures. — [NoBroker forum](https://www.nobroker.in/forum/how-to-calculate-paint-coverage-in-litres/); [NoBroker](https://nobroker.in/forum/how-to-calculate-paint-quantity)

### Inferences
- A dealer quotation tool would work room by room: L x B x H, minus openings, coats, then the product's coverage. Round up to an optimal pack mix (for example 20 L + 4 L + 1 L). Add primer, putty, thinner and labour per sq ft, then convert the quotation to a bill or order. Sending it on WhatsApp is the natural flow.

### Gaps
- Typical painter labour rates per sq ft by city. Not researched.
