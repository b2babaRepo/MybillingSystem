# Electrical sales and service shop: operations (India, 2026)

Note on evidence: public web sources on the back office of small dealers (claim rates, dealer margins, TA/DA rules) are thin. Most cited material comes from brand warranty documents, job listings, government tenders and PM-KUSUM tender terms. Practices described only from domain knowledge appear under Inferences, not as cited facts.

## Buying: purchase from company or distributor, schemes, credit, stock of units and spares

### Takeaway
Dealers buy through company or distributor channels on short credit, typically 15 to 30 days, or through channel financing. Company "dealer support schemes" are large enough to move brand margins. I found no current public figures for scheme mechanics or spare-parts buying.

### Cited Findings
- A pan-India dealer survey (Investec, around 2015) found Havells debtor days of about 15 to 30 days. Finolex Cables typically gave dealers no credit, and one peer preferred channel financing to a credit period. — [Investec dealer survey](https://www.investec.com/content/dam/india/insights/Pan-India-dealer-survey-Demand-weak-but-market-shares-intact.pdf)
- Motilal Oswal (2017) linked a dip in Havells' margin to the launch of support schemes for dealers. Schemes are a material channel cost. — [Motilal Oswal note](https://www.motilaloswal.com/site/rreports/HTML/636204129240926853/index.htm)
- Kirloskar says its 1-year product warranty is backed by spare-parts availability and a network of service engineers and appointed partners. — [Kirloskar Koel-Care services](https://www.kirloskaroilengines.com/care/water-solutions/services)

### Inferences
- An app needs purchase bills from the company or distributor with scheme or discount lines: cash discount, quantity or target slab, turnover rebate credited later as a credit note. It also needs a payable ageing view against the credit period.
- Stock has two classes. Serial-numbered whole units (pumps, motors, inverters, batteries, ACs) need serial capture at goods inward. Loose spares are counted by quantity or weight: capacitors, bearings, mechanical seals, PCBs, impellers, and winding copper wire bought by kg.
- Defective parts returned to the company, and replacement units received against claims, need their own stock state ("defective, awaiting return" and "claim replacement received"). This keeps them out of saleable stock.

### Gaps
- No 2024 to 2026 public data on dealer margins, scheme structures or credit terms for Crompton, CRI, Luminous, V-Guard or Kirloskar. Sources found date from 2015 to 2018.
- No public company dealer price lists were found. They are usually shared privately as PDFs or through dealer apps.

## Selling: retail and B2B, installation, warranty registration, serial numbers

### Takeaway
Warranty validity depends on the invoice matching the product serial or barcode. A defaced or mismatched serial voids the claim. Serial capture on the sale invoice, plus warranty-card registration, is therefore a core selling task. Government-scheme solar pump sales bring 5-year service obligations.

### Cited Findings
- Luminous rejects battery claims if the warranty-card barcode sticker is defaced or tampered with, or does not match the battery's barcode. Claims need the warranty card and original invoice. One manual asks for the card to be sent within 7 days to register the warranty. — [Luminous LIFEMAX manual](https://manuals.luminousindia.com/manuals/LIFEMAX.pdf), [Luminous Eco Charge manual](https://manuals.luminousindia.com/manuals/eco-charge-wc-6963290.pdf)
- Exide claims go through an authorised retailer with proof of purchase. The store uses the purchase record and/or the battery serial. Tampering with the serial or label voids the warranty. — [Exide warranty terms](https://www.exideindustries.com/products/automotive-batteries/automotive-batteries-warranty.aspx), [ExideCare battery warranty](https://www.exidecare.com/warranty_terms_and_conditions/batteries)
- Repair-claim guides note that claims are often rejected when the invoice does not match the serial number. — [Laptop Repair World](https://www.laptoprepairworld.com/blog/laptop-warranty-claim-process-india/)
- Taro (Texmo) pumps carry a 2-year warranty with two free services from authorised service centres. — [Taro five ticks](https://www.taropumps.com/blog/the-five-ticks-guarantee)
- Under PM-KUSUM Component B, tenders require the vendor to cover full system warranty, repair and maintenance for 5 years. Some states add a 5-year AMC. — [SECI PM-KUSUM RfS](https://www.seci.co.in/Upload/Tender/SECI000093-7633709-RfSunderPM-KUSUM_Component-B-Finalupload.pdf), [Mercom Arunachal](https://mercomindia.com/arunachal-pradesh-500-solar-water-pumps)

### Inferences
- The invoice must carry serial or barcode per line, warranty period and start date, and installation charges as a separate line. Installation is a service with a different GST nature than the goods.
- B2B customers need credit accounts and dues tracking. These include farmers (often paying after harvest), contractors and builders, and housing societies (pumps, AMC).
- A "warranty registered with company: yes or no, date" flag per sold serial would help avoid later claim rejections.

### Gaps
- No source for typical installation charges per product (pump, AC, solar), or for each brand's online registration flow.

## Service: walk-in jobs, field visits, warranty claims, paid repairs, AMC, rewinding

### Takeaway
Service work runs on job cards, rewinding logs and field service reports, often signed by the customer. Rewinding is priced by HP band. Government solar contracts impose strict response-time penalties, such as fixing within 5 days or paying per pump per day. Labour is often billed separately from parts at company price-list rates.

### Cited Findings
- A service-manager role at a motor and generator rewinding firm requires maintaining service records, job cards, rewinding logs and test reports. — [Qureos job listing](https://app.qureos.com/jobs/assistant-service-manager-1757575673499)
- Solar water-pump field technicians must document faults in a Field Service Report with the farmer's signature as confirmation. — [SimplyHired Hingoli FSE](https://www.simplyhired.co.in/job/966q7EHAX7N3Iv7kCqNMXoxabBxlXb5oFSTZZ1VL4gb6Ii-hvXvD-A)
- Rewinding rates: a BHEL 2022 tender gives about Rs 5,104 per motor for winding 8 to 12 HP submersibles. Municipal schedules price work by HP band (above 3 to 7.5 HP, 10 to 20 HP). A vendor listing gives Rs 1,500 to 6,000 for submersible repair or rewinding and Rs 1,200 to 8,000 for motor rewinding. — [BHEL price bid](https://tenders.bhel.com/sites/default/files/Price_Bid_OT-02-2022-04-09-12%3A14%3A39.pdf), [Surat Municipal](https://suratmunicipal.gov.in/Contents/News/13335.pdf), [FatafatService](https://www.fatafatservice.com/vendor-categories/motor-and-pump-service/abdul-gafoor)
- Out-of-warranty service charges replaced components at the company's prevailing price list (A. O. Smith, a water purifier, shown as an analogous pattern). — [A. O. Smith manual](https://www.manualslib.com/manual/1313550/A-O-Smith-X8.html?page=28)
- D-Link waives labour if the same fault recurs within 30 days of a repair (a service-warranty pattern). — [D-Link terms](https://service.dlink.co.in/resources/Dlink%20Service%20Center%20-%20Terms%20&%20Conditions.pdf)
- PM-KUSUM response terms:
  - Arunachal: fix within 5 days, then a penalty of Rs 500 per pump per day for 3 days and Rs 1,000 per pump per day after that.
  - Haryana: ordered deposits forfeited for pending complaints, with a penalty starting around Rs 100 per pump per day.
  - Odisha: a service centre per district and 95% uptime, with a blacklisting risk.
  — [Mercom Arunachal](https://mercomindia.com/arunachal-pradesh-500-solar-water-pumps), [Saur Energy Haryana](https://www.saurenergy.com/solar-energy-news/pm-kusum-haryana-orders-to-forfeit-deposits-of-errant-solar-firms), [Renewable Watch OREDA](https://renewablewatch.in/2023/09/19/oreda-invites-vendors-for-solar-pumps-under-pm-kusum/)
- Taro advertises a 48-hour service turnaround. — [Taro 48-hour](https://taropumps.com/about-us/5-ticks/48-hour-service-turnaround)
- Battery warranty is void if periodic distilled-water topping is skipped or the gravity is above 1.260. This drives periodic maintenance visits. A dealer told one forum user to top up yearly and inspect every six months (anecdotal). — [Luminous manual](https://manuals.luminousindia.com/manuals/LIFEMAX.pdf), [Team-BHP](https://www.team-bhp.com/forum/gadgets-computers-software/39704-inverter-batteries-69.html)
- In consumer disputes, dealers and manufacturers have been held jointly liable for defective parts under warranty. — [Indian Kanoon](https://future.indiankanoon.org/doc/26794575/)

### Inferences
- Job card fields: customer, product and serial, complaint, in-warranty flag (from the sale record), estimate and approval, parts used (deducted from stock), labour, rewinding details (HP, wire gauge, copper kg), test result, delivery, and payment or dues.
- Field calls need: a call log with complaint date (for SLA and penalty clocks), technician assignment, visit or trip records, customer signature or photo, and a link to the job and invoice.
- A warranty claim is a separate document against the company. It covers parts replaced free, labour or visit charges claimed, and defective parts returned (with a dispatch record), with status submitted, approved, credited or rejected.
- AMC: contract with start and end dates, fee, number of scheduled visits, auto-generated visit due list, and visit completion.

### Gaps
- No public source gives brand-specific warranty-claim reimbursement rates (labour per call) for pump or inverter service partners, or typical claim settlement delays. Agreements are private.

## Technicians: pay, TA/DA, travel, lodging, advances

### Takeaway
Field technician jobs in pump, solar and battery service pay roughly Rs 15,000 to 40,000 a month, with travel allowance and mobile allowance listed as benefits. Roles involve frequent and sometimes multi-state travel. I found no public source on DA rates or advance-settlement practice at small dealers.

### Cited Findings
- GEMS field service engineer (Kolar): Rs 17,000 to 25,000 a month, with PF, travel allowance and health insurance. — [Apna GEMS](https://www.apna.co/job/kolar/field-service-engineer-483000262)
- SunAP solar installation (Bengaluru, South India travel): Rs 30,000 to 40,000 a month, with travel allowance and mobile allowance. — [Apna SunAP](https://apna.co/job/bengaluru-bangalore/field-service-engineer-932100727)
- Battery and motor field technician (Mumbai): Rs 15,000 to 20,000 a month, frequent travel. — [SimplyHired](https://www.simplyhired.co.in/job/n041C85CSQ_2h_3p6t1LBKc3avW6P64F3cBxAQU1UnzN-tT4M0wOkQ)
- Shop-based rewinding operator (Bangalore): Rs 25,000 to 30,000 a month, day shift. — [JobHai](https://www.jobhai.com/technician-electrical-motor-rewinding-operator-job-in-shree-vigneshwara-electricals-electronic-city-bangalore-0-to-5-years-1756713375-6479944-jid)

### Inferences
- An app needs technician master records with a pay type (salary, per job, or mixed). It also needs:
  - A trip or expense log per visit: fuel or km, bus and train fares, lodging, meals or DA, with bill photos.
  - Advances given, settled against submitted bills, with the balance carried forward.
  - Cash collected on site by the technician, reconciled with the shop.
- Expenses should link to the job or claim so trip cost is visible against job revenue.

### Gaps
- No public DA or TA rate norms for small dealers, and no source on per-job pay rates for rewinders or installers.

## Common problems

### Takeaway
The documented pain points are claim rejection over paperwork or serial mismatches, unclear warranty and service accountability between dealer and company, and penalty exposure from slow complaint resolution on government solar pumps. Lost site cash and unbilled visits are plausible but not documented in public sources.

### Cited Findings
- Claims are rejected over an invoice-serial mismatch or a tampered or mismatched barcode. — [Luminous manual](https://manuals.luminousindia.com/manuals/LIFEMAX.pdf), [Laptop Repair World](https://www.laptoprepairworld.com/blog/laptop-warranty-claim-process-india/)
- Consumer complaints and forum cases show service-centre delays and disputes over who is accountable (brand or authorised centre). — [NewsMeter Apple case](https://newsmeter.in/hyderabad/apple-india-asked-to-pay-rs-5k-to-hyd-customer-for-delay-in-replacing-damaged-airpods-703985), [ConsumerComplaints Crompton pump](https://www.consumercomplaints.in/crompton-greaves-cgglobal-com-complaint-against-cg-power-and-industrial-solutions-limited-regarding-defective-mono-block-pump-of-1-hp-c3138133)
- Haryana moved to forfeit deposits of solar pump firms with pending complaints. — [Saur Energy](https://www.saurenergy.com/solar-energy-news/pm-kusum-haryana-orders-to-forfeit-deposits-of-errant-solar-firms)

### Inferences
- Features that address these: serial-linked sale records, parts-to-job traceability, a pending-claims ageing list, a list of visits closed with no invoice, technician cash-in-hand, and customer dues ageing.

### Gaps
- No quantified data on warranty claim settlement time, the share of unbilled visits, or cash leakage at Indian dealers.
