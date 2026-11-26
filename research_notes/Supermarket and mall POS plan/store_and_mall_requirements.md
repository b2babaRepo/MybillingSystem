# POS Operational Requirements: Indian Supermarkets/Mini-marts and Mall Tenants (incl. mall operator requirements)

> **Method note (read first):** In this session, WebFetch could not reach any outside host (DNS blocked for vasyerp.com, gofrugal.com, zoho.com, fynd.com). Every finding below comes from search-result snippets, not full-page reads. Most sources are vendor marketing pages, which are good evidence of the *expected feature set* but weak evidence of performance numbers. Treat numbers as indicative. The "Inferences" sections give the must-have and nice-to-have classification, which is my synthesis, not a sourced fact. Searches ran on 2026-11-26. Sources are 2024–2026 unless flagged.

## 1. Checkout speed and counter operations (supermarkets / mini-marts)

### Takeaway
The table-stakes checkout for an Indian supermarket is:
- barcode scanning plus weighing-scale/PLU billing for loose items;
- several counters billing against one live stock pool;
- hold/suspend bills;
- shift-wise cashier cash control and reconciliation;
- mixed tender (cash/card/UPI/wallet);
- above all, **billing that keeps working when the internet or power drops**.

I found no reliable independent benchmark for bills per hour per counter. Vendor figures disagree by an order of magnitude.

### Cited Findings
**Hardware and peripherals**
- Supermarket POS is expected to drive barcode scanners, weighing scales, cash drawers, printers and customer-facing displays. — [Zoho POS supermarket page](https://www.zoho.com/en-in/pos/solutions/supermarket-pos)

**Barcodes and weighing scales**
- Expected features: PLU codes for fast lookup of loose items, scale integration, and automatic price calculation by weight. — [Vyapar grocery billing](https://vyaparapp.in/grocery-billing-software)
- LOGIC ERP lists weighing-scale integration as a headline feature, together with multi-store management, real-time inventory sync and promotion/scheme management. — [LOGIC ERP supermarket software](https://www.logicerp.com/retail/supermarket-retail-software)
- Barcode generation, label printing and scanning are standard ("barcode generation and scanning features for precise and quicker bill preparation"). — [MyBillBook supermarket](https://mybillbook.in/s/?p=27903); [Tally supermarket POS article](https://tallysolutions.com/billing-software/best-supermarket-billing-and-pos-software/)

**Scale barcode format (international, not India-specific)**
- Prefixes 21/22/23/27 = price embedded; 28/29 = weight embedded. — [SAP POS docs](https://help.sap.com/doc/saphelp_pos23/2.3/en-US/5e/7ca28d23cc4ddf969366d803e00816/content.htm)
- Another vendor treats prefix 21 as "weight-dependent". — [ready2order](https://support.ready2order.com/l/en/article/5ggv2lrww3-connect-scales-and-other-barcode-enabled-devices-to-ready-2-order)
- So the prefix meaning is scale-configured, and the POS must let users configure it.
- Label structure: flag digit, 4–5 digit PLU (must match the item's PLU in the POS), price or weight, check digit. — [Tower Systems](https://help.towersystems.com.au/portal/en/kb/articles/price-embedded-barcodes); [Shift4](https://shift4.zendesk.com/hc/en-us/articles/6946937466899-Random-Weight-Barcodes-on-Harbortouch-Retail)
- Price-embedded is recommended over weight-embedded. — [LS-100 instructions](https://cdn.shopify.com/s/files/1/0613/4205/6698/files/LS-100_Weight_UPC_Instructions.pdf)

**Hold bills, queue-busting, shift cash control**
- Cashiers can "suspend and resume bills" to handle peak rushes. — [LOGIC ERP supermarket POS](https://www.logicerp.com/retail-pos-software/supermarket-pos-software)
- Tablet "queue buster" mobile POS is used in rush hours. — [LOGIC ERP supermarket POS](https://www.logicerp.com/retail-pos-software/supermarket-pos-software)
- Shift-based cashier operations, with the cash drawer tracked per shift to reduce fraud and discrepancies. — [LOGIC ERP](https://www.logicerp.com/retail/supermarket-retail-software)
- Reports expected: sales per hour, margin per category, slow/fast-moving SKUs, stock valuation, shrinkage, cash reconciliation. — [LOGIC ERP supermarket POS](https://www.logicerp.com/retail-pos-software/supermarket-pos-software)

**Payments**
- Multi-payment (cash, card, UPI, wallets) is expected. — [LOGIC ERP grocery POS](https://www.logicerp.com/retail-pos-software/grocery-pos-software)

**Throughput and store size (vendor claims, conflicting)**
- "Hundreds of items/transactions per hour" across counters. — vendor claims summarised from [MyBillBook queue blog](https://mybillbook.in/blog/how-supermarkets-can-reduce-checkout-queues/) and [LOGIC ERP checkout guide](https://www.logicerp.com/blog/supermarket-checkout-management-a-complete-guide-to-billing-counters-queue-management-and-pos-operations/)
- By contrast, "500+ bills daily during peak". — [BillingFast supermarket](https://billingfast.com/free/supermarket-billing-software)
- Customer expectation: checkout in under 2 minutes; "traditional POS" takes 2–3 minutes (vendor comparison). — [BillingFast](https://billingfast.com/free/supermarket-billing-software)
- Counter-sizing formula: counters = (customers/hour × avg checkout minutes) / (60 × target utilisation). The illustrative store has 120 customers/hour. — [LOGIC ERP checkout guide](https://www.logicerp.com/blog/supermarket-checkout-management-a-complete-guide-to-billing-counters-queue-management-and-pos-operations/)
- Typical SKU counts: supermarket 5,000+ products. — [BillingFast](https://www.billingfast.com/free/grocery-store-billing-software)
- Hypermarket 10,000+ SKUs with "dozens of counters". — [SwilERP](https://www.swindia.com/product/supermarket-software)

**Offline and multi-counter reliability**
- Billing must survive broadband drops and power cuts, which rules out cloud-only tools. — [TheHobbyist (2025)](https://www.thehobbyist.in/blog/free-billing-software-kirana-india.html); [RetailDaddy](https://retaildaddy.in/blog/best-retail-billing-software-for-small-shops-and-kirana-stores)
- Multi-counter stock desync is a known failure: one counter sells while another still shows the stock. — [Hedgethink multi-counter guide](https://www.hedgethink.com/best-pos-billing-software-for-small-multi-counter-shops-in-india-real-cost-staff-training-test/)
- Free plans that limit users or force offline-only mode "break the moment a second counter is added". — [Hedgethink](https://www.hedgethink.com/best-pos-billing-software-for-small-multi-counter-shops-in-india-real-cost-staff-training-test/)
- Usability bar: "if the billing process takes more clicks than a manual entry, the software is a failure". — [RetailDaddy](https://retaildaddy.in/blog/best-retail-billing-software-for-small-shops-and-kirana-stores)

### Inferences
**MUST-HAVE (supermarket, 1–10 counters)**
- Scanner input, plus fast item search by code/name/PLU.
- Configurable scale-barcode parsing (prefix, PLU length, price vs weight, decimals) and scale PLU export.
- Several counters on one stock pool with live or near-live sync.
- Local or offline billing with queued sync. A LAN server or local DB per counter is the usual answer to outages.
- Hold/park and recall bills.
- Cashier login with shift open (float), shift close and cash declaration (variance vs system), a day-end Z-style summary, and cash drawer kick.
- Split tender across cash, UPI, card and wallet.
- Returns/exchanges against the original bill number from any counter.
- Thermal receipt printing.
- GST-correct bill with mixed slabs and HSN. (GST detail is probably covered by another researcher.)

**Strongly expected**
- Keyboard-only billing with hotkeys. This is implied by the "fewer clicks than manual" bar, but I found no direct source for hotkeys.
- Customer display.
- Hourly sales report.

**NICE-TO-HAVE**
- Mobile queue-buster.
- Self-checkout.

**Throughput target**
- A design target of roughly 1–2 minutes per basket implies about 30–60 bills/hour per counter at peak.
- This is my arithmetic from the "<2 min checkout" claim, not a measured figure.
- The POS itself should add near-zero latency per scan. Search and scan response must not depend on the internet.

### Gaps
- No independent or measured bills-per-hour or items-per-minute data for Indian supermarkets. Vendor figures conflict ("hundreds per hour" vs "500+ per day").
- No source on typical counter counts for tier-2/3 independents.
- No India-specific confirmation of which scale-barcode prefix local scale makers (e.g., Essae) default to. I could not find Essae documentation.
- No direct source on keyboard-shortcut expectations or on Z-report terminology in Indian POS.

## 2. Store operations (inventory, purchasing, pricing, promotions, loyalty, multi-store)

### Takeaway
Indian supermarket POS is expected to be a light ERP. It needs:
- batch/MRP-wise stock with FEFO and expiry handling;
- purchase/GRN;
- a rich scheme engine (BOGO, combo, quantity- and value-slab, time-based, customer-specific);
- loyalty points with OTP redemption;
- margin/MRP guardrails;
- multi-store HQ control with stock transfer and shrinkage reporting.

### Cited Findings
**Batch, MRP and expiry**
- Batch-wise and MRP-wise stock, with FEFO (First Expiry First Out) applied at billing. — [LOGIC ERP grocery](https://www.logicerp.com/retail-pos-software/grocery-pos-software); [GoFrugal supermarket](https://gofrugal.com/retail/supermarket-pos)
- Alerts when selling price falls below landing cost, margin falls below expected, or price goes above MRP. — [GoFrugal supermarket POS](https://gofrugal.com/retail/supermarket-pos)

**Promotion and scheme engine**
- Promotion types listed: "Buy Get promo, Qty based slab wise promo, Value based slab wise promo, Combo promo". — [GoFrugal hypermarket billing](https://gofrugal.com/retail/hypermarket-software/hypermarket-billing-pos.html)
- Another vendor lists: "discounts, combo offers, buy-one-get-one deals, time-based schemes, & special customer offers". — [LOGIC ERP grocery](https://www.logicerp.com/retail-pos-software/grocery-pos-software)
- Slab pricing based on sale quantity. — [GoFrugal](https://gofrugal.com/retail/supermarket-pos)
- Schemes cause inventory leakage when "free" items aren't recorded correctly, so free goods must decrement stock. — [Tally price lists/schemes](https://tallysolutions.com/tally/price-lists-discounts-dealer-schemes-tallyprime/)

**Loyalty**
- Loyalty points per purchase. — [GoFrugal](https://gofrugal.com/retail/supermarket-pos)
- OTP-based secure redemption of points and coupons. — [LOGIC ERP](https://www.logicerp.com/retail-pos-software/grocery-pos-software)

**Multi-store and HQ control**
- HQ-to-outlet chain features (central masters, pricing, transfers). — [GoFrugal retail chain features](https://www.gofrugal.com/retail-chain-features.html)
- Multi-store management with real-time inventory sync. — [LOGIC ERP](https://www.logicerp.com/retail/supermarket-retail-software)
- Shrinkage reporting and stock valuation. — [LOGIC ERP supermarket POS](https://www.logicerp.com/retail-pos-software/supermarket-pos-software)

**Data-integrity pain points from user reviews**
- Purchases edited after entry were double-counted in the cash ledger.
- The inventory report did not compute profit correctly (cost vs sale).
- Sales returns were hard to process.
- "Offline mode not working".
- Source: Google Play reviews of a retail POS app. — [Google Play: POS App, Retail Billing POS](https://play.google.com/store/apps/details?id=com.webkul.mobikul.pos&hl=en_IN)
- Manual-billing errors that drive adoption: "₹120 becomes ₹102", credit (udhaar) confusion, stock mismatches. — [Elixir Retail360 kirana story](https://elixirretail360.com/blog/pos-system-kirana-shop-story/)

### Inferences
**MUST-HAVE**
- Item master with several barcodes per item and several MRPs/batches per item. At billing, prompt for the batch/MRP or auto-pick by FEFO.
- Expiry reports and near-expiry alerts.
- Purchase entry/GRN from distributor invoices, with free-quantity handling, supplier-wise payables and purchase returns.
- Price change with shelf-label/barcode reprint.
- A scheme engine covering BOGO, combo, quantity slab, value (bill-amount) slab, date/time-window offers ("happy hours") and member prices.
- Customer database keyed on mobile number, with credit (udhaar) ledger and loyalty points.
- Stock adjustment for damage, expiry or theft, with a reason code (shrinkage).

**For small chains (2–10 stores): MUST-HAVE**
- Central item and price master pushed to stores.
- Inter-store stock transfer (out and in, with in-transit status).
- Consolidated reports.

**NICE-TO-HAVE**
- Auto reorder suggestions.
- Gift vouchers (I found little India-supermarket evidence beyond general mentions).
- Delivery and pickup orders.
- Margin guardrail alerts. These are a differentiator, cited by GoFrugal.

### Gaps
- I found no source quantifying how often price changes happen or how many promotions run at once in Indian supermarkets.
- No India-specific gift-voucher usage data for supermarkets.
- No source explaining how BOGO, member price and batch MRP interact on one bill. This needs vendor documentation or a demo.

## 3. Mall-specific: what mall operators require from tenants' POS and how sales reporting works

### Takeaway
Indian mall leases are mostly a minimum guarantee plus revenue share (typically about 6–12%, "whichever is higher"). Operators therefore need daily tenant sales.

In India this is mainly done through **ADSR (Automated / All Day Sales Reporting)**. A third-party ADSR vendor, such as Onintouch, which says it serves the Phoenix Mills portfolio, either:
- pulls sales from any tenant POS without changes to that POS, or
- receives sales through POS-vendor "mall integration" modules (GoFrugal and VasyERP both ship ADSR/mall integration).

Channels globally include API, SFTP file (hourly or daily), email and manual entry. Food courts use centralised multi-counter or prepaid-card systems that report to the mall.

### Cited Findings
**Lease economics: why malls need sales data**
- Phoenix leases have two parts: a fixed minimum rent plus a "revenue share" of the store's actual sales. The revenue share kicks in once sales exceed the minimum guarantee threshold. — [smallcase: Phoenix Mills business model](https://www.smallcase.com/blog/phoenix-mills-business-model/)
- Phoenix reported FY26 retail consumption (tenant sales) of about ₹16,578 cr, up 21% YoY, and Q4 FY26 at ₹4,251 cr, up 31%. Tracking consumption is itself a headline KPI, which requires tenant sales capture. — [Free Press Journal](https://www.freepressjournal.in/business/phoenix-mills-reports-16578-crore-retail-consumption-in-fy26-achieves-21-growth-without-new-malls); [DSIJ](https://insights.dsij.in/appmindshare/phoenix-mills-posts-all-time-high-retail-consumption-in-fy26-stock-surges-7-56389)
- Rent lags sales because of lease structures, ramp-up assets, and "high-volume, low-revenue-share categories such as jewellery and electronics". — [Phoenix coverage via search summary, smallcase/JM Financial](https://www.jmfinancialservices.in/market-news-and-insights/1684746)
- Formats in use: pure revenue share; MG plus revenue share; MG or revenue share, whichever is higher. Rates are often 6–12%. — [Orion Malls blog](https://www.orionmalls.com/blogs/retail-leasing-models-fixed-rent-vs-revenue-sharing); [IIFL/JLL explainer](https://www.indiainfoline.com/knowledge-center/share-market/how-revenue-share-works-and-evaluating-a-mall-jll)
- Example: Zara at a Mumbai mall pays a 9.5% revenue share (per headline). — [Retail4Growth](https://www.retail4growth.com/news/at-95-of-sales-zara-offers-highest-revenue-share-to-mumbais-oberoi-mall-2271). The search summary separately cites an 8% example for Zara Palladium. The figures differ and may be different malls or years.
- Worked example: ₹2,00,000 MG vs 8% of net sales, whichever is higher, with GST and TDS on top. — [Patron Accounting](https://www.patronaccounting.com/blog/retail-accounting-mumbai-kalbadevi)
- Sales data "typically tracked via POS systems shared with mall management". — [IIFL/JLL explainer](https://www.indiainfoline.com/knowledge-center/share-market/how-revenue-share-works-and-evaluating-a-mall-jll)

**How reporting works in practice in India: ADSR**
- Onintouch ADSR is "non-intrusive". It works with any tenant POS (Pine Labs, Petpooja, GoFrugal, SAP, Tally, custom) with no changes to tenant systems. — [Onintouch FAQ](https://onintouch.com/faq); [Onintouch home](https://onintouch.com/)
- Onintouch claims 16+ major malls in 10+ Indian cities, including The Phoenix Mills Ltd portfolio, over 6+ years at 99.9% data reliability. These are vendor claims. — [Onintouch about](https://onintouch.com/about-us); [Onintouch vs MRI](https://onintouch.com/onintouch-vs-mri-software)
- Revenue-share malls often add a tenant sales reporting tool such as Onintouch ADSR. — [Lazy Monkey: mall management software India](https://lazymonkey.in/blog/mall-management-software-india/)
- An unnamed Intouch client cites "real-time sales tracking" for tenant billing over six years. — [Intouch Mall](https://www.intouchmall.com/)
- POS-vendor side: VasyERP's ADSR integration lets mall owners see only store sales for commission calculation, "without accessing any other confidential data". — [VasyERP shopping-mall POS](https://www.vasyerp.com/retailing/pos-software-shopping-malls); [VasyERP mall billing](https://vasyerp.com/retail/shopping-mall-billing-software)
- GoFrugal has an "ADSR Mall Integration" module with license sync in its RPOS. The shop owner gives the mall a restricted login that sees sales data only. — [GoFrugal KB](https://community.gofrugal.com/portal/en/kb/gofrugalserveeasy/integrations/adsr-mall-integration/articles/adsr-mall-integration-license-sync-in-rpos)

**Channels and data granularity (mostly non-India vendors, but these are the patterns in use)**
- Tenants push a file to SFTP hourly or daily, or use the POS-vendor API or a REST API. About 1 hour setup per tenant. — [VemTenant](https://vemcogroup.com/products/vemtenant)
- Options listed: PoS, API, SFTP, email, custom; pull and push. — [WovVRA (Wovv Tech)](https://www.wovvtech.com/products/wovvra-ai-sales-data-capture-solution/)
- Pull connectors over POS API, database, file export or a local collector, normalised into a standard model. — [KSI Vision](https://www.ksivision.com/en/pos-sales-integration)
- Hourly sales including line items per bill, used to settle profit-share disputes. — [Xpandmall](https://xpandmall.com/)
- Validated daily and monthly feeds into the mall ERP. — [Genix](https://genixintelligence.com/pos-data-integration-analysis/)
- Malls use capture to curb underreporting. Anomalies such as refunds, employee meals and cash-vs-card mixes are flagged. About 90% of connections are pull. — [Mallsense](https://project7421800.tilda.ws/mallsense-sales-data-capture)
- Comparable market, Malaysia (GTO): tenancy agreements require daily sales submission, with POS vendors providing automated sync. — [HitPay Mall GTO](https://docs.hitpayapp.com/pos/mall-gto); [EasyStore](https://blog.easystore.co/en-my/shopping-mall-gto)
- F&B POS vendors ship "Mall GTO" adapters that format and auto-submit outlet sales to mall requirements. — [Sapaad](https://www-review-dr.sapaad.com/in/?p=97217)

**Food courts and kiosks**
- Food-court POS must run many counters as one system, with top-up once and spend at any stall. Each counter's sales roll up into a master report. — [Petpooja food court/canteen](https://blog.petpooja.com/industry-business-guides/food-court-canteen-pos-multi-counter-billing/)
- Food-court needs: centralised billing, split payments, automated vendor settlement, prepaid card or wallet. Malls need daily reports and GST-compliant sales data. — [DineOpen food courts](https://www.dineopen.com/for/food-courts)
- Food-court reports can be uploaded to mall management software. — [Restroworks food court](https://www.restroworks.com/food-court-management-system/)
- An Indian mall food court uses a debit-card system (CMR Central). **Dated, likely pre-2020.** — [Blooloop](https://blooloop.com/uncategorised/news/fec-debit-card-systems-the-food-court-at-cmr-central-inaugurated-by-minister-of-state-govt-of-india)
- Fynd blog topics (April 2026) on mall kiosks losing weekend sales from setup mistakes; Wildstone scaled to 25 mall kiosks on Fynd POS. I could not read the content. — [Fynd kiosk blog](https://www.fynd.com/blog/we-watched-small-kiosk-owners-lose-sales-every-weekend-it-always-came-back-to-the-same-setup-mistake); [Wildstone case](https://www.fynd.com/customer-stories/when-you-go-from-0-to-25-mall-kiosks-wildstone-x-fynd-pos)

**Mall-wide gift cards and loyalty**
- Phoenix Gift Card: Visa-powered, with ITZCash and HDFC Bank. Accepted at all Phoenix malls nationally, POS transactions only, minimum 1-year validity. Because it is an open-loop card on Visa rails, tenants accept it through their normal card terminal. — [HDFC Phoenix gift card FAQ](https://hdfcbank.com/personal/pay/cards/prepaid-cards/hdfc-phoenix-gift-card/faq); [Electronic Payments International](https://www.electronicpaymentsinternational.com/?p=46573)
- Lulu loyalty has 1.5 lakh+ members. Shoppers redeem mall-brand vouchers by uploading their shopping bills to the mall app. Geo-fencing and ANPR are being added. — [IndiaRetailing](https://www.indiaretailing.com/?p=177771)
- Phoenix "Pulse" is a mall-driven loyalty/deals program. — [SCAI Mall Talk 2025](https://www.scai.in/wp-content/uploads/2025/10/SCAI-mall-talk-Oct-2025_-Issue-51F-1.pdf)
- Mall multi-brand loyalty (e.g., Phoenix Marketcity) uses middleware across tenant POS. — [Fundle guide](https://fundle.ai/guide/retail-loyalty-software-pos-integration-technical-guide-indian-it-teams)

### Inferences
**MUST-HAVE for a POS sold to mall tenants**
- A mall-sales export or ADSR integration that sends only sales data. Tenants guard margin and customer data.
- At least daily (ideally hourly), bill-level totals: bill no., timestamp, gross, discount, returns/voids, net of GST, tender split.
- Delivery via:
  - an ADSR-vendor connector (Onintouch-style pull), or
  - a scheduled SFTP/CSV or REST push in a configurable per-mall format, or
  - a restricted "mall user" read-only login.
- An immutable bill sequence and an audit trail of voids and returns, because malls flag underreporting anomalies.
- Clean net-sales definitions (excluding GST, after returns) that match the lease's revenue-share base.

**Food courts and kiosks: MUST-HAVE**
- Small-footprint, fast setup.
- Handling of weekend peak spikes.
- Mobile/tablet POS.
- UPI.
- For food courts, compatibility with the mall's prepaid-card or centralised billing system, or replacing it.

**NICE-TO-HAVE**
- Accepting mall gift cards. Phoenix's is open-loop Visa, so the card terminal handles it.
- Hooks for mall loyalty programs. Lulu's is bill-upload based, so a QR or e-receipt helps.
- Tenant-side MG vs revenue-share rent calculator.

**Mall-provided hardware**
- I found no evidence that Indian malls commonly supply POS devices or sales-capture boxes to tenants. The dominant Indian pattern appears to be software ADSR. Kiosks generally bring their own POS (Fynd, Petpooja and similar).

### Gaps
- No primary tenant-lease or fit-out manual from Phoenix, DLF, Lulu, Nexus, Inorbit, Pacific or VR was found online, so exact clauses (data format, deadline time, audit rights, penalties) are unconfirmed.
- No evidence on whether DLF, Nexus, Lulu, Inorbit, Pacific or VR use Onintouch or another ADSR vendor. Only Phoenix is named, and only by the vendor.
- No source on approved-POS-vendor lists at Indian malls.
- No food-court revenue-share percentages or settlement cycles found.
- No full-text access to the Fynd kiosk pages, GoFrugal or VasyERP ADSR documentation (blocked in this session).

## 4. What store owners complain about with their current POS

### Takeaway
Recurring complaints:
- offline/sync failures, especially multi-counter stock desync;
- recurring subscription cost (₹500–3,000/month);
- poor vendor support and product knowledge;
- slow performance and delayed features;
- accounting/report errors (profit, ledger double-counting);
- awkward returns;
- GST slab handling that needs manual selection;
- free tiers that break at the second counter.

The evidence is anecdotal and vendor-blog heavy. I found no independent survey.

### Cited Findings
**Offline and cost**
- Cloud-only billing that dies "every time the broadband drops… becomes a liability". Billing must survive power cuts. — [TheHobbyist (2025)](https://www.thehobbyist.in/blog/free-billing-software-kirana-india.html)
- Feature-complete Indian billing software costs "₹500 to ₹3,000 per month" by subscription. — [TheHobbyist](https://www.thehobbyist.in/blog/free-billing-software-kirana-india.html)

**Local-install trade-offs**
- High risk of data loss on disk crash without manual backup.
- Manual, costly updates.
- Source: cloud vs local comparison. — [RetailDaddy](https://retaildaddy.in/blog/best-retail-billing-software-for-small-shops-and-kirana-stores)

**Multi-counter and free tiers**
- Free plans limiting users, or offline-only modes, "break the moment a second counter is added". — [Hedgethink](https://www.hedgethink.com/best-pos-billing-software-for-small-multi-counter-shops-in-india-real-cost-staff-training-test/)

**GST handling**
- Mixed tax slabs on kirana bills. Basic tools need manual slab selection. — [RetailDaddy / search summary](https://retaildaddy.in/blog/best-retail-billing-software-for-small-shops-and-kirana-stores)

**Vendor support and product quality**
- Support "unprofessional and unhelpful… lack of product knowledge" (review of a major Indian vendor). — [SoftwareSuggest supermarket software reviews](https://www.softwaresuggest.com/supermarket-software)
- Recurring complaints for another major Indian retail ERP: missing advanced features, syncing/offline issues, slower performance, delayed feature delivery. — [SoftwareSuggest/aggregator summary](https://www.softwaresuggest.com/point-of-sale-pos-software/india)

**App-level errors**
- Profit calculation wrong, returns hard, edited purchases double-counted, offline not working. — [Google Play review](https://play.google.com/store/apps/details?id=com.webkul.mobikul.pos&hl=en_IN)

**Owner pain that drives adoption in the first place**
- Handwriting errors, credit confusion, stock mismatch. — [Elixir Retail360](https://elixirretail360.com/blog/pos-system-kirana-shop-story/)

### Inferences
- The fastest route to adoption: rock-solid offline multi-counter billing, transparent pricing with no per-counter surprises, responsive local-language support, and correct accounting/GST reports out of the box.
- Data correctness (stock, profit, cash ledger) is a trust issue. A single double-count bug leads to churn.

### Gaps
- No systematic review mining (Play Store at scale, Reddit, YouTube) was possible in this session. The complaints above are a small, anecdotal sample.
- No RAI or trade-press (ET Retail, Images Retail, Indian Retailer) survey on POS dissatisfaction was found.
