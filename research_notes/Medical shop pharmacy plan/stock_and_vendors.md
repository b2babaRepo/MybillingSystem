# Indian retail medical shop stock and supplier workflows (2026)

Note: 10 web searches; most sources are vendor blogs or secondary sites. Official sources (Parliament answers, CBIC circular, PIB) are flagged where used. No pages were fetched in full, so all citations are based on search snippets.

## Batch and expiry tracking, FEFO, near-expiry alerts, expiry returns

### Takeaway
Every stock line is tracked by batch, with its own expiry, MRP and cost. Billing should pick the earliest-expiring batch first (FEFO). Expired stock goes back to the distributor either as a fresh supply or against a credit note (CBIC Circular 72/46/2018). Return windows and acceptance are set by commercial practice and differ by company. I found no reliable typical accepted %.

### Cited Findings
- Vendors say the system should sell the earliest-expiring batch first (FEFO), with expiry alerts set ahead of time. A batch-to-buyer trail matters for drug-licence compliance. — [Softcircles buying guide](https://softcircles.com/blog/pharmacy-billing-software-buying-guide); [Accountune batch/expiry guide](https://www.accountune.com/pharmacy-billing-batch-expiry-guide)
- Each purchase batch carries its own cost price, so margin is best read per batch. — [Accountune margin](https://accountune.com/medical-store-profit-margin)
- Under CBIC Circular 72/46/2018, a retailer can return time-expired drugs in one of two ways: as a fresh supply (a tax invoice) or by issuing a credit note under Section 34 of the CGST Act. — [TaxHeal](https://taxheal.com/circular-no-72-46-2018-gst-procedure-for-return-of-time-expired-drugs-medicines.html); [GSTZen](https://gstzen.in/a/central-tax-circular-722018-circular-to-clarify-the-procedure-in-respect-of-return-of-time-expired-drugs-or-medicines.html)
- An AIOCD letter from 2021 said credit notes should be settled within 60 days of the goods being received, should not carry a zero-value claim, and that LBE credit notes should not be uploaded to the GST portal. It also said customers were sending expiry goods back on a challan only. — [AIOCD letter via Medical Dialogues](https://medicaldialogues.in/pdf_upload/issues-causing-concerns-to-trade-members-146429.pdf)
- Common practice: retail can return stock from about 6 months before expiry, hospitals about 3 months before. Credit is commonly given at the original invoice price. This is not a regulation. — [Terra Insight](https://www.terra-insight.com/insights/pharma-expiry-returns-saleable-non-saleable-india/)
- There is a conflict on how late a credit note can be issued. One source says a post-2024 amendment caps it at 30 November of the following financial year. An older source says Section 34 sets no limit. — [Terra Insight](https://www.terra-insight.com/insights/pharma-distributor-return-reconciliation-india); [Masters India](https://www.mastersindia.co/blog/return-of-expired-drugs-medicine-under-gst/)

### Inferences
- The app needs: a batch master (batch, expiry, MRP, PTR, cost), FEFO auto-pick, a near-expiry report (for example 90 and 180 days), an expiry-return note per distributor that tracks credit note pending or received, and a settlement ageing view (the 60-day norm).
- The app should support both return modes: a debit note or challan from the chemist, and a credit note from the distributor.

### Gaps
- I found no source for the typical % of expiry claims that distributors actually accept, or for haircuts on claims (anecdotally varies by company).

## Loose / strip / unit sales

### Takeaway
Pharmacy software handles box, strip and tablet conversion as standard. The pack (for example a strip of 10) is bought, and single tablets can be sold at pro-rata MRP.

### Cited Findings
- GoFrugal lists both strip sale and loose sale among its POS features. — [GoFrugal](https://cdn-origin.gofrugal.com/blog/pharmacy-solution-highlights/)
- Vyapar converts automatically between box, strip and tablet, for example buying a box and selling tablets. — [Vyapar pharmacy](https://vyaparapp.in/free/billing-software-for-retail-shop/pharmacy)

### Inferences
- Item master: pack size (units per strip, strips per box), and selling unit at either strip or tablet level. Stock should be kept in base units. Bottles and syrups are usually not split.

### Gaps
- I found no regulatory guidance on loose-tablet sales (for example, the batch/expiry visible on cut strips).

## Purchases from distributors: PTR, PTS, schemes, discounts, credit, bill import

### Takeaway
A purchase bill line carries batch, expiry, MRP, PTR, quantity, free quantity (scheme such as 10+1), trade discount %, GST and sometimes cash discount. Distributors buy at PTS and sell at PTR. Credit periods are short (a BNPL offer of up to 45 days was reported). Bill import from distributors is common but poorly documented publicly.

### Cited Findings
- PTS = PTR minus the stockist margin % of PTR. — [TotalCalcHub](https://www.totalcalchub.com/in/utilities/ptr-pts-calculator)
- For scheduled drugs, NPPA prices include an 8% wholesaler margin and a 16% retailer margin. Under DPCO 2013, the 16% is applied on PTR. Non-scheduled drugs are not capped, and industry norms are about 10% for wholesalers and 20% for retailers. — [Lok Sabha answers (eparlib)](https://eparlib.nic.in/bitstream/123456789/597172/1/100323.pdf); [Vikaspedia drug pricing FAQ](https://en.vikaspedia.in/viewcontent/health/nrhm/national-health-policies/faqs-on-drug-pricing)
- Logic ERP records "Free Units" next to quantity in the purchase voucher, and the scheme % is tied to item and lot. For example, 100 bought with 25 free gives a 25% scheme. — [Logic ERP blog](https://www.logicerp.com/blog/?p=3098); [Logic KB](https://kb.logicerp.com/logic-erp-in-action-a-feature-overview/distribution/billing/auto-batch-wise-scheme-implementation-as-received-in-the-purchase)
- Retailio, based in Pune and founded in 2017, is described as India's largest pharma B2B ordering platform. In 2022 Rupifi BNPL gave retailers up to 45 days of credit on it. — [CB Insights](https://www.cbinsights.com/company/retailio); [Exploding Topics](https://explodingtopics.com/topic/retailio)

### Inferences
- Effective cost per unit = (PTR × qty − discount) ÷ (qty + free). Margin reports should use this figure, not PTR.
- The app needs a supplier ledger with due dates, payments, and adjustment of credit notes against bills.

### Gaps
- I could not confirm the bill import formats used by Marg, Retailio or PharmEasy B2B (CSV, e-invoice JSON, and so on). Typical distributor credit days and cash-discount norms (for example 1–2% for prompt payment) were also not found.

## Reorder, shortage book, substitutes, generics, Jan Aushadhi

### Takeaway
Software offers reorder levels and a drug index for salt-based substitutes. Jan Aushadhi gives up to 20% retailer margin plus a monthly incentive, but the current incentive figure conflicts between sources.

### Cited Findings
- GoFrugal has an integrated drug index for generic substitutes. Pharmacy Pro suggests substitutes. — [GoFrugal](https://cdn-origin.gofrugal.com/blog/pharmacy-solution-highlights/); [Logic ERP top-10](https://www.logicerp.com/blog/?p=7658)
- Jan Aushadhi gives up to 20% margin to retailers and up to 10% to distributors. — [PMBI FAQ](https://www.pmbi.co.in/FAQ.aspx)
- The incentive is 20% of monthly purchases up to ₹20,000/month according to [PIB Jan 2025](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2025/jan/doc202516481901.pdf). That conflicts with 15% up to ₹15,000/month in [Bajaj Finserv 2026](https://www.bajajfinserv.in/pradhan-mantri-jan-aushadhi-yojana).
- Branded generics are about 70–80% of the Indian retail drug market. — [Atradius 2018](https://atradius.co.uk/reports/market-monitor-chemicals-india-2018.html)

### Inferences
- The item master needs a salt/composition field to search substitutes. The shortage book should be built from below-reorder items plus items customers asked for that were out of stock, then turned into a purchase order per distributor.

### Gaps
- I found no source documenting the shortage-book habits of independent chemists.

## Stock counting, breakage, cold chain, schedule-wise shelving

### Takeaway
Regulation drives some features: a separate Schedule H1 register kept for 3 years, prescription-only sale for H/H1/X, and storage at the labelled temperature (insulin at 2–8°C).

### Cited Findings
- H1 supplies must be recorded in a separate register (prescriber, patient, drug, quantity), kept for 3 years and open to inspection. — [Wikipedia: D&C Rules](https://en.wikipedia.org/wiki/Drugs_and_Cosmetics_Rules,_1945); [GoFrugal H1](https://cdn.gofrugal.com/blog/schedule-h1-drugs/)
- Schedule H, H1 and X drugs may be sold only against a registered practitioner's prescription. — [GKToday](https://www.gktoday.in/schedule-h-schedule-x-and-schedule-h1-drugs/)
- Insulin must be stored at 2–8°C, and a refrigerator is needed for cold-chain lines (vaccines, insulin, some eye drops). — [Godrej Insulicool PR](https://godrejenterprises.com/Resources/uploads/InnerPress/220712PressReleaseInsuliCool_13121623.pdf); [Accountune](https://www.accountune.com/pharmacy-billing-batch-expiry-guide)

### Inferences
- Item flags needed: schedule (H/H1/X/G/OTC), cold-chain, and rack location. Other needed features: a damage/breakage write-off voucher and a physical-count variance.

### Gaps
- I found no data on stock-count frequency or breakage rates.

## SKUs, daily bills, margins, working capital

### Takeaway
Blended gross margin is in the high teens to low twenties, against 16% regulated on scheduled drugs. Listed chains run about 24–26% gross but only about 5% EBITDA. I found no solid data on SKU counts, bills per day or working-capital days for independents.

### Cited Findings
- Typical blended gross margin is high teens to low twenties. Generics and non-pharma lines run higher, and price-controlled lines lower. — [Accountune](https://accountune.com/medical-store-profit-margin)
- Fitch put the retailer share at about 20% of printed price. — [Moneylife](https://moneylife.in/article/indian-pharma-largely-unaffected-by-proposed-caps-on-trade-margin-says-fitch-ratings/58822.html)
- In Q1 FY26 MedPlus reported 26.1% gross margin and 4.7% EBITDA. — [AlphaStreet](https://alphastreet.com/india/medplus-health-q1-fy26-earnings-results)
- Branded-generic retailer margins can be very high, 200–1016% in one study, against 25–30% for brands. — [PMC study](https://pmc.ncbi.nlm.nih.gov/articles/PMC3081449/)

### Inferences
- Expiry write-offs plus slow movers tie up cash. Near-expiry and dead-stock reports directly protect working capital.

### Gaps
- No reliable source was found for typical SKU count (commonly quoted anecdotally as 1,500–5,000) or daily bills. Treat both as unverified.

## Customer side: repeat medicines, refills, udhaar, doctor-wise sales, delivery

### Takeaway
Indian pharmacy apps market refill reminders (often on WhatsApp) and doctor-wise sales reports. Patient credit (udhaar) is rarely documented, which suggests an opportunity for a khata-style product.

### Cited Findings
- Pharmacy108 offers refill alerts and sales analysis by doctor. Medbikri offers refill reminders, which users credit with repeat business. Pharmacy Pro sends WhatsApp refill reminders. — [Techjockey Pharmacy108](https://www.techjockey.com/detail/pharmacy108); [Capterra Medbikri](https://www.capterra.in/software/1040049/medbikri); [G2 Pharmacy Pro](https://www.g2.com/products/pharmacy-pro/reviews)
- Marg is described as offering doctor-wise sales tracking. — [Codingclave](https://codingclave.com/blog/pharmacy-management-software-india)

### Inferences
- The app needs: a patient profile with a regular-medicine list, a days-of-supply-based refill date, a khata/udhaar ledger, a doctor field on the bill, and a delivery flag.

### Gaps
- I found no data on home-delivery share or udhaar prevalence at independent chemists.
