# Billing accuracy, GST and controls for an Indian clothing shop (as of Nov 2026)

## GST on apparel in 2026: rate slabs, per-piece test, mixed bills, HSN digits

### Takeaway
Since 22 Sep 2025 (GST 2.0, 56th Council meeting), apparel and clothing accessories are taxed at 5% when the sale value per piece is up to Rs 2,500 and 18% above that. The old rule was 5% up to Rs 1,000 and 12% above. The test applies to each piece's pre-GST transaction value after any discount shown on the invoice, so one bill can carry both rates.

### Cited Findings
- Threshold raised from Rs 1,000 to Rs 2,500 per piece. Pieces above Rs 2,500 moved from 12% to 18%. Effective 22 Sep 2025 — [Upstox](https://upstox.com/news/business-news/tax/gst-rejig-garments-priced-above-2-5-k-to-be-costlier-will-hurt-middle-class-affordability-say-industry-bodies/article-180738); [Business Today](https://www.businesstoday.in/latest/economy/story/gst-council-to-slash-tax-on-footwear-apparel-priced-under-rs-2500-to-5-492398-2025-09-03); [Free Press Journal](https://www.freepressjournal.in/amp/business/garments-above-2500-now-attract-18-gst-industry-warns-of-higher-prices-and-middle-class-impact)
- Cited as Notification 9/2025-Central Tax (Rate), dated 17 Sep 2025 — [Accountune](https://accountune.com/gst-on-garments-above-2500) (a vendor blog; I did not check the notification text)
- Footwear follows the same pattern: 5% up to Rs 2,500 per pair, 18% above (2.5+2.5 or 9+9 CGST/SGST) — [TheMunim](https://themunim.com/gst-on-clothes-apparel/)
- The test is on each piece's value before GST, not on the bill total. A Rs 2,400 item stays at 5% even though the customer pays Rs 2,520 — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- If the tag price includes tax, the app must back out GST before comparing with Rs 2,500. Example: a Rs 2,600 tag including 5% GST is about Rs 2,476 before tax, so 5% applies — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- A discount shown on the invoice at the time of sale lowers the taxable value, and the slab follows the lower value. Rs 3,000 minus Rs 500 = Rs 2,500, so 5% applies. A Rs 499 discount leaves it above Rs 2,500, so 18% applies — [Taxo.online](https://taxo.online/latest-articles/understanding-the-₹2500-slab-under-gst-2-0/); [Accountune](https://accountune.com/gst-on-garments-above-2500)
- Mixed bills are correct with two rates on one invoice. In a worked example, a Rs 9,600 bill should carry Rs 1,104 GST. Charging 18% on the whole bill would give Rs 1,728, an overcharge of Rs 624 — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- One page (Busy.in) claims a uniform 5% on most garments regardless of price. It contradicts the sources above and is internally inconsistent, so treat it as wrong — [Busy.in](https://busy.in/gst-rates/readymade-clothes/)
- HSN digits under Notification 78/2020-CT, effective 1 Apr 2021 — [GST Council PDF](https://www.gstcouncil.gov.in/sites/default/files/2024-05/notfctn-78-central-tax-english-2020.pdf); [IRIS](https://irisgst.com/for-taxpayer-with-turnover-upto-rs-5-cr-mention-4-digit-hsn-code-and-for-turnover-above-rs-5-cr-mention-6-digit-hsn-code/); [TaxHeal](https://www.taxheal.com/?p=97083):
  - Previous-year turnover up to Rs 5 crore: 4 digits on B2B invoices, optional on B2C invoices.
  - Turnover above Rs 5 crore: 6 digits on all invoices.
- Chapter 61 covers knitted apparel and Chapter 62 covers woven apparel. Which one applies depends on the fabric — [Accountune](https://accountune.com/gst-on-garments-above-2500)

### Inferences
- The app should work out the rate per line, using the unit price after line discount and after allocating any bill-level discount, minus GST if the price is tax-inclusive. It should never set the rate at bill level.
- A per-piece unit is needed. Pieces sold as a set or pair need a rule for whether they count as one piece; ask a CA.
- The HSN summary on the bill, and in GSTR-1, should be grouped by HSN and rate.

### Gaps
- I did not read the text of Notification 9/2025-CT(R). The rate, entry wording and the "sale value" definition come from secondary sources.
- Not confirmed whether a bill-level discount, spread across lines, may legally move a piece into a lower slab. The sources only discuss discounts shown on the invoice in general terms.
- Treatment of post-sale discounts under Section 15(3)(b) is not covered here.
- Fabrics (5%) and made-ups were not checked.

## MRP rules (Legal Metrology)

### Takeaway
MRP must be declared and must include all taxes. Selling above MRP is penalised, with Section 36 of the Legal Metrology Act 2009 commonly invoked. Since 1 Jan 2023, garments and hosiery sold loose need fewer declarations (four, including MRP and size).

### Cited Findings
- Rule 6(1)(e) of the Packaged Commodities Rules 2011 requires MRP to be declared. Under Rule 2(m), MRP includes all taxes — [TaxGuru (via search)](https://taxguru.in/corporate-law/centre-amends-legal-metrology-packaged-commodities-rules-2011.html)
- Garments and hosiery sold loose or open are exempt from the full set of declarations. They need only about four (manufacturer address, MRP, size, country of origin), effective 1 Jan 2023 — [Lexplosion](https://lexplosion.in/garments-hosiery-products-sold-in-loose-or-open-form-are-exempted-from-compliances-under-legal-metrology-packaged-commodities-rules-subject-to-declaring-manufacturer-address-mrp-size-country-of/)
- A 2016 advisory: garment size should be stated in cm or m, with S/M/L as an extra declaration — [Mondaq (via search)](https://mondaq.com/india/consumer-protection/1467222/legal-update-on-legal-metrology-packaged-commodities-third-amendment-rules-2022)
- Selling above MRP is punishable under Section 36, with fines that rise for repeat offences — [Lawx S.36](https://lawx.in/acts/section/47/480/4622); [CAG](https://www.cag.org.in/blogs/maximum-retail-price-mrp-and-over-charging)
- **May have changed:** one source says the Section 36 penalties were replaced from 1 May 2026, with a first breach now drawing an improvement notice. This is not corroborated by any government source — [righttoinformation.wiki](https://righttoinformation.wiki/legal-metrology-2026-amendment-improvement-notice-ecommerce-penalty)
- Where the retailer has not tampered with the declaration, the default is the manufacturer's or packer's — [search summary, IPleaders](https://blog.ipleaders.in/legal-metrology-packaged-commodities-rules-2011/)

### Inferences
- The app should block any selling price above MRP (hard stop), and show MRP, discount and net price on the bill.
- Because MRP includes tax, GST is inside the price. The taxable value is net ÷ (1 + rate), which feeds back into the Rs 2,500 slab test.
- When the GST cut lowered prices, shops may have restickered MRPs. The app should keep a history of MRP changes per item.

### Gaps
- I did not retrieve the text of Rule 18(2), the retailer's bar on selling above MRP. It is cited from general knowledge only.
- I found no rule that requires discounts to be displayed. Showing them is treated as good practice and consumer-law hygiene.

## Discounts and buy-X-get-Y schemes

### Takeaway
A discount shown on the invoice at the time of sale reduces each piece's taxable value, so it can move a piece from 18% to 5%. For buy-X-get-Y, the usual position is that the free item is part of one priced supply. Sources disagree on how to apportion it.

### Cited Findings
- A discount on the invoice reduces the taxable value, and the slab follows the reduced value — [Accountune](https://accountune.com/gst-on-garments-above-2500); [Taxo.online](https://taxo.online/latest-articles/understanding-the-₹2500-slab-under-gst-2-0/)
- On buy-one-get-one, sources conflict:
  - GST only on the paid item's transaction value — [TaxTMI](https://www.taxtmi.com/article/detailed?id=8419)
  - No GST on the free item and no ITC — [Bajaj Finserv](https://bajajfinserv.in/gst-on-clothes)
  - Treat as a composite or mixed supply, with ITC available — [Bilimoria Mehta](https://www.bilimoriamehta.in/blogs/valuation-under-gst)

### Inferences
- A safe app design: record the scheme on the invoice as a discount, spread the total price across all pieces including the "free" one (proportional to MRP), and set each piece's rate on its spread value. This leaves no zero-value lines and keeps the per-piece test defensible. It is the approach generally associated with CBIC Circular 92/11/2019, but this is unverified (see Gaps).
- Spreading the discount can push pieces under Rs 2,500 and so lower the tax. Expect scrutiny, and keep the scheme rules recorded.

### Gaps
- I did not retrieve CBIC Circular 92/11/2019-GST (on promotional schemes such as buy-one-get-one and discounts). Confirm its text.
- No authoritative ruling found on spreading a bill-level discount across slabs.

## Returns, exchanges and credit notes

### Takeaway
Returns are handled with a Section 34 credit note at the original invoice's rate and HSN. It must be reported by 30 November after the financial year of the sale, or by the annual-return date if earlier. An exchange is a credit note plus a fresh invoice at the new item's rate.

### Cited Findings
- Report the credit note by 30 Nov following the financial year of the supply, or the annual-return date, whichever is earlier. For FY 2025-26 sales, that is 30 Nov 2026 — [TaxGuru](https://taxguru.in/goods-and-service-tax/section-34-understanding-credit-notes-gst.html); [Quicko](https://learn.quicko.com/what-is-a-credit-note-under-gst/)
- After the deadline, only a commercial credit note can be issued. It does not reduce output tax — [Tally](https://tallysolutions.com/gst/how-to-handle-gst-on-goods-returned-after-6-months/)
- A credit note carries the original invoice's rate and HSN. An exchange across the threshold needs a credit note at the old rate plus a new invoice at the new item's rate — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- Credit and debit note format is under Rule 53 — [CalcGuru](https://calcguru.in/?p=9727)

### Inferences
- Every return must link to the original invoice line, so the app has the rate, value after discount and HSN.
- Goods bought before 22 Sep 2025 at 12% should be credited at 12%, not today's rate.
- Refund or store credit is shop policy. Store credit is a liability (an advance or voucher) and should be tracked with an expiry.
- Print the exchange policy (days, condition, tags) on the bill.

### Gaps
- The GST treatment of store-credit vouchers (single- vs multi-purpose vouchers, after the 2024-25 voucher amendments) was not researched.
- No statutory consumer return period was found. It is policy-driven.

## Controls against mistakes and staff fraud

### Takeaway
I found no statutory source. These are standard retail POS controls, inferred from the rules above.

### Cited Findings
- Charging 18% across a whole bill is a common error. It shows why the rate must be computed per line automatically — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- A shop may struggle to find the original invoice rate weeks later, which is an argument for retrievable, linked records — [Accountune](https://accountune.com/gst-on-garments-above-2500)

### Inferences
- **Barcode-only selling:** scan each piece (SKU = style + size + colour) so MRP, HSN and rate come from the master.
- **Price lock:** block selling above MRP. Changing a price downward needs a reason and, above a limit, a manager PIN.
- **Discount limits:** set a maximum percentage or rupee amount per staff role. Flag discounts that drop a piece just under Rs 2,500, which is a slab-gaming signal.
- **Void and cancel audit:** keep invoice numbers sequential and never delete. Cancelled bills stay with user, time and reason. Under Rule 46, the series is unique per financial year.
- **Day-end Z report:** expected versus counted cash, UPI and card. Track variance per cashier.
- **Salesperson tag on each line:** for commission and for reviewing returns by salesperson.
- **Returns:** only against the original bill number, with the condition checked.

### Gaps
- No Indian regulatory or industry source was retrieved for POS fraud controls.

## What a clear customer bill shows

### Takeaway
A clear bill combines the GST tax-invoice fields (Rule 46) with the MRP, discount and net price for each line, the GST split by rate, and the exchange policy.

### Cited Findings
- Two rates on one invoice is correct. The bill should show the tax per rate — [Accountune](https://accountune.com/gst-on-garments-above-2500)
- HSN digits depend on turnover; HSN is optional on B2C bills up to Rs 5 crore turnover — [IRIS](https://irisgst.com/for-taxpayer-with-turnover-upto-rs-5-cr-mention-4-digit-hsn-code-and-for-turnover-above-rs-5-cr-mention-6-digit-hsn-code/)
- MRP includes all taxes, and size is a declaration — [Lexplosion](https://lexplosion.in/garments-hosiery-products-sold-in-loose-or-open-form-are-exempted-from-compliances-under-legal-metrology-packaged-commodities-rules-subject-to-declaring-manufacturer-address-mrp-size-country-of/)

### Inferences
- Suggested layout:
  - **Header:** shop name, address, GSTIN, invoice number and date, place of supply.
  - **Each line:** item, size, colour, HSN, quantity, MRP, discount, net, taxable value, GST %.
  - **Footer:** a rate-wise table (taxable value, CGST, SGST), total savings, payment split, salesperson, the exchange policy (for example "exchange within 7 days with tag and bill"), and a QR code or bill number for returns.
- Do not print the MRP-inclusive total and then add GST again; that double-counts tax.

### Gaps
- The Rule 46 text was not fetched. The field list is from general knowledge.
- Thresholds for B2C dynamic QR codes and e-invoicing were not covered.
