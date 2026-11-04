# Milk procurement pricing in India (rate charts), focus Sudha/COMFED Bihar, as of Nov 2026

## 1. Pricing methods an app must implement (two-axis, fat-only, fixed rate, TS)

### Takeaway
Organised Indian dairies mostly use two-axis pricing: a rate per kg of fat plus a rate per kg of SNF, with SNF usually about 2/3 of the fat rate. The app should also support fat-only, TS (fat+SNF at one rate), fixed rate per litre, and a lookup rate chart (fat × SNF grid giving rate per litre). The grid is the easiest way to cover whatever the union sends.

### Cited Findings
- Two-axis formula: Price = (Fat%/100 × kg milk) × fat rate + (SNF%/100 × kg milk) × SNF rate. SNF is valued at about 2/3 of the fat rate. Cow milk ends up paid about 78% of buffalo. A lean-season premium of up to 30% is mentioned — [agriculture.institute](https://agriculture.institute/milk-production-and-quality/how-milk-is-priced-dairy-payment)
- Fat-only pricing pays fat only, so cow milk is undervalued. TS pricing pays fat and SNF at one rate, which undervalues fat and encourages skimming. Fixed rate by species ignores quality and invites passing cow milk off as buffalo — [agriculture.institute](https://agriculture.institute/milk-production-and-quality/how-milk-is-priced-dairy-payment)
- Two-axis is the standard in India's organised (cooperative and large private) procurement. Milk below the minimum is penalised or rejected — [terra-insight](https://www.terra-insight.com/insights/dairy-reconciliation-fat-snf-milk-procurement-india)
- Amul unions quote rates "per kg fat" (for example Rs 855 to Rs 865/kg fat for buffalo from 1 Jun 2025). Cow milk is quoted per kg "fat-equivalent" (one report: Rs 384.10 to Rs 388.65) — [dairynews7x7](https://dairynews7x7.com/news/amul-boosts-milk-price-cuts-feed-cost-to-aid-farmers); [agrimoon](https://agrimoon.com/?p=11390)

### Worked examples (arithmetic is mine; the rates are illustrative, not any dairy's current rates)
- Two-axis, fat Rs 600/kg and SNF Rs 400/kg, 10 kg milk:
  - Cow milk at 4.0% fat / 8.5% SNF: 0.40×600 + 0.85×400 = Rs 580 (Rs 58/kg)
  - Buffalo milk at 6.5% fat / 9.0% SNF: 390 + 360 = Rs 750 (Rs 75/kg)
- Fat-only at Rs 865/kg fat (the Amul buffalo rate as an example): 10 kg at 6.5% fat = 0.65 kg fat × 865 = Rs 562.25
- Litres to kg: kg = litres × density. The density is usually derived from CLR, about 1.028–1.032 (often 1.03). This is from my own background knowledge, so confirm which basis the union uses (some pay per litre, others per kg).
- Common lactometer formula (Richmond-style): SNF ≈ CLR/4 + 0.2×Fat + 0.14 (some charts use +0.36 or +0.66). Also background knowledge: the app should let the constant be configured.

### Inferences
- Model the union rate as a configurable plan, one of: {grid(fat,SNF)->Rs/L, two-axis(fatRate, snfRate, basis=kg|L), fat-only, TS, flat}. Add a per-species override, a minimum-standard rule (reject or deduct), and bonus lines.
- Round fat/SNF to 0.1 for grid lookup, as charts are tabulated in 0.1 steps.

### Gaps
- No official Sudha/COMFED formula document was found. It is unclear whether COMFED uses a grid or a two-axis kg-fat/kg-SNF rate per union.

## 2. Current Sudha/COMFED procurement prices and revisions 2024–2026; Amul comparison

### Takeaway
In May 2026 Sudha raised farmer purchase rates by up to about Rs 3.30/L: the lowest-quality milk went from Rs 33 to Rs 34.75/L, and the top grade ("SNF 10") from Rs 77.70 to Rs 81/L. A Nov 2026 DBT payout averaged Rs 46.71/L. I found no official per-kg-fat figure for COMFED.

### Cited Findings
- Sudha procurement hike reported 28 May 2026: the lowest-fat milk went from Rs 33 to Rs 34.75/L, and the top grade (SNF 10) from Rs 77.70 to Rs 81/L. The effective date was not stated ("soon"), and cow/buffalo were not split — [Rashtriya Ujala](https://www.rashtriyaujala.com/2026/05/28/big-relief-for-cattle-farmers-in-bihar-sudha-dairy-hikes-milk-procurement-rate-by-over-%E2%82%B93/)
- Nov 2026: Rs 68.51 crore was paid by DBT directly into the bank accounts of 106,063 producers in 5,832 societies, for 14,666,061 L of milk. That is an average of about Rs 46.71/L. The same item reports the launch of a Patna ICCC for tanker tracking and the Sudha Sampark helpline — [eDairyNews](https://in.edairynews.com/bihar-transfers-%E2%82%B968-51-crore-directly-to-106063-dairy-farmers-and-launches-digital-control-centre-for-sudha-milk/)
- The COMFED chairman says farmer prices rise with consumer prices and COMFED keeps a small margin. Procurement is about 3 lakh L/day higher than a year earlier, and about 3,000 new societies were formed in three months — [AgriMoon](https://agrimoon.com/bihar-targets-dairy-expansion-with-10000-cooperatives-higher-procurement-and-digital-milk-tracking/)
- Retail (not farmer) hikes: Rs 2–3/L from 22 May 2025 (Gold Rs 65 to 67, Shakti Rs 57 to 59) — [Patna Press](https://patnapress.com/?p=24275); a further Rs 2/L hike in May 2026 — [ETV Bharat](https://www.etvbharat.com/hi/state/sudha-dairy-milk-two-rupees-price-hike-in-bihar-comfed-brs26052106483). Reported Gold prices conflict (Rs 67 vs Rs 70) — [Times Now Hindi](https://www.timesnowhindi.com/cities/bihar-sudha-dairy-increases-milk-prices-in-bihar-by-rs-2-per-litre-sudha-doodh-ka-naya-rate-article-154369437)
- 2023: a retail hike was tied to raising the retailer margin from Rs 1.75 to Rs 1.95/L — [dairynews7x7](https://dairynews7x7.com/sudha-milk-to-get-dearer-by-rs-3-per-litre-from-april-24-in-bihar)
- COMFED reports 32,244 village DCS and 14.85 lakh members — [Bihar govt COMFED page](https://betastate.bihar.gov.in/Comfed)
- Amul comparison: buffalo milk Rs 855 to Rs 865/kg fat from 1 Jun 2025; the 2024-25 final price (with year-end bonus) was Rs 1,028/kg fat — [dairynews7x7](https://dairynews7x7.com/news/amul-boosts-milk-price-cuts-feed-cost-to-aid-farmers). In May 2026 GCMMF unions raised prices by Rs 30/kg fat (+3.7%) — [Angel One](https://www.angelone.in/news/economy/amul-and-mother-dairy-increase-milk-prices-by-2-per-litre-effective-may-14-2026). Another report gives Rs 865 to Rs 875 and a final price of Rs 1,035; its year is unclear — [agrimoon](https://agrimoon.com/?p=11390)
- Punjab Milkfed raised its procurement price by Rs 20/kg fat — [Tribune](https://www.tribuneindia.com/news/punjab/milk-procurement-price-hiked-by-rs-20-per-kg-fat-396892)

### Inferences
- A Rs 33–81/L span implies Sudha uses a quality grid (fat/SNF), not a flat rate. The roughly Rs 47/L average is consistent with mostly cow milk plus mixed milk.
- Rates change about once or twice a year, typically around Apr–Jun alongside retail hikes. The app needs dated rate-chart versions (effective-from).

### Gaps
- No official COMFED rate chart, per-kg-fat value, cow/buffalo split, or per-union differences (Patna/Vaishali/Barauni etc.) were found. The figure "Rs 50–60/L" on a blog is unreliable and was not used.
- The Rs 30/kg fat = 3.7% base (about Rs 811) conflicts with the Rs 865–875 Amul figures, probably because of different unions or bases.

## 3. Rate chart grids, frequency, minimum standards and penalties

### Takeaway
Unions issue fat × SNF charts in 0.1 steps (Rs/L or Rs/kg). The FSSAI minimum for cow milk is 3.2% fat / 8.3% SNF. For buffalo milk in Bihar and similar states, sources give 6.0/9.0 under the older rules and 5.0/9.0 after a 2021 revision. Milk below the minimum is deducted for or rejected, with rules set per union.

### Cited Findings
- Cow milk minimum 3.2% fat / 8.3% SNF (some sources say 3.5/8.5, which looks like the older or state-specific figure) — [dalvoy](https://www.dalvoy.com/en/upsc/mains/previous-years/2020/ani-husb-veter-science-paper-ii/fssai-milk-standards); [foodsafetymantra](https://foodsafetymantra.com/regulatory-insight/consumer-products/milk-cream/fssai-categorization-of-milk-products-and-their-standards)
- Buffalo milk in Bihar (and Assam, Delhi, Gujarat, UP etc.) was historically 6.0% fat / 9.0% SNF; it was revised to 5.0/9.0 — [dairynews7x7](https://dairynews7x7.com/news/buffalo-milk-standards-revised-to-5-fat-and-9-snf-by-fssai). Whether the revision is in force is uncertain.
- Below-standard milk is penalised or rejected — [terra-insight](https://www.terra-insight.com/insights/dairy-reconciliation-fat-snf-milk-procurement-india)

### Inferences
- FSSAI standards govern milk sold as "cow/buffalo milk". Procurement charts often still buy lower-grade milk at a low rate (Sudha's Rs 34.75 floor), so the app needs a "below-standard" rule per plan: reduced rate, deduction, or reject.

### Gaps
- No Sudha grid was obtained, and no specific penalty schedule was found.

## 4. Bonuses, incentives, seasonal differences

### Takeaway
Cooperatives pay year-end bonuses or "final price" top-ups (Amul: final price Rs 1,028/kg fat against a running rate of about Rs 855). COMFED says it pays bonuses and incentives, but no per-litre figure for a Bihar state incentive was found. Lean-season premiums of up to about 30% are described in general literature.

### Cited Findings
- COMFED provides producers with remunerative prices plus bonus, incentives, veterinary services and subsidised feed — [Bihar govt COMFED page](https://betastate.bihar.gov.in/Comfed)
- Amul year-end final price Rs 1,028/kg fat (2024-25) — [dairynews7x7](https://dairynews7x7.com/news/amul-boosts-milk-price-cuts-feed-cost-to-aid-farmers)
- Lean-season premium up to 30% — [agriculture.institute](https://agriculture.institute/milk-production-and-quality/how-milk-is-priced-dairy-payment)
- Other states' incentives for comparison: Maharashtra raised procurement by Rs 3/L — [restaurantindia](https://www.restaurantindia.in/news/maharashtra-govt-increases-milk-procurement-prices-by-rs-3-per-litre-retail-rates-to-remain-same.n14592); the Himachal Doodh Protsahan Yojana — [dairynews7x7](https://dairynews7x7.com/news/himachal-cm-launches-doodh-protsahan-yojana-freight-subsidy)

### Inferences
- The app needs: per-litre or per-kg-fat incentive lines, a year-end bonus (often a share of the society's profit, distributed in proportion to the year's milk supplied), and seasonal chart versions.

### Gaps
- I found no evidence of a Bihar per-litre state milk incentive (like Karnataka's Rs 5/L) for COMFED members. Treat it as unconfirmed or nonexistent.

## 5. Centre/society commission and how the dairy pays

### Takeaway
The union pays the society the chart value plus a society margin or commission. Published examples are Rs 1–2/L (Karnataka) and Rs 2/L for staff and office costs (Ernakulam, Kerala). Payment is by bank on a 7–10 day cycle. In Bihar, COMFED now pays farmers directly by DBT. No COMFED commission figure was found.

### Cited Findings
- Kolar union pays societies about Rs 1–2/L extra for maintenance; rates vary by union — [dairynews7x7](https://dairynews7x7.com/news/consumers-pay-more-but-dairy-farmers-get-lesser-in-karnataka)
- ERCMPU pays Rs 7/L extra: Rs 5 to farmers and Rs 2 for society salaries and office — [dairynews7x7](https://dairynews7x7.com/news/ercmpu-to-pay-%e2%82%b97-extra-to-societies-farmers-to-get-%e2%82%b95-more-a-litre)
- Society/union/federation structure and functions — [dairyknowledge.in](https://www.dairyknowledge.in/node/184)
- Bihar DBT to individual farmers' accounts, replacing cash payouts — [eDairyNews](https://in.edairynews.com/bihar-transfers-%E2%82%B968-51-crore-directly-to-106063-dairy-farmers-and-launches-digital-control-centre-for-sudha-milk/)

### Inferences
- Under DBT, a Bihar society or centre's own income is the commission plus any margin, not the gap between what it collects and what it pays. The app should reconcile the union statement (litres, fat, SNF, amount, commission) against its own farmer-wise records, and flag shortfalls in quantity or fat.
- Private collection agents (non-cooperative) typically earn a per-litre margin. These figures are unsourced.

### Gaps
- No COMFED commission per litre and no payment cycle (10-day bill vs weekly) were found.

## 6. Taxes and FSSAI for centres

### Takeaway
Raw and fresh milk carries nil GST. Commission on agricultural produce is likely GST-exempt (verify). Agents' commission may attract TDS under 194H (2%, threshold Rs 15k/20k), but cooperative-specific treatment is unclear. A farmer who is a cooperative member is exempt from FSSAI. Collection centres need registration (up to 500 LPD) or a state licence (501–50,000 LPD).

### Cited Findings
- Fresh/raw milk is nil-rated or exempt; UHT milk is 5% — [taxguru](https://taxguru.in/goods-and-service-tax/gst-raw-milk-exemption-dispute-asmt-10-drc-07.html); [Axis Bank](https://www.axis.bank.in/blogs/taxation/gst-on-milk)
- 194H commission TDS is 2% from 1 Oct 2024, with a threshold of Rs 15,000 (individual/HUF) or Rs 20,000 — [TaxBuddy](https://www.taxbuddy.com/blog/is-gst-applicable-on-commission-under-194h)
- 194Q does not apply when the seller is exempt from income tax (CBDT clarification, quoted secondhand) — [taxtmi forum](https://www.taxtmi.com/forum/issue?id=118733)
- Milk processing paid to dairies falls under 194C at 2% (ITAT) — [studycafe](https://studycafe.in/file/tds-under-section-194c-applicable-on-the-processing-of-milk-itat)
- FSSAI: up to 500 LPD needs basic registration; 501–50,000 LPD needs a state licence. Milk producers who are registered DCS members and supply all their milk to the society are exempt. Village collection centres working under a licensed chiller or plant are a separate category subject to state norms — [taxguru](https://taxguru.in/corporate-law/fssai-registration-process-dairy-units-including-milk-chilling-units.html); [FSSAI](https://fssai.gov.in/upload/media/FSSAI_News_License_IndianTalk_07_05_2019.pdf)

### Inferences
- Farmers' dairy income is generally treated as business income, not agricultural income. From background knowledge, this means taxable if above the threshold, so mark it uncertain. Cooperative societies have an 80P deduction for marketing members' milk (background knowledge, verify).
- The app should show GST as nil on milk and keep commission as a separate line so TDS can be computed.

### Gaps
- No primary source was found on whether a union deducts 194H TDS from society commission, or on 194Q for payments to farmers. A CA should confirm.
