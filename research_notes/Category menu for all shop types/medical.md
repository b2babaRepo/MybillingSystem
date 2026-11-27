# Category menu for an Indian medical shop (chemist): apps, POS software, regulation

Method note: this session's network blocked direct page fetches of 1mg.com, pharmeasy.in, margcompusoft.com and gofrugal.com, so none of those pages were opened. Everything below comes from web-search result snippets of the vendors' own pages (primary, but seen only as snippets and sometimes cached or dated) and from third-party pages, which are marked **[secondary]**. Search results were taken in November 2026. Several of the cached category pages are years old, so a live check is needed before using any exact label.

## 1. How Tata 1mg, PharmEasy, Apollo 24/7, Netmeds, Truemeds and MedPlus organise their category screens

### Takeaway
All the large Indian e-pharmacies split the catalogue into two trees:
- **Medicines** (Rx, found by search or prescription upload, not browsed).
- **Healthcare/wellness products** (OTC and FMCG, browsed by category), with a second entry point "by health concern/condition" (diabetes, heart, women's health, and so on).

The wellness tree is about 10–15 top-level departments with 3–8 sub-categories each. The departments repeat across apps: Vitamins & Nutrition, Personal/Skin/Hair Care, Mom & Baby, Sexual Wellness, Ayurveda/AYUSH and Homeopathy, Fitness, Diabetes, Health Devices, Surgical, Elderly Care.

### Cited Findings
**Tata 1mg**
- 1mg publishes a full "category-tree" page. Top-level headings in it include Vitamins & Nutrition, Hair Care, Fitness & Health, Sexual Wellness and Skin Care. — [1mg category tree](https://www.1mg.com/category-tree)
- Vitamins sub-categories in the tree include Omega & Fish Oil & DHA, Calcium, Vitamin B, Vitamin C and Vitamin D. — [1mg category tree](https://www.1mg.com/category-tree)
- Skin Care sub-categories include face wash, sunscreen, moisturizers and serums. — [1mg category tree](https://www.1mg.com/category-tree)
- Fitness & Health includes Plant Protein, Mass Gainers, Pre/Post Workout, Smart Watches & Rings and Fat Burners. — [1mg category tree](https://www.1mg.com/category-tree)
- Sexual Wellness includes Lubricants & Massage Gels, Sexual Wellness Devices, Performance Enhancers and Oral Contraceptives. — [1mg category tree](https://www.1mg.com/category-tree)
- The tree has "Nutritional Drinks" and "Healthy Snacks" headings. — [1mg category tree](https://www.1mg.com/category-tree)
- Homeopathy is a large department. Its sub-categories include Homeopathy Medicines, Sexual Health, Hair Care, Homeopathy Diabetes Medicines and Women's Health. Product types within it include Mother Tinctures, Dilutions and Triturations. Listings can be filtered by brand, for example `?brand=SBL`. — [1mg Homeopathy](https://www.1mg.com/categories/homeopathy-57?filter=true&pageNumber=2); [1mg Homeopathy medicines](https://www.1mg.com/categories/homeopathy/homeopathy-medicines-106?filter=true&amp;brand=SBL&amp;pageNumber=145); [1mg Homeopathy women's health](https://www.1mg.com/categories/homeopathy/women-s-health-257)
- Diabetes is a department (a concern-based hub). Its children include Diabetic Medicines, which in turn has Ayurvedic Medicines and Vitamins, Minerals & Antioxidants, plus Supplements and Healthcare Devices such as Blood Glucose Monitors and Test Strips & Lancets. The pages are about 5 years old. — [1mg diabetic medicines / ayurvedic](https://www.1mg.com/categories/diabetes/diabetic-medicines/ayurvedic-medicines-581); [1mg diabetes supplements](https://www.1mg.com/categories/diabetes/supplements-3); [1mg diabetic vitamins](https://www.1mg.com/categories/diabetes/diabetic-medicines/vitamins-minerals-antioxidants-579)
- Ayurveda is a department with "Top picks – Ayurveda", Ayurveda Personal Care, Ayurveda Skin Care and Diabetes Care (Ayurveda). — [1mg Ayurveda](https://www.1mg.com/categories/ayurveda-products-45); [1mg Ayurveda skin care](https://www.1mg.com/categories/ayurveda/ayurveda-skin-care-1504); [1mg Ayurveda diabetes care](https://www.1mg.com/categories/ayurveda/diabetes-care-46)
- The same item appears under several parents. For example, Diabetes appears inside "Vitamins & Nutrition > Top picks", and Diabetes Care appears inside Ayurveda. This means the tree is a poly-hierarchy (multi-parent), not a strict tree. — [1mg vitamins > top picks > diabetes](https://www.1mg.com/categories/vitamins-nutrition/top-picks-vitamins-nutrition/diabetes-4342)
- The homepage shows "popular categories": Vitamins & Supplements, Skin Care and Ayurveda Products. It also shows health-concern shortcuts such as Diabetes. — [1mg home](https://www.1mg.com/)
- 1mg has a featured "Health products – 1 hour delivery" category, which is a delivery-speed collection rather than a product type. — [1mg 1-hour delivery](https://www.1mg.com/categories/featured/health-products-1-hour-delivery-2632)
- URL pattern: OTC products live under `/otc/…` and medicines under `/drugs/…`. This is a hard split between the OTC/wellness catalogue and the drug catalogue. There is also a Hindi mirror at `/hi/drugs/…`. — [1mg OTC product](https://www.1mg.com/otc/go-shudh-natural-salt-otc556735); [1mg Hindi drug page](https://www.1mg.com/hi/drugs/axn-dry-syrup-475704)
- **[secondary]** A coupon site lists 1mg categories including "Health Food and Drinks", Baby Care, Maternity Care and Elderly Care. — [shopickr](https://www.shopickr.com/coupon/flat-25-off-medicines-orders-rs-300)

**PharmEasy**
- Healthcare (non-medicine) products sit under `/health-care/`. Top-level sections seen there are Personal Care, Ayurvedic Care, Skin Care, Healthcare Devices, Health Food and Drinks, Mobility & Elderly Care and a "Health Condition" entry. — [PharmEasy personal care](https://pharmeasy.in/health-care/personal-care-877); [PharmEasy health & wellness](https://pharmeasy.in/health-care/1290); [PharmEasy ayurvedic > herbs](https://pharmeasy.in/health-care/ayurvedic-care-138/herbs-141)
- Ayurvedic Care has a "Herbs" sub-category. Personal Care has an "Others care" sub-category. — [PharmEasy herbs](https://pharmeasy.in/health-care/ayurvedic-care-138/herbs-141); [PharmEasy others care](https://pharmeasy.in/health-care/personal-care-44/others-care-35)
- Seasonal or occasion collections appear in some snapshots, such as Heart Care, Pollution Care and Winter Essentials. The labels vary between snapshots. — [search snapshot of PharmEasy pages](https://pharmeasy.in/health-care/1290)
- Generics have their own landing page, `/generics`. — [PharmEasy generics](https://pharmeasy.in/generics)

**Apollo Pharmacy / Apollo 24|7**
- Apollo has a "shop-by-category" hub and a category sitemap listing all categories. — [Apollo shop-by-category](https://www.apollopharmacy.in/shop-by-category); [Apollo category sitemap](https://www.apollopharmacy.in/static/shop-by-category-sitemap)
- Personal Care (about 9,873 items) has these sub-categories: Skin Care, Hair Care, Oral Care, Mens Grooming, Sexual Wellness and Adult Diapers. — [Apollo personal care](https://www.apollopharmacy.in/shop-by-category/personal-care-1)
- Baby Care (about 1,507 items) has Diapering, Baby Food and Baby Skin Care. — [Apollo baby care](https://www.apollopharmacy.in/shop-by-category/baby-care)
- Nutritional Drinks & Supplements has Adult Nutrition, Kids Nutrition, Rehydration Drinks and Sports Nutrition. — [Apollo shop-by-category](https://www.apollopharmacy.in/shop-by-category)
- Women Care covers sanitary napkins, menstrual cups, tampons and intimate washes. Its sub-categories include Women Supplements, Women's Multivitamins and Menstrual Care. — [Apollo women care](https://www.apollopharmacy.in/shop-by-category/women-care)
- Health Devices and Ayurveda are top-level sections. Their sub-lists were not seen. — [Apollo home](https://www.apollopharmacy.in/)
- There is a private-label department, "Apollo Life Products". This is a house-brand department. — [Apollo products](https://www.apollopharmacy.in/shop-by-category/apollo-products)
- Medicines are listed alphabetically (A–Z index) rather than in categories. — [Apollo medicine A–Z](https://www.apollopharmacy.in/medicine)

**Netmeds (Reliance)**
- The sitemap groups "Wellness Main Categories" as: Ayush · Fitness · Personal Care · Sexual Wellness · Mom & Baby · Diabetes Support · Devices · Surgical · Treatments · Eyewear · Veterinary. — [Netmeds sitemap](https://www.netmeds.com/page/site-map)
- Diabetes sub-categories: Diabetes Support, Sugar Substitutes, Diabetes Management Supplements, Diabetes Care Ayurveda, and Lancets & Test Strips. — [Netmeds categories](https://www.netmeds.com/categories)
- Fitness sits next to Weight Management, Sports Supplements and Vitamins and Supplements. Sexual Wellness includes condoms, massagers and lubricants. — [Netmeds all categories](https://www.netmeds.com/sections/all-categories)
- There is a separate "Order Medicine Online" section, again a medicines-versus-wellness split, and a Devices section for personal healthcare devices and supports. — [Netmeds order medicine](https://www.netmeds.com/sections/order-medicine-online); [Netmeds devices](https://www.netmeds.com/sections/devices)

**Truemeds**
- Truemeds is a generics-first model. Users either upload a prescription or search for medicines. A doctor or pharmacist calls to verify the prescription and suggest a substitute. — [Truemeds](https://www.truemeds.in); **[secondary]** [GrabOn](https://www.grabon.in/truemeds-coupons/)

**MedPlus**
- **[secondary]** MedPlus Mart offers Medicines, FMCG, Personal Care, Click & Pick and Rapid Delivery, and has over 5,000 stores. — [punjabkesari / listicle](https://english.punjabkesari.com/business/10-best-online-medicine-apps-in-india-for-safe-delivery)

### Inferences
- A suggested left rail for a chemist, synthesised from the overlap across apps:
  1. Medicines (Rx)
  2. OTC Medicines
  3. Vitamins & Nutrition
  4. Diabetes Care
  5. Health Devices
  6. Personal Care
  7. Skin & Hair
  8. Mom & Baby
  9. Women Care
  10. Sexual Wellness
  11. Ayurveda / AYUSH
  12. Homeopathy
  13. Surgicals & First Aid
  14. Elderly & Mobility
  15. Fitness / Sports Nutrition
  16. Veterinary (optional)
  17. Generics / House brand
- Health-concern tiles (Diabetes, Heart, Women's health, Stomach, Cold & cough) are best modelled as tags or collections that cross the tree. The apps put one product under several parents.
- Medicines are not browsed by category in the apps. Search, A–Z, prescription upload or reorder are the entry points. At the counter, the Medicines department should open straight into search.
- Surgicals and Veterinary appear only at Netmeds among the apps seen, yet both are common in physical chemist shops.

### Gaps
- No live view of the PharmEasy, Truemeds or MedPlus Mart category trees or their mobile left-rail layouts. The pages could not be fetched, and the search snippets did not carry them.
- The sub-category lists for Apollo Health Devices and Ayurveda, and for 1mg Mom & Baby and Elderly Care, were not seen in primary pages.
- No UX case study or teardown of 1mg or PharmEasy category screens was found.

## 2. How they search medicines (brand, salt, misspellings, Hindi) and show substitutes and "Rx required"

### Takeaway
Medicine detail pages show:
- a "Prescription Required" flag
- the salt composition
- a storage note
- a list of "All substitutes" with the same composition (brand, pack and manufacturer)

When an item is sold out, a same-composition alternative is offered. Generic substitution is allowed only when the prescription names the salt.

### Cited Findings
- 1mg drug pages carry a "Prescription Required" flag, a "SALT COMPOSITION" section, a storage line (for example "Store below 25°C") and an "All substitutes" section. When nothing matches, that section reads "No substitutes found". — [1mg Ranset 20mg](https://mars.1mg.com/drugs/ranset-20mg-capsule-484772); [1mg E Tel 40mg](https://1mg.com/drugs/e-tel-40mg-tablet-312145)
- Sold-out handling on 1mg: "Pantomit 40mg Injection is currently sold out", with a same-composition alternative (Pantosec IV Injection) shown. Substitute rows give the pack ("vial of 1 Injection") and the manufacturer ("by Cipla Ltd"). — [1mg Pantomit](https://www.1mg.com/drugs/pantomit-40mg-injection-513342); [1mg Papmac IV](https://www.1mg.com/drugs/papmac-iv-40mg-injection-999489)
- 1mg drug names include strength and form, for example "Cevy CV 200mg/125mg Tablet" and "Diclofin Plus 50mg/325mg Tablet". Hindi-language drug pages exist under `/hi/`. — [1mg Hindi Cevy CV](https://1mg.com/hi/drugs/cevy-cv-200mg-125mg-tablet-740068); [1mg Hindi Diclofin Plus](https://1mg.com/hi/drugs/diclofin-plus-50mg-325mg-tablet-830421)
- PharmEasy's substitute rule: you may buy a substitute only if the prescription lists salt names instead of a brand name. — [PharmEasy help](https://pharmeasy.in/help/general-enquiries-43000348790/can-i-buy-a-substitute-medicine-on-my-prescription--43000188944)
- PharmEasy's generics flow has three steps: search your regular medicine, compare the substitute options, then pick the option a doctor suggests. — [PharmEasy generics](https://pharmeasy.in/generics)
- Truemeds says its substitutes match the prescribed medicine on active ingredient (salt), strength and dosage form, and are CDSCO-approved. A recommendation algorithm suggests them. — [Truemeds Play listing](https://play.google.com/store/apps/details?id=com.intellihealth.truemeds&hl=en_IN); [Truemeds](https://www.truemeds.in/)
- **[secondary]** Organic search share for Indian pharmacy queries: Tata 1mg holds a 73.77% top-5 share. Hindi non-brand queries such as "खांसी की दवा" (cough medicine) carry real volume. — [Techmagnate report (PDF)](https://www.techmagnate.com/wp-content/uploads/2024/05/Online-Pharmacy-Trends-India-FY_24new.pdf)
- 1mg has written about mining medicine searches for health trends. — [1mg on Medium](https://medium.com/1mgofficial/how-we-used-medicine-searches-to-uncover-health-trends-d0144ac31e47)
- **[secondary, analogue]** Zepto, an Indian quick-commerce app, corrects phonetic and vernacular misspellings (for example "kothimbir" for coriander) with an LLM plus retrieval. It learns new misspellings from users' query reformulations, such as "banan chips" → "banana chips". — [Analytics Vidhya](https://www.analyticsvidhya.com/blog/2025/07/replicate-zeptos-multilingual-query-resolution-system/); [AIM](https://analyticsindiamag.com/ai-features/zeptos-spellchecker-fixes-errors-with-a-little-help-from-llms/)

### Inferences
- The search index should hold:
  - brand name
  - salt or composition, normalised so that "paracetamol" finds Crocin and Dolo
  - strength
  - form
  - manufacturer
  - phonetic or transliterated aliases (Hinglish, Devanagari)
- Results should group by salt + strength + form, so that substitutes are one tap away.
- Substitute rows should show brand, pack size, manufacturer, MRP and stock. When stock is 0, the app should offer a same-composition alternative, as 1mg does.
- The Rx badge belongs on the result row and the product page, not only at checkout.

### Gaps
- No primary 1mg or PharmEasy source on how their spelling correction or Hindi search works internally.
- No primary evidence on how each app badges Schedule H1 versus H, or whether they show the schedule letter. Only the "Prescription Required" flag was seen.

## 3. Filters and sorts that matter for a chemist

### Takeaway
The public apps filter category listings mainly by brand (1mg uses `?brand=` URL filters). The real chemist-grade attributes live in the drug master: salt, strength, form, pack, manufacturer, Rx flag, storage, batch and expiry. The billing software exposes these for picking.

### Cited Findings
- 1mg category listings support `filter=true`, `brand=` and `sort=relevance` parameters. — [1mg Homeopathy sort](https://www.1mg.com/categories/homeopathy-57?filter=true&sort=relevance&pageNumber=19)
- Drug pages show the attributes salt composition, strength and form (in the name), pack ("vial of 1 Injection"), manufacturer, Rx flag and storage temperature. — [1mg Ranset](https://mars.1mg.com/drugs/ranset-20mg-capsule-484772); [1mg Papmac IV](https://www.1mg.com/drugs/papmac-iv-40mg-injection-999489)
- Truemeds matches substitutes on salt + strength + dosage form. These are the three minimum keys for equivalence. — [Truemeds Play listing](https://play.google.com/store/apps/details?id=com.intellihealth.truemeds&hl=en_IN)
- Vyapar lets users filter inventory by expiry date, so near-expiry stock can be cleared first. It tracks batch number, manufacturing date and expiry date. — [Vyapar pharmacy inventory](https://vyaparapp.in/free/inventory-management-software/pharmacy)
- **[secondary]** A guide to Indian medical-store software says FEFO should be a software requirement: the software should prompt the dispenser to sell older stock first. — [Techimply](https://www.techimply.com/blog/medical-store-software)

### Inferences
Filters to offer:
- Salt/composition
- Strength
- Form: tablet, capsule, syrup, suspension, injection, drops, cream/ointment, inhaler, sachet, device
- Rx / OTC
- Schedule: H, H1, X, G, OTC, NDPS
- Manufacturer
- Pack size
- In stock
- Near-expiry (≤ 30/60/90 days)
- Cold chain (2–8 °C)
- Generic vs branded
- Rack

Sorts to offer:
- Relevance
- Price, MRP and price per unit
- Margin (counter only)
- Expiry (FEFO)

### Gaps
- No primary source found for public-app filters on form, strength or schedule. They may exist in-app but were not seen.
- No source on cold-chain badges in the Indian apps beyond the per-drug storage line.

## 4. How Indian pharmacy billing software helps the chemist find items at the counter

### Takeaway
Indian chemist POS tools centre on:
- multi-key search: name, salt/substitute, rack, barcode, item code
- a salt master and generic drug index
- rack, shelf and box location
- batch-wise stock with expiry alerts and short-expiry blocking

Category browsing is secondary.

### Cited Findings
- **Marg ERP:** search modes are Salt / Substitute / Rack / Name-wise, plus barcode and item code. — [Marg drug inventory page (search snippet)](https://margcompusoft.com/retail/drug-inventory-management-software.html)
- **Marg ERP:** a Salt Master holds salt compositions. Salt-wise Stocks/Search is enabled in the Control Room (set "Y"). Salts can be created, modified, deleted and given a status. — [Marg Care: create salt](https://care.margcompusoft.com/margerp/inventory-master/38694/1/how-to-create-modify-delete-salt-in-marg-software); [Marg Care: salt-wise view](https://care.margcompusoft.com/margerp/other-reports/37214/1/How-to-view-salt-wise); [Marg Care: salt status](https://care.margcompusoft.com/margerp/inventory-master/166482/1/How-to-change-salt-status)
- **Marg ERP:** rack numbers are enabled in the Control Room, and stock moves between racks via Masters > Inventory Masters > Rack No. Transfer. — [Marg Care: transfer stock](https://care.margcompusoft.com/margerp/inventory/1633/1/-How-to-transfer-Stock)
- **Marg ERP:** a batch-stock report sits at Stock > Batch Stock. Batch, Size, MRP and Ref fields can be switched on or off per item, and there is a help article on searching items by batch at billing. Only its title was seen. — [Marg Care: batch-wise](https://care.margcompusoft.com/Question.aspx/3911/1/How-to-view-batch-wise); [Marg Care: enable batch/size/MRP](https://care.margcompusoft.com/margerp/inventory-master/3318/1/How-to-enable-disable-Batch,Size,MRP,Ref-on); [Marg Care: search item with batch](https://care.margcompusoft.com/margerp/inventory/3660/1/How-to-search-item-with)
- **GoFrugal:**
  - rack, shelf and box number location identification
  - batch-wise expiry tracking with alerts
  - automatic blocking of short-expiry drugs at inward and at sale
  - batch-wise transaction history
  - returns to the right supplier by batch
  - a drug index by generic and by brand, with an integrated drug index book for generic substitutes (50,000+ drugs)
  - "shortcuts and quick searches" for checkout

  — [GoFrugal pharmacy](https://www.gofrugal.com/retail/pharmacy-software/); [GoFrugal medical store](https://gofrugal.com/retail/pharmacy-software/medical-store-billing.html); [GoFrugal healthcare POS](https://cdn.gofrugal.com/retail/pharmacy-software/healthcare-pos-billing.html); [GoFrugal chemist POS](https://gofrugal.com/retail/pharmacy-software/chemist-pos.html)
- **GoFrugal:** has a separate "Surgical shop POS" product page, so surgicals are treated as their own retail format. — [GoFrugal surgical POS](https://gofrugal.com/retail/pharmacy-software/surgical-pos-billing.html)
- **Vyapar:**
  - Rack Wise Management
  - category filters for adding medicines to a bill
  - search by name or category
  - near-expiry notifications
  - batch, mfg and expiry tracking
  - its blog recommends search "by drug name or chemical salt to find alternatives"

  These are marketing claims. — [Vyapar medical shop](https://vyaparapp.in/free/billing-software-for-retail-shop/medical-shop); [Vyapar pharmacy POS](https://vyaparapp.in/pos-software/pharmacy); [Vyapar blog](https://vyaparapp.in/blog/choose-right-pharmacy-billing-software/)
- **RetailGraph:** only a third-party mention as a multi-store pharma retail and distribution option. No official feature detail was found. — **[secondary]** [techbloat](https://www.techbloat.com/?p=1948047)
- **MedPlus internal POS:** no public information found.

### Inferences
At the counter:
- Default to a search box with a mode toggle (Name / Salt / Rack / Barcode). The category rail is a fallback.
- A result row should show rack location and a batch picker sorted by nearest expiry (FEFO).
- Expired batches should be blocked. Batches near expiry should carry a warning.
- Schedule H1 and X sales should trigger capture of the patient and prescriber details for the register (see section 5).

### Gaps
- MedPlus's internal POS, RetailGraph's feature set and the exact Marg billing-screen batch picker flow are not documented publicly in what was seen.
- No vendor page was opened directly. All vendor facts come from search snippets.

## 5. Regulatory constraints in India on showing Schedule H/H1/X drugs online and on labels

### Takeaway
Labels must show "Rx" and a red-boxed warning for Schedule H, and a stronger red-bordered warning for H1. H1 sales must be entered in a separate register (prescriber, patient, drug, quantity), kept for 3 years.

Online sale is still governed by the 2018 draft e-pharmacy rules, which have never been notified. That draft requires a prescription upload for H/H1, bars Schedule X, NDPS drugs and tranquillisers, and bars advertising. A 2025–2026 draft amends Rule 65 to require central sanction before retailers advertise H, H1 and X drugs.

### Cited Findings
- Schedule H labels carry "Rx" at the top left and a red-boxed warning: "Schedule H drug. Warning: Not to be sold by Retail without the prescription of a Registered Medical Practitioner". — [CDSCO cautionary labelling notice](https://www.cdsco.gov.in/opencms/resources/UploadCDSCOWeb/2018/UploadPublic_NoticesFiles/labellingofcautionary_289.doc); [Medindia](https://medindia.net/patientinfo/drugs-and-cosmetics-rules-schedule-h-schedule-h1-drugs.htm)
- Schedule H1 labels carry "Rx" in red at the top left and a red-bordered box: "Schedule H1 Drug – Warning: It is dangerous to take this preparation except in accordance with the medical advice. Not to be sold by retail without the prescription of a Registered Medical Practitioner." — [Medindia](https://medindia.net/patientinfo/drugs-and-cosmetics-rules-schedule-h-schedule-h1-drugs.htm); [Fifth Amendment Rules 2018](https://www.legitquest.com/act/drugs-and-cosmetics-fifth-amendment-rules-2018/9431)
- **[secondary, weak]** A "red vertical stripe" on Rx drug packs was mentioned only in a Slideshare deck. It was not confirmed in rule text. — [Slideshare](https://www.slideshare.net/slideshow/pharmaceutical-jurisprudence-bp505t-unit2-schedules-and-dmrpptx/261822061)
- H1 register (G.S.R. 588(E), 2013): each supply is recorded in a separate register at the time of supply. The entry holds the prescriber's name and address, the patient's name, the drug name and the quantity. Records are kept for 3 years and open to inspection. The NHSRC format notes say 2 years, which is likely an older version. — [Indian Kanoon rule text](https://future.indiankanoon.org/doc/196956471/); [NTEP](https://ntep.in/node/938); [NHSRC H1 register format](https://www.nhsrcindia.org/sites/default/files/2021-07/Attachment%20A%2C%20Schedule%20H1%20register%20and%20NDPS%20Forms.pdf); [CliniExperts](https://cliniexperts.com/fourth-amendment-in-drugs-cosmetics-rules-2013/)
- Draft e-pharmacy rules (Aug 2018, proposed Part VIB "Sale of Drugs by E-Pharmacy"):
  - the customer must upload a prescription from a registered medical practitioner for Schedule H/H1 drugs
  - e-pharmacies may not deal in NDPS drugs, tranquillisers or Schedule X drugs
  - advertising of drugs through the e-pharmacy is prohibited

  The rules have not been notified as of the latest sources. There were Madras High Court and Delhi High Court proceedings, and the Centre told the Delhi High Court in 2023 it needed more time. — [Mondaq](https://webiis08.mondaq.com/india/food-and-drugs-law/740234/draft-rules-for-e-pharmacy-under-the-drugs-and-cosmetic-rules-1945); [Scroll](https://scroll.in/latest/893030/health-ministry-releases-draft-rules-to-regulate-online-sale-of-medicines); [LSHTM](https://www.lshtm.ac.uk/media/102426); [Business Standard 2023](https://business-standard.com/article/current-affairs/rules-to-regulate-e-pharmacies-need-some-more-time-centre-to-delhi-hc-123031501084_1.html); [IJME 2025](https://ijme.in/wp-content/uploads/2025/05/Emerging-epharmacy_288.pdf)
- 2025–2026 draft amending Rule 65: retailers, wholesalers and distributors would need Central Government sanction before advertising Schedule H, H1 and X drugs. A law-firm note says this is not an absolute ban. The draft also proposes CCTV in medical stores. The dates conflict across sources: November 2025 (Medical Dialogues, which also covers Schedule G), and 17 Sep 2026 / G.S.R. 861(E) 28 Sep 2026. — [Medical Dialogues](https://medicaldialogues.in/news/industry/pharma/no-more-online-ads-for-prescription-drugs-govt-proposes-ban-on-schedule-g-h-h1-and-x-medicines-159368); [News On AIR](https://newsonair.gov.in/govt-proposes-amendments-to-drugs-rules1945-to-strengthen-oversight-of-schedule-h-h1-and-x-drugs/); [Moneylife](https://www.moneylife.in/article/cctv-at-medical-stores-proposed-as-govt-tightens-regulatory-watch-on-schedule-h-h1-and-x-drugs/81691.html); [Hans India](https://www.thehansindia.com/business/health-ministry-proposes-tighter-curbs-on-advertising-of-prescription-drugs-1130646); [Nishith Desai](https://www.nishithdesai.com/default.aspx?id=15644)
- The CDSCO Drugs Rules index is the authoritative text. — [CDSCO Drugs Rules](https://cdsco.gov.in/opencms/en/Acts-and-rules/Drugs-Rules/)

### Inferences
For the customer-facing online catalogue:
- Do not promote H, H1 or X items in banners, offers or "top picks" tiles. That risks breaching the advertising draft.
- Hide Schedule X and NDPS items from online ordering altogether.
- Make H and H1 items orderable only with a prescription upload, and show a visible "Rx" badge.

For the counter:
- Show the schedule letter on the item.
- Make H1 and X sales force capture of patient and prescriber details for the register, with 3-year retention.

### Gaps
- Whether the 2025–2026 Rule 65 draft has been finally notified, and its exact G.S.R. number and date. The sources conflict.
- The official current Schedule H1 drug list was not retrieved.
- No source found on whether apps may show category labels such as "Schedule H" publicly. Only the "Prescription Required" flag was observed.
