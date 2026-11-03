# Retail pharmacy (chemist) compliance in India, 2026: what billing/khata software must support

Note: 5 web searches done (a light pass). Most sources are secondary (state notices, legal or vendor blogs, exam-prep sites). The consolidated text of Rule 65 was not fetched. Check every clause against the official Drugs Rules, 1945 PDF on cdsco.gov.in before building to it.

## Licences: retail drug licence, pharmacist, validity, FSSAI, GST

### Takeaway
A retail chemist needs Form 20 (non-biological drugs) and Form 21 (biological/Schedule C, C1 drugs) licences from the State Drug Controller, with a registered pharmacist. Since 2017 these licences do not expire. They stay valid as long as a retention fee is paid in every five-year block. Forms 20B/21B are wholesale licences and need a "competent person" rather than a pharmacist.

### Cited Findings
- Rule 63, as substituted by G.S.R. 1337(E) of 27 Oct 2017: a retail licence needs no renewal, only a retention fee (equal to the grant fee) paid before each successive five-year period ends — [calcguru](https://calcguru.in/?p=4879)
- Delhi fee schedule (2026): retention Rs 1,500, late payment adds 2% of the licence fee per month, retention certificate Rs 150 — [Delhi Drugs Control](https://drugs.delhi.gov.in/sites/default/files/2026-03/forms_and_fees_sale_licences.pdf)
- Retail licences (20/21) need a registered pharmacist. Wholesale licences (20B/21B) need a competent person — [calcguru](https://calcguru.in/?p=4879); [registerkaro](https://www.registerkaro.in/post/pharmacy-license)
- Conflict: some sites still describe licences as "valid for five years" or renewed annually depending on the state. These look outdated — [agileregulatory](https://www.agileregulatory.com/blogs/what-is-form-20-and-21-drug-license); [startupgrantsindia](https://www.startupgrantsindia.com/services/drug-license/faqs)
- Meghalaya publishes a separate "Retention Certificate for Retail Licence" form — [meghealth](https://www.meghealth.gov.in/forms/Retention_Certificate_for_Retail_Licence.pdf)

### Inferences
- The app should store each licence: number, form type (20/21/20B/21B), issue date and the next retention due date (issue date + 5 years, repeating). It should also store the pharmacist's name, registration number and State Pharmacy Council. The licence numbers are printed on every bill.
- Remind the shop before the retention fee falls due, because late payment adds 2% a month.

### Gaps
- Not verified in this pass: an FSSAI licence or registration for nutraceuticals and supplements, and GST registration rules for pharmacies (a Rs 40 lakh goods turnover threshold; medicines mostly 5%, some 12%/18% after the 2025 GST rate rationalisation). Check both with the GST and FSSAI researchers.
- Whether the pharmacist must be present for all opening hours, and the rules on displaying the pharmacist's name. These were not sourced.

## Schedules H, H1, X, G and NDPS: registers, retention and bill fields

### Takeaway
Schedule H and H1 drugs are sold only against a prescription. Every H1 sale goes in a separate register (prescriber name and address, patient name, drug, quantity), kept for 3 years. Schedule X needs a duplicate prescription, with one copy kept for 2 years, and the stock kept under lock. Other Rule 65 registers are kept for at least 2 years from the last entry. Pregabalin was moved into Schedule H1 in May 2026.

### Cited Findings
- H1: record the supply in a separate register with the prescriber's name and address, the patient's name, the drug name and the quantity. Keep it for 3 years, open for inspection — [NHSRC](https://www.nhsrcindia.org/sites/default/files/2021-07/Attachment%20A%2C%20Schedule%20H1%20register%20and%20NDPS%20Forms.pdf); [Nagaland IPR](https://ipr.nagaland.gov.in/node/16415)
- Rule 65 (ordinary registers): kept for at least 2 years from the date of the last entry. H1 is the 3-year exception — [accountune](https://accountune.com/pharmacy-billing-batch-expiry-guide); [okcredit](https://okcredit.in/blog/dos-and-donts-for-medical-store-owners/) (secondary sources)
- For Schedule C, H or H1 drugs, record the manufacturer's name, batch number and expiry date along with the prescriber and patient. The cash memo carries the seller's name, address and licence number, a serial number, the drug and the quantity — [petpooja](https://blog.petpooja.com/templates-checklist/pharmacy-medical-store-billing-template/) (vendor blog, unverified against the rule text)
- Schedule X: dispense only against duplicate prescriptions, keep one copy for 2 years, keep stock under lock (Rule 65(9)(a)) — [okcredit](https://okcredit.in/blog/dos-and-donts-for-medical-store-owners/)
- Pregabalin moved into Schedule H1 (May 2026) — [InsightsIAS](https://www.insightsonindia.com/2026/05/23/health-ministry-brings-pregabalin-under-schedule-h1/)
- H1 covers selected 3rd/4th-generation antibiotics, anti-TB drugs and habit-forming drugs — [gktoday](https://www.gktoday.in/schedule-h-schedule-x-and-schedule-h1-drugs/)
- 2026 draft amendment to Rule 65: CCTV at shops selling H, H1 and X drugs. Still a draft, status unconfirmed — [Sanskriti IAS](https://www.sanskritiias.com/current-affairs/drugs-rules-1945-amendment-cctv-proposed-for-medical-stores-selling-schedule-h-h1-and-x-drugs); [gktoday](https://www.gktoday.in/health-ministry-proposes-amendments-to-drugs-rules-1945-for-schedule-h-h1-and-x-drugs/)
- Some districts issue extra local orders, for example a Kohima list of 46 drugs whose sales must be recorded in a separate register — [Nagaland IPR](https://ipr.nagaland.gov.in/node/12012); [Herald Goa](https://www.heraldgoa.in/goa/sale-of-46-drugs-regulated/408807)

### Inferences (app requirements)
- Item master: schedule flag (OTC / G / H / H1 / X / C / NDPS), manufacturer, HSN, GST rate, MRP, and whether the item sells by strip or loose units.
- Purchase and stock: batch number, expiry date and MRP per batch, plus the supplier's invoice and licence number.
- Sale bill: the shop's licence number(s) and GSTIN, a serial number, date, patient name, doctor name (with registration number and address for H1), and per line the drug, manufacturer, batch, expiry, quantity and MRP. For H/H1/X items, block the sale until the prescriber and patient fields are filled.
- Auto-generate an H1 register and a Schedule X register from sales, exportable as PDF or print. Keep the data for at least 3 years, and do not let users hard-delete entries.
- Store a photo of the prescription per sale (useful for X duplicates and for proof at inspection).
- Keep the schedule lists updatable, because drugs get reclassified (for example pregabalin).

### Gaps
- NDPS (narcotic) retail records and forms under state NDPS rules were not researched. The NHSRC PDF title mentions NDPS forms.
- The Schedule G label ("Caution: to be used under medical supervision") needs no register. This is from background knowledge, not a source.

## Prescriptions, e-prescriptions, online pharmacy and antibiotics

### Takeaway
As of 2026 there are still no notified e-pharmacy rules. The 2018 draft (G.S.R. 817(E)) was never finalised, so online and home-delivery sales fall under the Drugs and Cosmetics Act/Rules, the Pharmacy Act and court orders. The antibiotic "Red Line" (red vertical band on the pack) campaign was not confirmed in this pass.

### Cited Findings
- India has no e-pharmacy-specific regulation as of mid-2026. The draft rules have stalled for years, and offline chemist associations campaigned against online pharmacy in Feb–May 2026 — [LSHTM report](https://www.lshtm.ac.uk/media/102426)
- Online sellers currently follow the D&C Act 1940, D&C Rules 1945, Pharmacy Act 1948 and IMC Act 1956 — [Vajiram](https://vajiramandravi.com/current-affairs/e-pharmacies-on-regulating-online-sale-of-drugs-in-india/)
- The draft required registration with the CDSCO and data localisation — [Medical Dialogues](https://business.medicaldialogues.in/health-ministry-draft-rules-on-sale-of-drugs-by-e-pharmacy-details)

### Inferences
- For home delivery, the app should record the prescription image, delivery address and pharmacist sign-off. Do not market the app as an "e-pharmacy".

### Gaps
- Red Line antibiotic campaign (2016, Schedule H1 / Rx-only antibiotics carrying a red band): not confirmed by any source found here.
- Whether e-prescriptions (digital or ABDM-issued) are accepted for H1/X sales: not found.

## Expired drugs, returns, disposal, inspections and penalties

### Takeaway
Very little sourced material on this in this pass. Drug inspectors audit H1 registers and stock. In 2026, one district acted against 32 stores (3 licences suspended, 8 registrations cancelled).

### Cited Findings
- Bhopal: action against 32 medical stores, 3 licences suspended — [Free Press Journal](https://www.freepressjournal.in/bhopal/action-against-32-medical-stores-licences-of-three-suspended-eight-registrations-cancelled)
- H1 registers must be kept available for drug inspector audits — [InsightsIAS](https://www.insightsonindia.com/2026/05/23/health-ministry-brings-pregabalin-under-schedule-h1/)

### Inferences
- The app should list near-expiry stock (for example 90, 60 and 30 days ahead) and record "expired, held separately, not for sale" stock and returns to the supplier (credit note, batch, quantity).

### Gaps
- Not sourced: the rule requiring expired stock to be stored separately and labelled "not for sale", returns policy between AIOCD members and suppliers, CPCB/Biomedical Waste Rules 2016 disposal of pharmaceutical waste, and the penalties under Sections 18 and 27 of the D&C Act (as amended by the Jan Vishwas Act 2023).

## Drug price control: DPCO 2013, NPPA, MRP

### Takeaway
A retailer may not sell above the MRP printed on the pack, or above the NPPA ceiling price plus local taxes for scheduled formulations. Any overcharge can be recovered with interest, from retailers too. Retailers must display the manufacturer's price list in the shop (DPCO para 24(4)). Discounts below MRP are allowed.

### Cited Findings
- MRP must not exceed the ceiling price plus applicable taxes. Selling above it counts as overcharging and is recoverable with interest — [Vikaspedia FAQ](https://vikaspedia.in/health/nrhm/national-health-policies/faqs-on-drug-pricing); [DoP speaking order](https://www.pharma-dept.gov.in/sites/default/files/Speaking%20Order-IPA.pdf)
- If a retailer, distributor or stockist sells above the ceiling price, the overcharge is recoverable — [DoP notification](https://www.pharma-dept.gov.in/sites/default/files/notification%20dpco.pdf)
- Para 24(4): every retailer and dealer must display the price list and any supplementary price list on a conspicuous part of the premises — [Vikaspedia](https://vikaspedia.in/health/nrhm/national-health-policies/faqs-on-drug-pricing)
- DPCO is issued under the Essential Commodities Act — same source

### Inferences
- The app should block a selling price above the batch MRP, and flag it when the MRP is above the NPPA ceiling (optionally by importing the NPPA list). Allow line or bill discounts and print them. Work out the price of a loose tablet as strip MRP ÷ units per strip.

### Gaps
- Penalty amounts under the Essential Commodities Act. The 2026 ceiling price revisions (WPI-linked annual change each April) were not checked.

## 2024–2026 changes: QR codes, track-and-trace, portals, ABDM

### Takeaway
Since 1 Aug 2023, the top 300 brands (Schedule H2, G.S.R. 823(E)) must carry a QR code or barcode on the pack. Secondary sources report that a June 2026 notification widens this to vaccines, antimicrobials and cancer drugs (not verified in the gazette). Pregabalin moved to H1 and a CCTV rule was drafted in 2026.

### Cited Findings
- Schedule H2, G.S.R. 823(E) of 17 Nov 2022, in force from 1 Aug 2023: a QR code or barcode on the primary label (secondary label if space is short) for 300 brands. It applies to batches made on or after that date — [CDSCO FAQ](https://cdsco.gov.in/opencms/resources/UploadCDSCOWeb/2018/UploadPublic_NoticesFiles/Final%20FAQs%20on%20QR%20code%2021.07.2023.pdf)
- 22 June 2026 notification brings vaccines, antimicrobials and anticancer drugs under Schedule H2 — [Daily Pioneer](https://www.dailypioneer.com/news/govt-expands-qr-code-tracking-to-key-drug-categories-to-curb-counterfeits); [Anantam IAS](https://anantamias.com/current-affairs/qr-code-drug-traceability-vaccines-antimicrobials/?pdf=1) (unverified against the gazette)
- The QR code holds the product ID, generic name, brand, manufacturer, batch, manufacture and expiry dates and licence number. (This comes from the CDSCO FAQ and needs checking.)

### Inferences
- Scanning the pack's QR code at purchase could fill in batch, expiry and manufacturer automatically. This would be a strong feature for the app.

### Gaps
- ABDM/ABHA linkage for pharmacies (the HPR/HFR registry, e-prescription pull), state online drug-licence portals (ONDLS / SUGAM) and any national track-and-trace database: not researched in this pass.
