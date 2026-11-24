# Indian compliance rules for jewellery counter features (as of Nov 2026)

Note: limited research budget (about 8 searches, no full-page fetches). Items marked [UNVERIFIED] come from background knowledge or a single secondary source and must be checked before they are built into the app.

## HUID / hallmark verification and bill contents

### Takeaway
Gold: a 6-character alphanumeric HUID is mandatory on hallmarked gold (since 1 July 2021), and buyers verify it in the BIS CARE app. Silver: since 1 Sept 2025, HUID is mandatory on any silver that gets hallmarked (IS 2112:2025), but hallmarking silver itself was still voluntary as of mid-2026. The bill must show each article's description, net weight, purity (carat and fineness) and the hallmarking charge.

### Cited Findings
- Each hallmarked gold article carries a 6-character alphanumeric HUID (since 1 July 2021), which can be verified in the BIS CARE app — [BIS Hallmarking FAQ](https://www.bis.gov.in/hallmarking-overview/hallmarking-faqs/hallmarking-faq)
- BIS CARE (official BIS app on iOS/Android) verifies ISI marks, hallmarks and CRS registrations — [App Store listing](https://apps.apple.com/in/app/bis-care-app/id6443724891)
- Bill/invoice for hallmarked articles must state each article's description, net weight of precious metal, purity in carat and fineness, and the hallmarking charges — [BIS Jewellers page](https://www.bis.gov.in/hallmarking-jewellers/); a law-firm article cites this as Regulation 5(11), with hallmarking charges shown separately — [Lakshmikumaran & Sridharan](https://lakshmisri.com/insights/articles/hallmarking-scheme-of-bis-safeguarding-the-purity-of-precious-metals)
- Hallmarking is charged per article (Rs 45 per gold article, Rs 200 minimum per consignment, plus tax), not by weight — [BIS FAQ](https://www.bis.gov.in/hallmarking-overview/hallmarking-faqs/hallmarking-faq)
- If an article is found to be below its marked purity, the buyer is owed twice the difference plus testing charges — [BIS consumer protection](https://www.bis.gov.in/hallmarking-overview/consumer-protection/)
- BIS has published 2026 jeweller guidelines (PDF) — [BIS Guidelines for Jewellers (Jul 2026)](https://www.bis.gov.in/wp-content/uploads/2026/07/Guidelines-for-Jewellers.pdf) (not read)
- Silver: from 1 Sept 2025, silver submitted for hallmarking must carry a 6-digit HUID under IS 2112:2025, together with the BIS mark, "SILVER" and the purity grade, and it is verifiable in BIS CARE — [Business Today, Dec 2025](https://www.businesstoday.in/personal-finance/investment/story/silver-jewellery-gets-safer-govt-makes-huid-compulsory-for-hallmarked-silver-boosting-transparency-consumer-trust-505408-2025-12-06); [GJEPC Solitaire](https://gjepc.org/solitaire/bis-reports-strong-early-response-to-mandatory-silver-huid-rollout/); [Vikaspedia](https://en.vikaspedia.in/viewcontent/social-welfare/social-awareness/consumer-education/hallmarking-of-jewellery/hallmarking-of-silver)
- Mandatory silver hallmarking: in June 2026 the BIS DG said BIS was still assessing readiness, had no timeline, and planned a gradual rollout — [Business Today, Jun 2026](https://www.businesstoday.in/personal-finance/news/story/will-silver-hallmarking-become-mandatory-bis-assesses-readiness-of-indian-market-534988-2026-06-05). BIS expanded the silver HUID ecosystem in Aug 2026 (about 77 lakh pieces, 341 AHCs, 102 districts) — [News on AIR](https://newsonair.gov.in/bis-expands-hallmark-unique-identification-based-silver-hallmarking-ecosystem/); [Business Today, Aug 2026](https://www.businesstoday.in/india/story/gold-price-surge-drives-silver-rush-bis-ramps-up-hallmarking-labs-across-india-548176-2026-08-09)
- Third-party commercial HUID verification APIs exist (Surepass, Meon) — [Surepass](https://surepass.io/huid-verification-api/); [Meon](https://meon.co.in/huid-verification-api)

### Inferences
- App: store HUID per item (validate as 6 uppercase alphanumeric characters) and print it on the bill line. Also print net weight, purity (for example 22K916 or 18K750; silver grades per IS 2112:2025) and hallmark charge as a separate line.
- A "Verify HUID" button should copy the HUID and open BIS CARE (or show instructions). No official deep link with the HUID pre-filled was found.
- Treat silver HUID as optional at the item level (required only if the piece is hallmarked). Keep a setting ready in case silver hallmarking becomes mandatory.
- [UNVERIFIED] Silver purity grades under IS 2112:2025 are commonly reported as 990, 970, 925, 900, 835 and 800.

### Gaps
- No official public BIS web URL or deep link/QR for HUID lookup with the code pre-filled was found. Whether BIS CARE shows jeweller registration, AHC, article type, purity and hallmark date was not confirmed from a primary source.
- Whether the Regulations require the HUID number itself on the bill was not confirmed; BIS pages list only description, weight, purity and charges. Printing it is good practice.
- A report that silver hallmarking becomes mandatory from Oct 2026 was found only in secondary reporting and was not confirmed.

## Old gold from unregistered persons (URD)

### Takeaway
A jeweller buying old jewellery from an individual (not in business) is not liable to GST under reverse charge (s.9(4)), per the 2017 Finance Ministry clarification. When old gold is resold after only minor processing, GST may be paid on the margin (Rule 32(5)); melting it into bars or lumps loses margin-scheme treatment (advance rulings).

### Cited Findings
- Finance Ministry clarification: a jeweller buying old gold jewellery from an individual is not liable under s.9(4) RCM, because the individual is not supplying in the course of business. Purchases from an unregistered supplier who is in business were treated differently — [PIB, 2017](https://pib.gov.in/PressReleasePage.aspx?PRID=1495551); [The Quint](https://www.thequint.com/news/india/gst-old-gold-jewellery-finance-ministry)
- Rule 32(5) margin valuation is available for second-hand goods resold as such or after minor processing that does not change their nature. Melting into irregular shapes or lumps is not eligible — [Taxmann on AAR](https://taxmann.com/post/blog/margin-scheme-benefit-not-available-for-melting-old-gold-jewellery-into-irregular-gold-shapes-aar); [GST Council AAR: Aadhya Gold](https://gstcouncil.gov.in/hi/node/2167); [AAR: Attica Gold](https://www.gstcouncil.gov.in/hi/node/2192); [LiveLaw](https://livelaw.in/amp/news-updates/gst-difference-purchasing-selling-price-second-hand-gold-jewellery-aar-229017)
- The CBIC press release on reverse charge (deferment of s.9(4)) — [CBIC](https://cbic-gst.gov.in/pdf/press-release/Press-Release-Reverse-Charge.pdf)

### Inferences
- [UNVERIFIED, background] Since 1 Feb 2019, s.9(4) RCM applies only to notified classes of registered persons (essentially promoters in real estate), so ordinary URD old-gold purchases attract no RCM. Jewellers commonly issue an internal "purchase voucher" or "URD purchase bill" with the seller's name, address, ID/PAN, item description, gross/net weight, purity tested, rate and amount, and the seller's signature.
- A purchase voucher can stand alone, because buying old gold is a separate inward transaction. Exchange against a new sale is then a payment-adjustment on the sale invoice, where GST on the new item is charged on its full value. [UNVERIFIED: full-value treatment in exchange, from common CA practice]
- App: give URD purchase its own voucher series, independent of sales, and allow it to be linked to a sale as a payment mode called "old gold adjustment".

### Gaps
- No primary 2026 CBIC text was fetched confirming the current s.9(4) notification scope.
- No prescribed format for the URD purchase voucher was found. Rule 46 tax invoice rules do not directly apply to inward purchases from URD.

## Cash limits: s.269ST, payments, PAN/Form 60, new Act 2025, PMLA

### Takeaway
A jeweller must not receive ₹2 lakh or more in cash from one person in a day, for one transaction, or for one event/occasion (old s.269ST). The penalty equals the amount received (s.271DA). Under the Income-tax Act 2025, effective 1 April 2026, this rule reportedly sits at s.186, although the section number is disputed. PAN or Form 60 is needed for jewellery/bullion purchases above ₹2 lakh. Under PMLA, cash dealings of ₹10 lakh or more with a customer (single or linked transactions) bring reporting and KYC duties to FIU-IND.

### Cited Findings
- s.269ST text and exceptions (Government, banks, post office savings banks, co-operative banks) — [incometaxindia.gov.in s.269ST](https://www.incometaxindia.gov.in/w/section-269st-8); [Modes of receipts and payments](https://www.incometaxindia.gov.in/w/mode-of-receipts-and-payments)
- Income-tax Act 2025 is listed on the department site and applies from 1 April 2026 — [Income-tax Act 2025](https://www.incometaxindia.gov.in/en/income-tax-act-2025)
- s.269ST is said to map to s.186 of the 2025 Act — [TaxClue mapping](https://www.taxclue.in/blog/section-269ss-269st-income-tax-act-2025-mapping/); [Indian Kanoon s.186](https://future.indiankanoon.org/doc/47068600). A blog instead says s.221 — [incorpx](https://www.incorpx.io/blog/cash-transaction-limits-small-business-2026) (conflict, likely less reliable)
- PMLA: notification of 28 Dec 2020 makes dealers in precious metals and stones "designated businesses" when they enter into cash transactions of ₹10 lakh or more with a customer, in one operation or in several that appear linked. They must keep records and report to FIU-IND — [TaxGuru (MoF notification)](https://taxguru.in/finance/dealers-precious-stones-real-estate-agents-notified-pmla-act-2002.html); [RSM India](https://www.rsm.global/india/sites/default/files/media/RSM%20India/Publications/2021/rsm_india_newsflash_-_record_of_cash_transactions_in_gems_jewellery_over_and_above_rs._10_lakhs_mandatory_under_pmla.pdf)
- Department of Revenue (Jan 2021): purchases below ₹2 lakh need no PAN or Aadhaar. Cash purchases above ₹2 lakh already required KYC under existing law (Rule 114B) — [Business Today](https://businesstoday.in/current/economy-politics/only-purchase-of-jewellery-above-rs-2-lakh-needs-mandatory-kyc-clarifies-govt/story/427523.html); [Deccan Herald](https://www.deccanherald.com/business/no-new-kyc-disclosure-for-jewellery-purchase-kyc-only-for-high-value-cash-purchases-revenue-department-936757.html)

### Inferences
- s.269ST limits receipts. When a jeweller pays cash to a member of the public for old gold, the seller is the one receiving it, so the seller is the receiver under 269ST. The jeweller's own risk is s.40A(3): business expenditure paid in cash above ₹10,000 per person per day is disallowed. [UNVERIFIED: whether 40A(3) applies to stock purchases from individuals, and its new-Act number] s.269SS/269T cover loans and deposits, which are relevant to girvi/gold loans (see below), not to purchases.
- App: block or warn when cash received from one customer reaches ₹2,00,000 or more, aggregated per day, per bill and per event. Make PAN or Form 60 mandatory when a sale exceeds ₹2 lakh. Warn when cash paid out for old gold exceeds ₹10,000 per day (40A(3)). Run a PMLA monitor for cash of ₹10 lakh or more per customer per month, with a CTR export.
- [UNVERIFIED] Rule 114B thresholds may have been revised under the Income-tax Rules 2026. Check the new rules before hard-coding them.

### Gaps
- The new-Act section numbers for 269ST, 271DA, 40A(3) and 114B (now in the Rules 2026) were not confirmed from the enacted text.
- No 2025–26 change to the ₹2 lakh or ₹10 lakh thresholds was found, which does not prove there was none.

## State Pawnbrokers / Money-lenders Acts (girvi / gold loans)

### Takeaway
A jeweller who lends against gold is generally covered by the state Money-lenders or Pawnbrokers Act. These Acts typically require a licence, a pawn ticket or receipt, interest within notified caps, and notice before auction. The research found statute links but not the state-wise caps.

### Cited Findings
- Maharashtra Money-Lending (Regulation) Act, 2014 — [PRS](https://prsindia.org/files/bills_acts/acts_states/maharashtra/2014/2014MH8.pdf)
- Rajasthan Money-Lenders Act, 1963 — [India Code](https://www.indiacode.nic.in/bitstream/123456789/18781/1/rjeng.pdf)
- Karnataka Pawnbrokers Act, 1961, which has a section on interest and charges allowed (example of the model) — [PRS](https://prsindia.org/files/bills_acts/acts_states/karnataka/1962/1962KR13.pdf)
- Secondary sources say gold loan rates in Gujarat and Maharashtra are not fixed by regulation for RBI-regulated lenders — [IIFL Gujarat](https://www.iifl.com/blogs/gold-loan/gold-loan-rules-gujarat-2026); [IIFL Maharashtra](https://www.iifl.com/blogs/gold-loan/gold-loan-new-rules-in-maharashtra-2026-state-wise-impact-guide)

### Inferences
- [UNVERIFIED, background] Gujarat Money-Lenders Act 2011, Bombay/Maharashtra 2014, MP Money-Lenders Act 1934, UP Regulation of Money-Lending Act 1976, Rajasthan 1963, and the Punjab Registration of Money-Lenders Act/Delhi rules each require a licence, a pawn ticket and periodic accounts, and allow capped or notified interest. Maharashtra notifies caps (reported around 9% secured / 12% unsecured p.a.). Auction usually needs prior written notice. Verify every figure before use.
- App: on the girvi ticket, record licence number, pawner identity, item weight and purity, principal, interest rate, and date. Give the state-specific rate cap as a configurable warning, and generate an auction notice letter with a configurable notice period.

### Gaps
- No primary-source interest caps, licence fees or auction-notice periods were found for Gujarat, MP, UP or Delhi.
