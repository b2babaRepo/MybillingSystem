# Accounting, GST and P&L for Indian electrical sales + repair/installation/field-service shops (as of Nov 2026)

Note: this pass used 6 tool calls. Most sources are secondary (blogs and vendor sites). The CBIC rate notification (Notification 9/2025-Central Tax (Rate), effective 22 Sep 2025) was not fetched directly, so every HSN rate below is marked [CA-CONFIRM]. Items marked (unsourced) come from general knowledge and must be checked.

## GST: goods rates, repair/installation, AMC, warranty, labour-only

### Takeaway
After GST 2.0 (22 Sep 2025), ACs moved from 28% to 18%. Solar modules dropped to 5%. Motors, transformers/inverters/UPS and batteries are 18%. Sources disagree on fans and on pumps (8413). Repair, installation and AMC services under SAC 9987 are generally 18%. Goods plus installation on one bill is usually a composite supply taxed at the principal supply's rate (the goods). Free warranty replacements carry no GST and need no ITC reversal (Circular 195/07/2023, extended by 216/10/2024). When a dealer invoices the manufacturer for warranty parts or labour, that invoice is taxable.

### Cited Findings
- AC (HSN 8415): rate cut from 28% to 18% effective 22 Sep 2025 — [ClearTax AC](https://cleartax.in/s/gst-air-conditioners); [Tally](https://tallysolutions.com/gst/new-gst-rates-september-2025-business-impact/)
- Solar cells/modules/panels (HSN 8541): 12% to 5%. Solar inverters and charge controllers are 18% per one source, but disytax claims 5% for solar inverters, so the sources conflict — [Avaada](https://www.avaada.com/?p=29666); [disytax Ch.85](https://disytax.com/?p=17092). [CA-CONFIRM: the solar entry in the 5% schedule covers "solar power based devices", and whether a solar inverter or solar pump qualifies is disputed]
- Electric motors/generators (HSN 8501): remain 18%. Transformers, ordinary inverters and UPS (HSN 8504) are 18% — [ClearTax electronics](https://cleartax.in/s/gst-on-electronics); [Busy electrical](https://busy.in/gst-rates/electrical-items)
- Batteries (HSN 8507): uniform 18% now. Earlier, lithium-ion was 18% and other types (lead-acid) were 28% — [Tally FAQs](https://tallysolutions.com/gst/new-gst-rates-2025-key-changes-and-faqs); [Lakshmikumaran & Sridharan](https://lakshmisri.com/newsroom/news-briefings/impact-of-gst-rate-changes-on-the-electronics-sector)
- Fans (HSN 8414): sources conflict. Busy and ClearTax's fan page show 18%. Bajaj Finserv and ClearTax's electronics page say household fans moved from 18% to 5% — [ClearTax fans](https://cleartax.in/s/gst-on-fans); [Bajaj Finserv](https://bajajfinserv.in/gst-on-electronics). [CA-CONFIRM]
- Pumps (HSN 8413): no source states a clear rate for ordinary water pumps. Agricultural and hand pumps and sprinkler/drip equipment reportedly moved from 12% to 5%. Fuel/lubricant pumps moved from 28% to 18% — [TaxGuru](https://taxguru.in/?p=985076); [Busy transition](https://busy.in/gst/gst-2-0-transition-new-slab-rates.md). (unsourced: general power-driven pumps in 8413 are commonly billed at 18%, and centrifugal pumps used mainly for water at 5%; the app must let HSN plus rate be editable per item) [CA-CONFIRM]
- SAC 9987 covers maintenance, repair and installation (except construction). Most entries are 18%, with some concessional entries (5% on some job-work-type repairs). SAC 998717 (commercial/industrial machinery repair) is listed at 18% — [Busy SAC 9987](https://busy.in/sac-code-9987); [GimBooks](https://www.gimbooks.com/blog/maintenance-repair-installation-services-9987-hsn-code-gst-rate/amp/); [Busy 998717](https://busy.in/sac-code-998717/)
- Composite supply: for "supply-cum-installation of an AC machine", the principal supply is the AC. AMC with parts is composite unless the contract prices goods and services separately — [WB AAR 47/2018-19 Alok Bhanuka](https://gstcouncil.gov.in/sites/default/files/AAR/wb_47_2018-19_alok_bhanuka.pdf)
- Installation billed separately is a standalone 9987 service at 18%. Bundled installation may take the rate of the goods — [Busy installation HSN](https://busy.in/hsn/installation-hsn-code/)
- Circular 195/07/2023-GST (17 Jul 2023) says:
  - No GST is charged on a free warranty replacement of parts or a free repair, and the manufacturer need not reverse ITC.
  - When a distributor replaces from its own stock and the manufacturer replenishes, no reversal is needed.
  - If the customer is charged extra, GST applies to the extra amount.
  - If the distributor invoices the manufacturer for parts or service, that is a taxable supply and the manufacturer can take ITC.
  — [CBIC circular PDF](https://gstcouncil.gov.in/sites/default/files/2024-06/circular-cgst-195.pdf); [Taxmann](https://taxmann.com/post/blog/no-need-to-reverse-itc-by-manufacturer-in-respect-of-free-replacement-parts-or-repair-services)
- Circular 216/10/2024-GST (26 Jun 2024) extends this to replacement of entire goods. Extended warranty sold separately is a separate taxable supply — [VJM Global](https://www.vjmglobal.com/ja/blog/gst-liability-and-input-tax-credit-availability-in-cases-involving-warranty-extended-warranty-clarification-in-furtherance-to-circular)

### Inferences
- The app needs per-item HSN plus rate (editable, with a rate-effective-date history) and per-service SAC 9987xx at a default of 18%.
- Invoice modes:
  - (a) goods only;
  - (b) labour only (SAC, 18%);
  - (c) goods plus installation on separate lines (each line at its own rate, which is a mixed/independent supply only if priced separately and genuinely separable);
  - (d) a composite "supplied and installed" price (whole value at the goods' rate).
- Default to separate lines. Flag the case where a 5% good (a solar module) is bundled with 18% installation, because composite vs works-contract treatment is litigated for solar EPC. [CA-CONFIRM]
- AMC is a service at 18%. A comprehensive AMC that includes parts is a composite supply where the service is principal, so the whole AMC is 18%. Parts billed outside the AMC are taxed at their own rate. (inference)
- Warranty job flow:
  - The customer invoice is zero or a delivery challan (no GST).
  - Parts consumed come out of stock with no ITC reversal.
  - The claim invoice to the manufacturer (parts plus labour) is taxable: labour at 18% SAC 9987, parts at their HSN rate.
  - If the manufacturer replenishes parts free, no GST applies; record a stock-in with zero value or a memo.
- A dealer's own free replacement, made without manufacturer backing (goodwill), may not be covered. [CA-CONFIRM]

### Gaps
- The exact 8413/8414 rates and the solar inverter/solar pump entry need the official Notification 9/2025 schedule text.
- Whether reimbursement of the manufacturer's labour is a "service to the manufacturer" (it is per Circular 195 if invoiced) or a pure-agent recovery is not tested here.

## Profit and loss: COGS, parts per job, wages, trip expenses, warranty income

### Takeaway
To show true profit per sale and per job, the app needs costing at line level. Per sale: sale value minus landed cost of the unit (moving weighted average or FIFO), minus direct costs. Per job: labour income plus parts income minus parts cost at stock cost, minus allocated technician cost and trip costs. Warranty jobs book their income on accrual when the claim is raised, not when cash arrives.

### Cited Findings
- No authoritative source was found specifically on job costing for field service in Indian SMEs. The methods below are standard cost-accounting practice. (unsourced; general accounting principles; AS 2 Inventories requires FIFO or weighted average cost) [CA-CONFIRM]

### Inferences (implementation rules)
- **COGS per sale:** landed cost = purchase price excluding recoverable GST + freight in. Use weighted average per item (simpler) or FIFO. Track serialised units (inverters, ACs) at actual cost. Non-creditable GST (composition dealer, or a blocked credit) gets added to cost.
- **Parts per job:** issue from stock to the job card at stock cost. Do this for warranty parts too, so the cost shows against the claim.
- **Technician cost:**
  - salaried staff: monthly salary ÷ working hours → hourly rate × job hours (or split equally across jobs done that day);
  - piece-rate/contract technicians: actual payout per job.
- **Trip expenses:** tag travel, food and stay to a trip. One trip can hold N jobs; allocate by one of three methods: (a) equal split, (b) by job revenue, (c) by distance/time. Default to equal split, with an override. Expenses not tagged to a job or trip go to overheads (rent, electricity, shop staff, vehicle EMI/depreciation).
- **Warranty jobs:**
  - when the claim is raised: book accrued income as a receivable from the company;
  - if the claim is rejected: write the receivable off as a loss against that job;
  - "pending claim ageing" is a key report.
  - Cash-basis users would otherwise see a loss on warranty jobs.
- **Monthly P&L:**
  - sales income − COGS = goods gross margin;
  - service income (labour + AMC earned + warranty claims) − parts consumed − direct technician cost − job trip cost = service margin;
  - subtract overheads → net profit.
- **AMC income:** spread over the contract months (deferred revenue). GST is due at invoice/receipt, but the P&L recognises income monthly.

### Gaps
- No Indian dealer-specific guide was found. The allocation method is a product decision.

## Monthly reports a dealer needs

### Takeaway
Reports derived from the above (no source found; product inference).

### Cited Findings
- None. These come from the requirement list. (unsourced)

### Inferences
- Sales vs service income, with GST rate-wise and HSN/SAC summaries (feeds GSTR-1 Table 12 HSN summary — unsourced).
- Job profitability (per job and per job type).
- Technician-wise jobs, hours, parts used, expenses and margin.
- Pending warranty claims, aged by manufacturer (0-30/31-60/60+ days), plus rejected claims.
- AMC renewals due in the next 30/60 days, and AMC visits due.
- Customer dues (receivables ageing) and company/manufacturer dues (claims and schemes receivable), plus supplier payables.
- Stock of units (serial-wise) and parts: quantity, value, slow-moving items, and parts below reorder level.
- Trip/expense register, with untagged expenses flagged.

### Gaps
- None researched.

## Income tax: 44AD vs 44ADA, TDS 194C/194Q

### Takeaway
A trading-plus-repair business is a "business", so 44AD (8% of turnover, or 6% on digital receipts; limit ₹2 cr, or ₹3 cr if cash receipts are 5% or less) applies to the whole turnover. 44ADA covers only specified professions under 44AA(1) (legal, medical, engineering, architecture, technical consultancy, etc.), not repair technicians. From 1 Apr 2026 the Income-tax Act, 2025 renumbers TDS into section 393. 194C rates are 1% (individual/HUF) and 2% (others). 194Q is 0.1% on purchases above ₹50 lakh, by buyers whose turnover exceeds ₹10 cr.

### Cited Findings
- The Income-tax Act, 2025 applies from 1 Apr 2026. Section 393 consolidates TDS. 194C continues at 1%/2% under 393(1) — [Busy 194C/393](https://busy.in/tds/section-194c-contractors-subcontractors/); [StudyCafe chart](https://studycafe.in/tds-rate-chart-for-fy-2026-2027-under-income-tax-act-2025-420727.html)
- 194Q is mapped to 393(1) Table Sl. 8(ii) at 0.1%, on purchases above ₹50 lakh per FY. The deductor is a buyer whose previous-year turnover exceeds ₹10 cr — [StudyCafe](https://studycafe.in/tds-rate-chart-for-fy-2026-2027-under-income-tax-act-2025-420727.html); [GimBooks chart](https://www.gimbooks.com/blog/tds-tcs-rate-chart-for-fy-2026-27/amp/)
- If the payee has no PAN, TDS is deducted at a higher rate — [Busy 194C](https://busy.in/tds/section-194c-contractors-subcontractors/)
- (unsourced) 194C thresholds: ₹30,000 single / ₹1,00,000 aggregate (raised from ₹75k in Budget 2024). An individual/HUF deducts under 194C only if they were under tax audit in the previous year. 44AD details as stated above (limits raised in Budget 2023). The equivalent provisions in the 2025 Act are presumed, not confirmed. [CA-CONFIRM]

### Inferences
- Most small dealers are below ₹10 cr turnover, so 194Q does not apply to them as buyers. Large distributors selling to them may deduct 0.1% (or TCS 206C(1H), which was reportedly omitted from 1 Apr 2025 — unsourced [CA-CONFIRM]). The app should capture TDS deducted by customers or manufacturers on claim payments (194C/194J may apply to service invoices raised to the company) as a receivable to reconcile with Form 26AS/AIS.
- The app should flag a technician payout once it crosses the 194C threshold, but only if the shop is subject to audit.
- Under 44AD, books are not mandatory, but GST still requires them. Show "presumptive income at 6/8%" alongside actual profit.

### Gaps
- No primary text from the Income-tax Act, 2025 was fetched to confirm the renumbered 44AD/44ADA and the 194C thresholds.
- I did not confirm whether any 2026 Budget changed the thresholds.
