# Village dairy milk collection centre operations (India, Bihar/Sudha focus, 2026)

Note: This was a light pass (6 searches, no page fetches). Claims below come from search snippets and should be checked before anyone relies on them in product rules.

## Daily flow (shifts, measuring, testing, slip)

### Takeaway
Members bring milk to the village Dairy Cooperative Society (DCS) twice a day, morning and evening, at fixed times. Each lot is measured and sampled for fat (and SNF) on the spot, and the farmer gets a printed slip. Bihar's COMFED runs this through about 32k village DCSs.

### Cited Findings
- Members bring milk at a set time morning and evening. It is measured at once and a sample is taken to test fat for payment — [Amul, Cow to consumer](https://Amul.com/files/pdf/6-10-15-Cow-to-consumer.pdf)
- Bihar: 32,244 village DCSs procure milk from farmer members. Milk is collected from the village societies twice a day and carried to district milk union plants — [Govt of Bihar COMFED page](https://betastate.bihar.gov.in/Comfed)
- COMFED's daily procurement is reported as about 30 lakh litres, with a target of 45 lakh. Figures differ between sources and dates — [Patna Press](https://patnapress.com/comfed-milk-collection-processing-growth-20-years/); [Govt of Bihar](https://betastate.bihar.gov.in/Comfed)
- An AMCU/DPMCU identifies the farmer by RFID, smart card or barcode. It weighs the milk, analyses it, works out the bill and prints the slip on a thermal printer — [Prompt DPMCU](https://www.promptdairytech.com/dpmcu-milk-collection-system/)

### Inferences
- The app needs these fields per entry: date, shift (M/E), farmer ID, milk type (cow/buffalo/mixed), quantity (L or kg), fat, SNF/CLR, rate, amount.
- Cow and buffalo milk normally have separate rate charts, but I did not confirm this from a source.

### Gaps
- No source on Bihar shift timings, or on whether cow and buffalo milk are kept separate at Bihar DCSs.
- The 1 L ≈ 1.03 kg conversion was not checked against a source.

## Quality tests and pricing formula

### Takeaway
Sources commonly give SNF as CLR/4 + 0.21×fat + 0.36, but they do not agree on the constants. Price is set either from a two-axis fat×SNF rate chart or as fat kg × fat rate plus SNF kg × SNF rate.

### Cited Findings
- SNF% = CLR/4 + 0.21×Fat + 0.36 (CLR at 27°C) — [Terra Insight](https://www.terra-insight.com/insights/dairy-reconciliation-fat-snf-milk-procurement-india)
- A different formula: SNF = CLR/4 + 0.25F + 0.6 — [IIT Kanpur virtual lab](https://dt-iitk.vlabs.ac.in/exp/snf-content-milk/procedure.html)
- Two-axis pricing puts fat on one axis and SNF on the other. A worked example values fat at Rs 370/kg and SNF at Rs 247/kg — [agriculture.institute](https://agriculture.institute/milk-production-and-quality/how-milk-is-priced-dairy-payment); [Neupane slides](https://www.slideshare.net/slideshow/unit-6ddpdr-sushil-neupanepdf/259408690)
- Dairy ERPs store rate charts as fat/SNF matrices that carry an effective date — [Ecosire ERPNext dairy](https://ecosire.com/ja/apps/erpnext/erpnext-dairy-management)

### Inferences
- Make the SNF formula constants configurable for each dairy or union, and version rate charts by effective date.

### Gaps
- I could not find COMFED's actual rate chart, Rs/kg fat values, or its rules for rejecting sour milk (for example the COB or alcohol test).

## Equipment (AMCU, milk analyser, BMC) and connectivity

### Takeaway
A DPMCU/AMCU combines a data processor with printer, an electronic weighing scale, a milk analyser and a display. Newer units upload to the vendor's cloud over 4G and send SMS. Bihar is now rolling out DPMCUs tied to direct bank payment.

### Cited Findings
- Components: a data processor with built-in printer, a milk analyser, an electronic hanging scale and a digital display — [NDDB technological interventions PDF](https://www.dairyknowledge.in/sites/default/files/technologicalinterventionsinstrengtheningdcsnetwork-webversion.pdf)
- Data is stored on a micro SD card and sent to Prompt Cloud. The unit has a 4G GSM modem and can send SMS alerts — [Prompt DPMCU](https://www.promptdairytech.com/dpmcu-milk-collection-system/)
- NDDB describes an SMS query-response system for farmers — [NDDB PDF](https://www.dairyknowledge.in/sites/default/files/technologicalinterventionsinstrengtheningdcsnetwork-webversion.pdf)
- An AMCU station analyses 120–150 samples per hour — [Vikaspedia AMCU](https://en.vikaspedia.in/viewcontent/agriculture/livestock/model-bankable-projects/automatic-milk-collection-units)
- Bihar is introducing DPMCUs. A federation portal gives farmers a virtual passbook showing quantity, quality, value, cattle feed and cash payments — [Govt of Bihar / search summary](https://betastate.bihar.gov.in/Comfed); [Patna Press](https://patnapress.com/?p=56553)

### Inferences
- Many centres will already have a DPMCU that feeds the dairy's cloud, so an app should import or sync its data rather than ask for the same entries again. Small private centres and middlemen often have no AMCU, and for them manual entry is the main use case.

### Gaps
- No public API or export format found for Prompt, Akashganga or Everest units.
- No details found on bulk milk coolers.

## Handover to the dairy, re-testing and margin

### Takeaway
I found no Bihar-specific source on the society's margin or on how shortage and fat loss are settled. Examples from Tamil Nadu suggest societies keep about Rs 1–2/L for running costs.

### Cited Findings
- Aavin (Tamil Nadu) deducts Rs 1.75/L for society administrative costs and holds back Rs 1/L to pay later — [National Herald](https://www.nationalheraldindia.com/national/tiruchy-dairy-farmers-flag-deductions-from-revised-milk-payments)
- In Madurai, cooperatives that run BMCs reportedly keep Rs 1–2/L — [DairyNews7x7](https://dairynews7x7.com/news/madurai-large-cooperatives-deduct-commission-incentives-say-dairy-farmers)
- In Bihar, DCSs buy only about a third of marketed milk. Most of the rest goes through private dairies and middlemen — [ICAR study](https://epubs.icar.org.in/index.php/IJAnS/article/view/24317)

### Inferences
- The app should reconcile three things for each shift or dispatch: what the centre bought (kg, fat, SNF), what the dairy accepted (kg, fat, SNF after re-test), and the gap between them in kg, fat kg and Rs.

### Gaps
- No source on COMFED's commission per litre, its tanker schedule, or how it handles shortages.

## Payment cycles and deductions

### Takeaway
The standard Amul-model cycle is every 10 days. Bihar now pays farmers straight into their bank accounts (DBT). Cattle feed and cash advances are deducted from the farmer's passbook.

### Cited Findings
- The society receives payment once every 10 days, based on fat and quality — [Amul](https://Amul.com/files/pdf/6-10-15-Cow-to-consumer.pdf)
- Bihar's unions pay directly into bank accounts — [DairyNews Today](https://dairynews.today/news/bihar_s_dairy_cooperatives_perform_strongly_on_payments_and_development_5418849.html)
- The Bihar passbook shows cattle feed and cash payment entries — [Patna Press](https://patnapress.com/?p=56553)

### Gaps
- Not confirmed: whether the farmer cycle in Bihar is 10 days or something else, and how mineral mixture and loans are recovered.

## Volumes, disputes and fraud

### Takeaway
Rough averages run to hundreds of litres per society per day. The common frauds are under-reporting fat by 0.1–0.5%, rigged scales or lactometers, dummy farmer accounts, adulteration with salt or sugar to pass SNF, and diverting milk from tankers.

### Cited Findings
- Amul: 13,141 VDCSs and about 10.16 M litres/day. That works out to roughly 770 L per society, which is my own division — [Amul AMCS PDF](https://www.amuldairy.com/assets/img/pdf/AMCS_June_2021.pdf)
- Fat cut by 0.1–0.5% at the centre, and bills raised against dummy accounts (commercial source) — [Bharathi Dairy Farm](https://www.bharathidairyfarm.com/common-malpractices-by-milk-collection-agencies.php)
- Salt and sugar added to raise SNF, with help from centre staff (Telangana) — [Telangana Today](https://telanganatoday.com/telangana-vijaya-dairy-milk-in-some-centres-found-to-be-adulterated)
- Faulty lactometers used by agents of private dairies — [Deccan Chronicle](https://deccanchronicle.com/141117/nation-crime/article/milk-adulteration-rampant-karimnagar-district)
- Fat readings inflated through collusion with traders (Gujarat, six arrests) — [DairyNews Today](https://dairynews.today/news/crave_creamery_suffered_a_5_3m_milk_fat_fraud_loss_1094183.html)
- Milk diverted from tankers fitted with a water compartment (Nandini, Karnataka) — [Deccan Herald](https://www.deccanherald.com/india/karnataka/involvement-of-insiders-suspected-in-milk-adulteration-scam-994897.html)

### Gaps
- No Bihar figures for farmers per DCS, litres per DCS, or the economics of independent middlemen (dudhiya).
