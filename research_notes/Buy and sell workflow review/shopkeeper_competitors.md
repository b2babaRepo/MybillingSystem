# How Indian shopkeeper billing and khata apps make selling and buying fast (research as of Nov 2026)

Method note: 12 searches/fetches. Most evidence is vendor-published (official sites, marketplace listings that reuse vendor copy). Independent tap-count measurements were not found for any app. Treat speed claims as marketing.

## Q1. Billing and khata/udhaar speed: taps, scan, voice, favourites, reminders, UPI on bill

### Takeaway
The leaders compete on three moves: (a) record credit in a couple of taps with the customer needing no app, (b) push a WhatsApp/SMS reminder carrying a UPI pay link in one tap (bulk-send for many customers), and (c) share the bill on WhatsApp with UPI QR. Barcode POS billing is standard in the GST-billing tier (Vyapar, myBillBook, Swipe). No credible source confirmed voice entry or published tap counts.

### Cited Findings
- OkCredit: WhatsApp and SMS reminders go out automatically; a WhatsApp reminder with a payment link can be sent in one tap; reminder includes a UPI option (GPay, PhonePe, BHIM, Paytm) — [OkCredit reminder page](https://okcredit.in/payment-reminder-app)
- OkCredit: QR-based payment is auto-recorded to the customer's khata account — [OkCredit](https://okcredit.in/en/)
- OkCredit: customers do not need the app; they receive entries and reminders by SMS and pay via any UPI app — [OkCredit udhar khata](https://okcredit.in/udhar-khata-app)
- OkCredit free Basic plan: unlimited customers, entries, reminders, online backup; Premium adds ad-free, multi-device, GST billing; 11 languages — [OkCredit vs Khatabook](https://okcredit.in/khatabook-alternative)
- OkCredit also lists GST/non-GST billing, stock tracking with low-stock alerts, and loans up to Rs 1,00,000 via NBFC partners — [okcredit.in/llms.txt](https://okcredit.in/llms.txt)
- Khatabook: payment reminders by SMS/WhatsApp, multiple reminders with one tap; GST/non-GST invoices shared via WhatsApp — [Khatabook listing summary via G2](https://www.g2.com/products/khatabook/discuss); official feature blog is dated Oct 2020 (old) — [Khatabook blog](https://khatabook.com/blog/khatabook-app-features)
- Khatabook free tier described as ledger-only by a competitor (biased source) — [BillZap blog 2026](https://billzap.netlify.app/blog/best-free-gst-billing-app-india-2026/)
- Swipe: vendor claims invoices, POS bills and estimates in under 10 seconds; WhatsApp/email sharing; UPI QR or payment gateway collection with status tracking and reminders; POS billing with barcodes; GSTR-1 and 40+ reports — [Capterra Swipe](https://www.capterra.com/p/10008988/Swipe/)
- Swipe pricing: free version; PRO Rs 2,999/user/yr, JET Rs 4,199, RISE Rs 5,499, RISE+E-Invoice Rs 7,499 — [Capterra Swipe](https://www.capterra.com/p/10008988/Swipe/)
- myBillBook: barcode scanning for faster POS checkout (vendor page) — [myBillBook](https://mybillbook.in/s/?p=29974); price on its trial page about Rs 217/month billed annually excl. GST, third-party listings conflict (Rs 142 to Rs 199) — [myBillBook trial](https://freetrial.mybillbook.in/generate-bills-online-v2.html), [SaaSworthy](https://www.saasworthy.com/product/mybillbook)
- Vyapar: barcode printing, batch/serial, low-stock alerts; offline mode — [Capterra compare](https://www.capterra.in/compare/202732/180579/flobooks/vs/vyapar), [Techjockey](https://www.techjockey.com/detail/vyapar)
- Vyapar pricing (approx., sources conflict): free mobile tier; Desktop Silver ~Rs 3,399/yr, Gold ~Rs 3,699/yr; Mobile+Desktop Silver Rs 3,999, Gold Rs 4,299; Gold adds e-invoice/e-way bill, WhatsApp integration, multi-firm — [Techjockey](https://www.techjockey.com/question/12078/what-are-the-pricing-plans-for-vyapar), [FitGap](https://us.fitgap.com/products/011408/vyapar); 2026 USD-priced Gold/Platinum structure and a reported 10% price cut (June 2026, unverified) — [PricingSaaS](https://pricingsaas.com/companies/vyapar)
- PhonePe Business: merchant can enable Khata for a non-PhonePe customer, who then gets entries and reminders by SMS (payments recorded manually); PhonePe users accept a khata invite — [PhonePe help](https://cms.phonepe.com/en/myhelp/local-stores-and-shops/khata/can-a-merchant-enable-khata-for-me-if-im-not-a-phonepe-user), [PhonePe help](https://cms.phonepe.com/en/myhelp/local-stores-and-shops/khata/how-do-i-enable-khata-phonepe)
- PhonePe SmartSpeaker gives real-time voice payment alerts (monthly service fee) — [PhonePe business blog](https://www.phonepe.com/blog/merchant-solution/why-more-merchants-are-choosing-phonepe-a-smarter-way-to-do-business/)

### Inferences
- Patterns worth copying: customer-side "no app needed" SMS/WhatsApp ledger; one-tap reminder with embedded UPI link; bulk reminders; auto-posting UPI receipts to the right khata (needs a per-customer QR or link reference); WhatsApp bill share as the default end of checkout; audible payment confirmation (speaker or in-app TTS) so the shopkeeper does not look at the phone.
- Market split: ledger apps (OkCredit, Khatabook, PhonePe Khata) free; GST billing/inventory (Vyapar, myBillBook, Swipe) roughly Rs 2,500 to Rs 5,500 per year.

### Gaps
- No measured tap counts for any app; no confirmation of voice entry in Khatabook/OkCredit; quick-add favourites and recent-items UI not documented in sources found. Marg ERP/Marg Books, Zoho Invoice/Inventory, Dukaan billing, BharatPe and Paytm Business were not covered by results (search budget).

## Q2. Purchase side: supplier bill OCR, reorder, low-stock, distributor ordering

### Takeaway
myBillBook is the clearest example of purchase entry from a supplier bill photo (OCR with review-before-save). Low-stock alerts are common (Vyapar, OkCredit). Distributor apps (HUL Shikhar, Jumbotail, Udaan) carry the actual reordering, with Jumbotail claiming no minimum order, next-day delivery and pay-on-delivery credit.

### Cited Findings
- myBillBook "Scan and Record Bills": photo or file of a supplier bill; OCR fills invoice number, party, items; user reviews and edits before saving; mobile app only — [myBillBook feature](https://mybillbook.in/s/features/scan-and-record-bills/)
- HUL Shikhar: retailers order HUL products 24/7, see trade promotions and catchment best-sellers; generates over a third of HUL sales (Dec 2024) — [Business Standard](https://www.business-standard.com/companies/news/hul-kirana-distribution-samadhan-strategy-124123100260_1.html), [Unilever 2023](https://www.unilever.com/news/news-search/2023/the-inhouse-developed-app-thats-transforming-a-traditional-sales-model/)
- Shikhar suffered high dormancy; many used it only for monthly stock orders; retailers preferred face-to-face ordering — [WARC](https://www.warc.com/content/article/emvies/hul-shikhar-app-ai-prescribes-how-to-conquer-shikhar/en-gb/157382)
- Jumbotail: near 100% of sales from unassisted app orders by kiranas (2024) — [YourStory](https://yourstory.com/2024/03/jumbotail-marketplace-kirana-retail-artal-asia-funding); ~75% next-day delivery FY23, no minimum order value, pay at delivery using lender working-capital credit (secondary source) — [valueforstartups](https://valueforstartups.in/jumbotail); 50,000+ kiranas — [Inc42](https://inc42.com/buzz/jumbotail-raises-inr-35-cr-debt-round-from-alteria-capital/)
- ElasticRun narrowed FY25 loss 60% to Rs 145 Cr (Dec 2025) — [Entrackr/Inc42 via search](https://entrackr.com/?p=165655)

### Inferences
- Copy: photo-of-supplier-bill to draft purchase entry with editable review; low-stock list that becomes a one-tap supplier order (WhatsApp to supplier). Shikhar's dormancy suggests reorder must fit the existing weekly/monthly salesman rhythm, not replace it.

### Gaps
- No source found on auto-reorder suggestions in any billing app; Udaan and ElasticRun retailer app flows not documented; OCR plan tier for myBillBook unknown.

## Q3. Customer order handling (WhatsApp, ONDC, JioMart, Dukaan) and offline/pricing

### Takeaway
ONDC lets a seller registered through one seller app appear on many buyer apps (Paytm, PhonePe Pincode, etc.); order screens live in the seller app. JioMart's kirana-WhatsApp model (2020, old) routed orders to the nearest partner kirana and notified customers when the order was billed/ready. Step-level accept/pack/deliver flows were not found.

### Cited Findings
- ONDC sellers register via a seller network participant app and become discoverable across buyer apps without separate integrations — [Fretron guide 2026](https://www.fretron.com/guides/sell-on-ondc/), [Shiprocket](https://shiprocket.in/blog/top-ondc-apps-in-india)
- Kiko Live (kirana seller NP) crossed half a million ONDC orders for kiranas — [CXOToday](https://cxotoday.com/media-coverage/kiko-live-crosses-half-million-milestone-orders-for-kirana-stores-on-ondc/)
- PhonePe Pincode exited non-food ONDC categories and looked to groceries (older report) — [Medial](https://medial.app/news/phonepes-pincode-drops-electronics-and-fashion-looks-beyond-ondc-3fe08ede6fbaf); The Ken noted buyer apps lacked sellers — [The Ken](https://the-ken.com/story/sellers-wanted-say-phonepe-and-paytm-after-bringing-buyers-to-ondc/)
- JioMart (2020): WhatsApp orders sent to nearest partner kirana; customer messaged when store has billed/ready; initially pickup only, no online payment — [Business Insider 2020](https://www.businessinsider.in/business/ecommerce/news/jiomart-is-neither-delivering-orders-to-your-doorstep-nor-cancelling-orders/amp_articleshow/75408052.cms), [Business Standard 2020](https://business-standard.com/article/companies/reliance-begins-using-whatsapp-for-grocery-deliveries-through-jiomart-120042700691_1.html)
- Offline: Vyapar lists offline mode; competitor BillZap markets itself as offline-first; no independent offline test found — [Techjockey](https://www.techjockey.com/detail/vyapar), [BillZap](https://billzap.netlify.app/blog/best-free-gst-billing-app-india-2026/)

### Inferences
- Copy: customer status messages at "billed/ready" and "out for delivery" over WhatsApp; a single order inbox regardless of channel; pickup as a first-class option.

### Gaps
- No documented auto-accept or tap-by-tap accept/pack/deliver flows for Dukaan, ONDC seller apps, or JioMart partner app; Dukaan not covered; Play Store review analysis not done.
