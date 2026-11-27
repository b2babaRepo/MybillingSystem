# Indian hardware, sanitaryware, plumbing, electrical and building-material shops: daily workflow and pain points

Note: most sources on shop workflow are billing-software vendor marketing pages (myBillBook, Vyapar, Munim, VasyERP, BillClap, BluePrimes). They describe real problems but are not neutral. No YouTube shop-owner videos were reviewed (not fetchable in this run).

## Huge SKU counts, sizes and units (piece/box/kg/foot/metre, cut lengths, loose sales)

### Takeaway
Shops hold 1,000+ SKUs where one product exists in many sizes, brands and tax classes, and a single bill mixes cement, pipes, paint, fasteners and electricals. Software needs size/variant items, several units per item with conversion, and fast search.

### Cited Findings
- Hardware businesses handle thousands of products with different sizes, brands, specifications and tax classifications — [Munim](https://themunim.com/retail/hardware-shop)
- Shops with 1,000+ SKUs need software covering pipes, fittings, tools, paints and wire — [Accountune](https://accountune.com/hardware-store-billing-software)
- One bill often mixes cement, pipes, paints, fasteners and electricals — [BillClap](https://www.billclap.com/billing-software-for-hardware-stores)
- Common complaints: physical stock not matching software records, and slow GST billing in peak hours — [TallyAtCloud](https://www.tallyatcloud.com/articles/gst-billing-software-for-hardware-traders-in-bathinda-focal-point-industrial-area-mangaluru-kiadb-industrial-area-2026-guide)
- Wire is sold by coil of fixed length (for example 90 m), with list prices per coil by size (sq mm) — [Havells Consumer Cables price list](https://havells.com/content/dam/havells/brouchers/Consumer-cables/Havells_ConsumerCables.pdf)

### Inferences
- Must-haves: an item with a size grid (for example a PVC elbow in 1/2", 3/4", 1"... in SCH40/SCH80), a base unit plus alternate units (coil to metre, box to piece, bag to kg), decimal quantity, and cut-piece sales that leave a remainder in stock (a coil cut to 23 m leaves a 67 m remainder).
- Loose screws and nails are sold by weight or by count. A "loose" item that pulls from a box SKU avoids stock drift.
- Search by brand + size shorthand ("astral 1 inch tee") matters more than barcode, because many fittings carry no barcode.

### Gaps
- No firm data on typical SKU counts by shop size. No first-hand shop-owner sources were found on cut-length handling.

## Contractor, plumber and electrician customers: site accounts, credit, brand reward programmes

### Takeaway
Tradespeople (contractors, plumbers, electricians) bring repeat business, usually on credit, often split by job site. Brands run their own loyalty apps that pay tradespeople directly by scanning a code or entering purchase value. Shops end up keeping site-wise khata and informal commission for these customers.

### Cited Findings
- Hardware billing vendors highlight contractor credit tracking as a key need — [Munim](https://themunim.com/retail/gst-billing-software-for-hardware-shop/); [BillClap](https://www.billclap.com/billing-software-for-hardware-stores)
- Havells EPLUS is an electrician-only app: earn and redeem points, bank transfer, vouchers and mobile top-ups, and Havells/Crabtree/Standard price lists — [App Store](https://apps.apple.com/app/id1453504335)
- Havells electrician programme needs a PAN copy; without PAN, 20% TDS is deducted on rewards — [Havells PDF](https://loyaltyuat.havells.com/NotificationUpload/242_flashAttachment.pdf)
- Havells also runs a channel/retailer portal ("Power Plus / Sampark") — [Havells](https://havellspowerplus.havells.com/Authorize/AboutSampark)
- Polycab Red Carpet Program is for architects, contractors and consultants — [App Store](https://apps.apple.com/in/app/polycab-red-carpet-program/id1644677287). A blog claims the Polycab Experts app gives instant bank transfer per scanned code, three tiers and insurance (not officially confirmed) — [Buyerr blog](https://buyerr.in/blogs/electrician-loyalty-program-brand/)
- Finolex runs the Super Plumber programme based on valid purchases from authorised dealers, and the Finolex Samruddhi app (scan single or multiple SKUs for points) — [App Store](https://apps.apple.com/in/app/finolex-samruddhi-loyalty/id6474528387)
- Astral plumbers get a unique code to claim points on pipes and fittings bought from authorised dealers, valued on the pre-GST purchase value. Bondtite adhesives carry a scan-QR — [Astral Adhesives](https://www.astraladhesives.com/blog/know-how-to-turn-bondtite-purchases-into-big-rewards)
- Prince Pipes runs Prince Udaan, and Ashirvad runs a plumber loyalty programme — [Prince](https://princepipes.com/prince-udaan-loyalty-program); [Ashirvad](https://www.ashirvad.com/loyalty/)

### Inferences
- The shop's bill is often the proof a plumber needs to claim brand points (pre-GST value, authorised dealer). A bill that clearly shows brand, item and pre-tax value per line, tagged to the plumber, has real value.
- Useful features: a party with sub-accounts per site/project, a "referred by" plumber/electrician on each sale (where the end customer pays), a shop's own commission ledger for the plumber, and statements per site.
- The shop should not copy brand programmes. Instead it can track its own commission and remind tradespeople of their dues.

### Gaps
- No official Supreme Industries plumber programme page was found. Commission percentages that shops typically pay plumbers are undocumented (informal practice).

## Quotations/estimates to bills, delivery challans, partial deliveries, site returns

### Takeaway
A typical project sale goes estimate, then material issued in parts against challans, then returns of unused material, then a final bill or settlement at the end. This chain is where generic grocery-style billing fails.

### Cited Findings
- Workflow described by a vendor: the sale starts with a quotation, material is issued in parts, delivery challans are linked to a final bill later, unused or wrong items are returned with stock adjustment, and the account is settled at project end — [BluePrimes](https://blueprimes.com/?p=206)
- Manufacturer return terms are strict: Hilti India accepts some returns within 30 days, only unused, saleable, in original packing, and kits must come back in full — [Hilti](https://www.hilti.in/content/hilti/A2/IN/en/support/info-center/material-return-policy.html)
- In public-sector contracts, unused material not returned is recovered at issue rate + 25%, and site stock is checked periodically — [EIL conditions via 1library](https://1library.net/article/return-unused-material-conditions-issue-materials-cement.y9j37xjq)

### Inferences
- Needed: quotation to sales order to multiple challans (each reducing stock), a challan-to-invoice merge, a site return note (credit note or challan-in), and a "balance pending to deliver" view per quotation.
- Return rules: allow returns only of items on earlier bills for that party/site, and block cut pieces and opened boxes or price them differently.

### Gaps
- No data on what share of sales are quotation-based versus counter sales.

## Price-list changes, dealer price vs MRP, bulk rate updates

### Takeaway
Wire and cable prices follow copper and are revised often (fortnightly or monthly in volatile periods). Each new brand list supersedes the old one, so shops need bulk price updates by brand/category or by percentage.

### Cited Findings
- Copper rose over 60% on MCX in 2025, hitting Polycab, Finolex Cables, KEI and Havells — [Upstox](https://upstox.com/news/market-news/stocks/copper-prices-soar-60-in-2025-four-key-sectors-that-could-face-the-heat-of-price-rise-check-details/article-187353)
- Wire companies pass price hikes to distributors or consumers on a fortnightly or monthly basis — [Upstox](https://upstox.com/news/market-news/stocks/copper-prices-soar-60-in-2025-four-key-sectors-that-could-face-the-heat-of-price-rise-check-details/article-187353)
- Havells issued dated lists (consumer cables from 22 Jan 2025, wires from 15 Apr 2025), each superseding earlier lists — [Havells price list PDF](https://havellspowerplus.havells.com/NotificationUpload/285_flashAttachment.pdf)
- Dealers stock up ahead of announced increases — [Ventura](https://www.venturasecurities.com/news/stocks/polycab-eyes-q2-support-from-copper-led-buying-as-competition-intensifies/)
- Copper was up about 50.7% in Jan 2026 versus Jan 2025 levels (ICICI Securities) — [Business Today](https://www.businesstoday.in/markets/stocks/story/copper-prices-up-50-havells-polycab-shares-down-7-9-in-jan-concerns-overdone-512415-2026-01-22)
- Havells reportedly plans 5–7% price hikes in Q3 FY27 — [News Karnataka](https://newskarnataka.com/business/havells-plans-5-7-price-hikes-in-q3-fy27/23102026/)

### Inferences
- Brands publish a list price (near MRP). Dealers buy at the list minus a trade discount, which can be a multi-level discount. Retail and contractor rates are a discount off the list. Software should store the list price plus a discount % per customer type, so one list update reprices everything.
- Needed: import a brand price list (CSV/PDF to items), a percentage update by brand or category, the effective date kept as history, and a warning on quotations older than the latest revision.

### Gaps
- No public source on the exact trade discount structures. Polycab's own dated price lists were only seen via an aggregator.

## GST rates, HSN, e-way bills

### Takeaway
After GST 2.0 (22 Sep 2025), cement fell to 18%. Most hardware items stay at 18%. The PVC pipe rate is reported inconsistently, so check it against the CBIC notification. E-way bills apply above Rs 50,000 consignment value, including challan movements.

### Cited Findings
- Cement (HSN 2523) cut from 28% to 18% from 22 Sep 2025 — [Business Standard](https://www.business-standard.com/industry/news/gst-rate-cut-on-cement-to-boost-infrastructure-125090401466_1.html); [Tally](https://tallysolutions.com/gst/cement-gst-percentage-rate-hsn-code-applicability/); [PIB](https://pib.gov.in/FactsheetDetails.aspx?Id=150297)
- Sanitaryware at 18%, no change — [ConstructionPlacements](https://www.constructionplacements.com/gst-rates-on-construction-services-materials-india-sept-2025-update)
- PVC pipes (HSN 3917): one source lists 18% before and after 22 Sep 2025 — [ConstructionPlacements](https://www.constructionplacements.com/gst-rates-on-construction-services-materials-india-sept-2025-update); contradicted by [JK Cement blog](https://www.jkcement.com/gst-2-0-home-construction-guide-2025/), which claims 5%. Unresolved; the 18% figure looks more likely and must be checked against the CBIC rate notification.
- E-way bill needed when consignment value (invoice, bill of supply or delivery challan) exceeds Rs 50,000. Some states set different intra-state limits. Non-sale movements (stock transfer, return of goods) are also covered — [ClearTax](https://cleartax.in/s/eway-bill-gst-rules-compliance); [Busy](https://busy.in/gst/state-wise-threshold-limits-for-e-way-bills)

### Inferences
- An HSN master should ship by category: 2523 cement, 3917 plastic pipes/fittings, 6910 ceramic sanitaryware, 8544 wires/cables, 7318 screws/bolts, 8481 taps/valves, 3208/3209 paints. The rates must be editable and checked against official notifications.
- Truck deliveries of cement, steel and pipes often cross Rs 50,000, so offer an e-way bill prompt or export on the invoice/challan.

### Gaps
- I did not verify the rates above for the listed HSNs (other than cement) against the official GST notification.

## Stock counting, dead stock, theft

### Takeaway
Stock mismatch is the most-cited operational pain. Small, high-value items (fittings, MCBs, taps) and loose items leak easily. Dead stock builds up from discontinued sizes and brand changes.

### Cited Findings
- Physical stock not matching software records is a common problem — [TallyAtCloud](https://www.tallyatcloud.com/articles/gst-billing-software-for-hardware-traders-in-bathinda-focal-point-industrial-area-mangaluru-kiadb-industrial-area-2026-guide)
- Vendors offer flags for items sitting too long, for clearance or supplier return — [BluePrimes](https://blueprimes.com/?p=206)
- Multi-godown stock is a stated need — [VasyERP](https://www.vasyerp.com/retailing/accounting-software-hardware-stores)

### Inferences
- Useful: cycle counts by shelf or category (not the full store at once), shrinkage reports per category, an ageing report (no sale in 90/180 days), and godown-wise stock for cement and pipes.

### Gaps
- No quantified Indian data on theft or shrinkage rates in hardware retail was found.
