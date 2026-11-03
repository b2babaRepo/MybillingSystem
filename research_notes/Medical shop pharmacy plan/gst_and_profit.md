# GST, billing and P&L accounting for Indian retail medical shops (as of Nov 2026)

## GST rates on pharmacy goods after GST 2.0 (effective 22 Sep 2025), HSN codes and HSN digits on invoices

### Takeaway
From 22 Sep 2025, most medicines (HSN 3003/3004) are 5% (down from 12%). A notified list of life-saving drugs is nil-rated: 33 moved from 12% and 3 from 5%. Devices, diagnostic kits, glucometers and strips, thermometers, spectacles, gloves, baby diapers and feeding bottles, and most toiletries are also 5%. The app needs a GST rate stored per item (not assumed from HSN alone), with nil, 5% and 18% all supported.

### Cited Findings
- 56th GST Council: 33 life-saving drugs go from 12% to nil, and 3 drugs for cancer, rare and chronic diseases go from 5% to nil. All other drugs and medicines go from 12% to 5% — [TaxGuru](https://taxguru.in/goods-and-service-tax/gst-2-0-impact-pharmaceutical-sector.html)
- These items also move to 5%: diagnostic kits and reagents, surgical and examination gloves, glucometers and test strips, spectacles, many medical instruments, and X-ray apparatus (from 12%, or from 18% for some). Thermometers go from 18% to 5%. Medical oxygen, medicinal hydrogen peroxide, iodine and anaesthetics go from 12% to 5% — [TaxGuru](https://taxguru.in/goods-and-service-tax/gst-2-0-impact-pharmaceutical-sector.html)
- Legal basis cited as Notification 10/2025-Central Tax (Rate), dated 17 Sep 2025. Sources disagree on the count of exempt drugs (33, 36 or 37) — [Busy](https://busy.in/gst-rates/medicines/); [Accountune](https://accountune.com/medicine-gst-rate-list)
- "HSN 3004 = 5%" is not a blanket rule. Specific drugs are nil-rated, and nicotine polacrilex gum is reportedly 18% — [Busy](https://busy.in/gst-rates/medicines/); [Tally](https://tallysolutions.com/gst/hsn-code-3004-product-classification-gst-rate-business-filing-guide/)
- HSN 9018 (syringes, needles, catheters, cannulae and other medical instruments) is 5% — [Busy](https://busy.in/gst-rates/medicines/)
- Talcum powder, hair oil, shampoo, toothpaste, shaving products, toothbrushes and toilet soap go from 18% to 5%. Mouthwash was not cut — [MS Associates](https://www.msassociates.pro/gst/gst-personal-care-and-hygiene.php); [The Week](https://www.theweek.in/news/biz-tech/2025/09/04/gst-2-0-toothpaste-shampoos-shaving-creams-to-become-cheaper-but-not-mouthwash-like-listerine-and-colgate-plax-why.amp.html)
- Baby feeding bottles and nipples, and baby and clinical diapers, go from 12% to 5% — [MS Associates](https://www.msassociates.pro/gst/gst-personal-care-and-hygiene.php)
- HSN digits (Notification 78/2020-CT, in force from 1 Apr 2021): up to Rs 5 cr AATO, 4 digits are mandatory on B2B invoices and optional on B2C. Above Rs 5 cr, 6 digits on all invoices — [GST Council PDF](https://www.gstcouncil.gov.in/sites/default/files/2024-05/notfctn-78-central-tax-english-2020.pdf); [IRIS](https://irisgst.com/for-taxpayer-with-turnover-upto-rs-5-cr-mention-4-digit-hsn-code-and-for-turnover-above-rs-5-cr-mention-6-digit-hsn-code/)
- E-invoices and e-way bills with AATO above Rs 5 cr need at least 6-digit HSN codes. The e-invoice portal is reported to reject 4-digit codes — [Tungsten](https://us.tungsten-network.com/compliance-overview/country-updates/country/india/e-invoice-system-will-no-longer-accept-4-digit-hsn-codes)

### Inferences
- Store at least 6-digit, ideally 8-digit, HSN per item (for example 30049011) so the shop is ready if it crosses Rs 5 cr. Print 4 digits on B2B invoices when below Rs 5 cr.
- Seed rates: 3003/3004 at 5% by default, with a nil flag for notified drugs; 3005 (dressings) and 3006 at 5% (CA to confirm); 9018/9025/3822 at 5%; personal care at 5%.

### Gaps
- No reliable post-GST 2.0 rate found for cosmetics (creams, perfumes, make-up) or nutraceuticals and health supplements (HSN 2106, food supplements). Likely 18% (cosmetics, 3304) and 5% or 18% (2106), but unverified. **CA must confirm.**
- The authoritative list of nil-rated drugs needs to come from the schedule in Notif 10/2025 or 1/2025-CT(Rate). Exact rates for 3005 and 3006 were not confirmed in a primary source.

## MRP-inclusive pricing, old-stock rate change, free goods, and the composition scheme

### Takeaway
Taxable value = MRP-inclusive selling price ÷ (1 + rate). The NPPA required the rate-cut benefit to be passed on from 22 Sep 2025 even on old-MRP stock. Free quantity in "buy X get Y" schemes is not a separate supply, and ITC is not blocked. Composition (1% for traders, Rs 1.5 cr limit, no ITC) is legally possible but usually unattractive for chemists.

### Cited Findings
- The NPPA made it mandatory to pass on the GST cut to consumers from 22 Sep 2025. Re-stickering old stock is not mandatory if price compliance is ensured at the retail level — [ASL India](https://aslindia.net/newsletters/gst-reduced-delhi-drug-control-dept-directs-manufacturers-to-revise-mrps-by-sept-22); [A2Z Taxcorp](https://a2ztaxcorp.net/gst-rate-cut-on-medicines-from-sept-22-no-need-for-re-labelling-prices-to-be-updated-via-revised-lists/)
- Under DoCA's 9 Sep 2025 permission, a revised MRP may be stickered on unsold stock until 31 Dec 2025, with the old MRP kept visible. DPCO medicines are exempt from the LMPC rules under Rule 26(c), but nutraceuticals are not — [TaxGuru](https://taxguru.in/goods-and-service-tax/gst-2-0-impact-pharmaceutical-sector.html)
- For medicines that became nil-rated, ITC on stock held on 21 Sep 2025 must be reversed under s.18(4) read with Rule 44 — [TaxGuru](https://taxguru.in/goods-and-service-tax/gst-2-0-impact-pharmaceutical-sector.html)
- Chemist bodies asked for a 3-to-4-month transition period and ITC relief on old stock bought at 12%. No grant of this was found — [Medical Dialogues](https://medicaldialogues.in/news/industry/pharma/chemist-body-seeks-transition-period-gst-input-adjustment-after-tax-cut-writes-to-cm-154922); [Navhind Times](https://www.navhindtimes.in/2025/09/24/goanews/chemists-seek-120-day-period-to-transition-into-new-gst-rates/)
- Circular 92/11/2019-GST: free quantity supplied with goods (common in pharma) is not a free supply, because a single price covers all the goods. ITC is not blocked under s.17(5)(h). Free samples are different, and ITC on them is blocked — [CBIC circular 92](https://gstcouncil.gov.in/sites/default/files/2024-06/circular-cgst-92.pdf); [TaxGuru](https://taxguru.in/goods-and-service-tax/sales-promotion-schemes-taxability-itc.html)
- Composition scheme: Rs 1.5 cr turnover limit (Rs 75 lakh in special-category states), 1% for traders, no ITC, no collecting tax from customers — [Zoho](https://www.zoho.com/in/books/gst/faq/composition-scheme.html); [Masters India](https://www.mastersindia.co/books/indian-gst-a-comprehensive-guide/composition-scheme/)

### Inferences
- Formula: taxable = round(sale_price × 100 / (100 + rate), 2); CGST = SGST = (sale_price − taxable)/2 for intra-state sales. Apply the customer discount on MRP before back-calculating. Example: MRP 112, old 12% stock now sold at revised MRP 105 at 5% gives taxable 100.00 and GST 5.00.
- Rate is decided at the time of supply (sale date), not the purchase rate. The app must allow a "revised MRP" field per batch that overrides the printed MRP.
- Schemes: a purchase of 10+1 at PTR 100 means effective cost = 1000/11 = 90.91 per unit. Book the free unit into stock at zero cost and spread the cost across the batch. Full ITC is available on the invoice tax.
- Composition maths: on 5% goods, a regular dealer at a 20% margin pays about 5% × 20% ≈ 1% of sales net of ITC, roughly the same as composition's 1% but without the ITC loss. Composition also bars B2B tax invoices (hospitals and clinics can't claim ITC) and inter-state purchases are still allowed. It is attractive mainly for very small shops. **CA to confirm**, including whether exempt (nil) drug turnover counts toward the limit.

### Gaps
- No CBIC circular found on retailer ITC refund or adjustment for old 12% stock. In a 12%→5% case, ITC stays available, so excess credit just accumulates; refund under the inverted-duty rule is unclear for traders. **CA to confirm.**

## GSTR-1 / 3B / CMP-08 and e-invoicing for chemists

### Takeaway
Regular chemists file GSTR-1 (monthly, or quarterly under QRMP up to Rs 5 cr) and GSTR-3B. Composition chemists file CMP-08 quarterly and GSTR-4 annually. B2C sales are reported rate-wise and state-wise in aggregate. B2B sales to hospitals and clinics with a GSTIN are reported invoice-wise. E-invoicing applies only above Rs 5 cr AATO.

### Cited Findings
- E-invoicing threshold reduced to Rs 5 cr from 1 Aug 2023 — [Taxscan](https://www.taxscan.in/gst-update-e-invoicing-and-e-waybill-hsn-code-requirement-changes-to-be-effective-from-october-1-2023/318039/embed/)
- Composition taxpayers lose ITC and must file ITC-01 within 30 days when leaving the scheme — [Zoho](https://www.zoho.com/in/books/academy/taxes-and-compliance/composition-scheme.html)

### Inferences
- The app should export a GSTR-1 JSON or Excel with these tables: B2B (invoice-wise), B2CS (aggregated by rate and place of supply), CDNR (credit notes for expiry or returns to registered buyers), HSN summary (Table 12, split B2B/B2C since 2025), document summary (Table 13), and exempt/nil (Table 8, for nil-rated drugs).
- Composition shops print a "Bill of Supply" with the declaration "composition taxable person, not eligible to collect tax on supplies".

### Gaps
- GSTR-1/3B due dates, QRMP details, and the Table 12 B2B/B2C split were not verified in this session from primary sources (from memory: GSTR-1 on the 11th, IFF/QRMP on the 13th, 3B on the 20th/22nd/24th; CMP-08 on the 18th after the quarter; GSTR-4 on 30 April). **Verify.**

## Profit calculation: PTR vs MRP margin, schemes, expiry, discounts, P&L heads

### Takeaway
Retail margin is typically about 16–20% of MRP on branded drugs (higher on generics). Profit must be computed on GST-exclusive values: net sale (after discount) minus effective landed cost (PTR after scheme, excluding GST because ITC is claimed). Online-style discounts of 10–20% can wipe out most of the margin on branded drugs.

### Cited Findings
- AIOCD seeks a 20% retailer margin on MRP and 10% for wholesalers on PTR (16% on scheduled drugs). NPPA guidance is reported at about 16% — [Free Press Journal](https://www.freepressjournal.in/amp/indore/indore-reframe-trade-margin-says-all-india-organisation-of-chemists-and-druggists); [Biospectrum](https://www.biospectrumindia.com/public/news/73/25168/can-phased-trade-margin-rationalisation-boost-pharma-growth.html)
- In Oct 2026, the government capped trade margins at 30% of MRP for non-scheduled anti-cancer drugs — [Medical Dialogues](https://medicaldialogues.in/news/industry/industry-perspective/aiocd-welcomes-30-trade-margin-cap-on-cancer-drugs-backs-rational-mrp-fixation-180898)
- AIOCD complains that corporate and online players discount "beyond margin limit". There was a nationwide chemist strike in May 2026 over e-pharmacy notifications GSR 817 and GSR 220 — [ETV Bharat](https://www.etvbharat.com/en/health/pharmacies-across-india-shut-may-20-epharmacy-concerns-enn26051305103); [Outlook](https://www.outlookindia.com/amp/story/healthcare-spotlight/chemists-intensify-protest-against-e-pharmacies-call-for-nationwide-bandh-on-may-20)
- Manufacturers advised returning near-expiry and expired stock — [TaxGuru](https://taxguru.in/goods-and-service-tax/gst-2-0-impact-pharmaceutical-sector.html)

### Inferences
- Worked example at 5% GST: MRP 100, PTR = MRP×0.80/1.05 = 76.19 (ex-GST basis). Sale at 10% off = 90 incl. GST, which is 85.71 taxable. Gross profit = 85.71 − 76.19 = 9.52 (11.1% of net sales). With a scheme of 10+1, cost is 69.26 and profit is 16.45. At 20% off, profit is 0. The app should warn when the discount exceeds the margin.
- Expiry: track batch and expiry. Expired goods returned to the distributor go against a credit note (the distributor issues it, and GST may need reversing). Goods destroyed or written off are a P&L loss, and ITC on them must be reversed under s.17(5)(h). **CA to confirm.**
- P&L heads: Sales (net of returns and discount); Purchases; Opening/closing stock (at cost, batch-wise); Expiry/breakage write-off; Scheme/discount received; Rent; Registered pharmacist salary (a legal requirement under Pharmacy Act / D&C Rules); Staff wages; Electricity (incl. refrigeration for cold-chain items); Drug licence fees (Forms 20/21, renewal), shop-establishment and GST compliance/CA fees; Software; Bank/UPI/card MDR charges; Delivery; Depreciation on fridge, AC and furniture.

### Gaps
- No primary source found on typical rents or salaries, or on margins for generics (often quoted at 30–60%). No pharmacy-specific P&L benchmark found.

## Income tax: presumptive 44AD and books

### Takeaway
A proprietor or firm chemist can use s.44AD: deemed profit is 6% of digital receipts and 8% of cash receipts. The turnover limit is Rs 3 cr if cash receipts are 5% or less of the total, otherwise Rs 2 cr. Under 44AD, no detailed books or audit are needed. Pharmacies with heavy cash sales will often hit the Rs 2 cr limit.

### Cited Findings
- 44AD: Rs 3 cr limit if at least 95% of receipts are digital, otherwise Rs 2 cr. Deemed profit is 6% (digital) or 8% (cash). Available to resident individuals, HUFs and firms, but not LLPs — [ClearTax](https://cleartax.in/s/section-44ad-presumptive-scheme); [Bhatt & Joshi](https://bhattandjoshiassociates.com/presumptive-taxation-scheme-for-business-section-44ad-of-income-tax-act/)
- Income-tax Act, 2025 consolidates the presumptive schemes into Section 58 (for later years) — [TaxAJ](https://www.taxaj.com/learn/?p=516)

### Inferences
- The app should report a digital vs cash receipts split (UPI, card, bank vs cash) for the year, which drives the 6%/8% calculation and the limit test.
- Outside 44AD, s.44AA books plus a tax audit (s.44AB) apply above the thresholds. Retail margins of 10–15% net are usually above 6–8%, so 44AD is often beneficial.

### Gaps
- The 2025 Act's s.58 effective date and exact thresholds, the 44AB audit limits, and the 5-year lock-in rule under 44AD(4) were not verified here. **CA to confirm.**

## Tally / accountant export expectations

### Takeaway
No pharmacy-specific source was found in this session. Accountants typically expect day-book exports of sales and purchase vouchers with GST ledgers split by rate, plus an HSN summary and a stock summary.

### Cited Findings
- Tally publishes HSN 3004 classification and filing guides, which shows that Tally users classify by HSN and rate — [Tally](https://tallysolutions.com/gst/hsn-code-3004-product-classification-gst-rate-business-filing-guide/)

### Inferences
- Export: sales vouchers (invoice no., date, party/GSTIN, taxable by rate, CGST/SGST/IGST, round-off, payment mode); purchase vouchers with distributor GSTIN; credit/debit notes (expiry returns); ledgers such as "Sales @5%", "Sales Exempt", "Output CGST 2.5%" and so on; Tally XML or Excel import format; GSTR-1-ready Excel.

### Gaps
- Tally XML schema specifics and accountant preferences were not researched. See the existing report "reports/Tally export for paid shops.md" in this repo.
