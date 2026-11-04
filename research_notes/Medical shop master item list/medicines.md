# Medical shop master item list: top-selling medicine brands at Indian retail chemists (2026)

Method note: only 2 web searches were run (market rankings and GST). Brand, manufacturer, salt and pack data below come from general knowledge of Indian chemist catalogues (1mg / PharmEasy / Netmeds style listings). They were **not** checked page by page. Treat every row as a seed to verify against the live catalogue or distributor before you load it into production. Schedules are the usual classification for the salt: "OTC" means non-prescription in practice (India has no formal OTC schedule yet). Where a row says "unknown", the field was not verified.

## Which brands sell most, and what is the market context?

### Takeaway
Augmentin (GSK) and Glycomet GP (USV) are India's biggest brands by annual value. Mounjaro (tirzepatide, Eli Lilly) became the No.1 brand by monthly value from October 2025, but it sells in much lower volume.

### Cited Findings
- FY25: Augmentin was the top brand at Rs 816 cr, then Glycomet GP at Rs 803 cr. Indian pharma market grew 8.4%, mostly from price increases. Source: [Business Standard](https://www.business-standard.com/industry/news/robust-chronic-performance-drives-8-4-growth-for-indian-pharma-mkt-in-fy25-125040801060_1.html)
- October 2025: Mounjaro led monthly sales at about Rs 100 cr, ahead of Augmentin at about Rs 80 cr. Volume was about 85k units for Mounjaro against about 5.8M for Augmentin. Sources: [Angel One](https://www.angelone.in/news/market-updates/eli-lilly-s-obesity-drug-mounjaro-tops-india-s-drug-sales-in-october); [India Briefing](https://india-briefing.com/news/eli-lilly-mounjaro-india-obesity-drug-market-2025-40740.html)
- December 2025: Mounjaro led the month at Rs 105 cr. On a moving annual total (MAT), Augmentin was at Rs 889 cr and Glycomet GP at Rs 871 cr. Source: [BioSpectrum / Pharmarack](https://biospectrumindia.com/news/16/27158/mounjaro-eli-lilly-leads-ipm-for-dec-2025-as-no-1-brand-with-sales-of-rs-105-cr-pharmarack-.html)

### Inferences
- The app's master list should put high-volume acute brands first: Dolo, Pan, Augmentin, Azithral, Montair. Expensive specialty items like Mounjaro sell in low volume.

### Gaps
- I did not get IQVIA or AWACS top-300 brand lists. They are paywalled. The full 2026 ranking was not found.

## What GST rate and HSN code apply?

### Takeaway
From 22 Sep 2025, most retail-packed medicines (HSN 3004) carry 5% GST, down from 12%. About 36 to 37 specified life-saving drugs are nil-rated. The exact count is unverified.

### Cited Findings
- Rate moved from 12% to 5% after the 56th GST Council meeting. Cancer and rare-disease drugs are exempt. Sources: [Business Today](https://www.businesstoday.in/industry/pharma/story/cancer-rare-disease-medicines-exempted-from-gst-other-drugs-at-5-check-faqs-492416-2025-09-03); [Business Standard](https://www.business-standard.com/amp/industry/news/gst-cut-patient-benefits-cos-margin-pressure-125090401185_1.html)
- HSN 3004 covers retail-packed formulations and 3003 covers bulk. Source: [Tally](https://tallysolutions.com/gst/hsn-code-3004-product-classification-gst-rate-business-filing-guide/)
- Some sources warn against applying "3004 = 5%" blindly. Source: [Accountune](https://accountune.com/medicine-gst-rate-list)

### Inferences
- Insulin: HSN 3004 31 (insulin), likely 5%. Verify this.
- Antiseptic liquids (Dettol, Savlon), soaps, sanitisers, ORS powder, Eno, Vicks, balms and protein powders may fall outside 3004 or carry different rates. They are marked unsure below.
- Ayurvedic and proprietary products (Cremaffin, Liv 52, Moov, Volini) are usually classified 3004, but some under 3003/3304. Also marked unsure.

### Gaps
- HSN and GST for non-drug FMCG health items (Dettol soap 3401, sanitiser 3808/3004, Horlicks 1901, etc.) were not verified.

## Master item list (about 260 rows)

Legend:
- GST "5" means the standard post-Sep-2025 medicine rate, probable for HSN 3004.
- "5?" or "unknown" means unverified.
- Schedule values are the usual classification for the salt. Verify against the pack label.

| Name | Category | Manufacturer | Salt | Pack unit | Schedule | GST % | HSN |
|---|---|---|---|---|---|---|---|
| Dolo 650 Tablet | Fever/Pain | Micro Labs | Paracetamol 650mg | Strip of 15 | H (sold OTC) | 5 | 3004 |
| Dolo 500 Tablet | Fever/Pain | Micro Labs | Paracetamol 500mg | Strip of 15 | OTC | 5 | 3004 |
| Dolo 250 Oral Suspension | Fever/Pain | Micro Labs | Paracetamol 250mg/5ml | Bottle 60ml | OTC | 5 | 3004 |
| Calpol 500 Tablet | Fever/Pain | GSK | Paracetamol 500mg | Strip of 15 | OTC | 5 | 3004 |
| Calpol 650 Tablet | Fever/Pain | GSK | Paracetamol 650mg | Strip of 15 | H | 5 | 3004 |
| Calpol 250 Suspension | Fever/Pain | GSK | Paracetamol 250mg/5ml | Bottle 60ml | OTC | 5 | 3004 |
| Crocin Advance Tablet | Fever/Pain | Haleon | Paracetamol 500mg | Strip of 15 | OTC | 5 | 3004 |
| Crocin 650 Tablet | Fever/Pain | Haleon | Paracetamol 650mg | Strip of 15 | OTC | 5 | 3004 |
| Crocin Pain Relief Tablet | Fever/Pain | Haleon | Paracetamol + Caffeine | Strip of 15 | OTC | 5 | 3004 |
| P-500 Tablet | Fever/Pain | Ipca | Paracetamol 500mg | Strip of 10 | OTC | 5 | 3004 |
| Pacimol 650 Tablet | Fever/Pain | Ipca | Paracetamol 650mg | Strip of 15 | H | 5 | 3004 |
| Combiflam Tablet | Fever/Pain | Sanofi | Ibuprofen 400mg + Paracetamol 325mg | Strip of 20 | OTC | 5 | 3004 |
| Combiflam Suspension | Fever/Pain | Sanofi | Ibuprofen + Paracetamol | Bottle 60ml | H | 5 | 3004 |
| Brufen 400 Tablet | Fever/Pain | Abbott | Ibuprofen 400mg | Strip of 15 | H | 5 | 3004 |
| Ibugesic Plus Suspension | Fever/Pain | Cipla | Ibuprofen + Paracetamol | Bottle 60ml | H | 5 | 3004 |
| Meftal-Spas Tablet | Fever/Pain | Blue Cross | Mefenamic acid + Dicyclomine | Strip of 10 | H | 5 | 3004 |
| Meftal-P 125 Suspension | Fever/Pain | Blue Cross | Mefenamic acid 50mg/5ml (verify) | Bottle 60ml | H | 5 | 3004 |
| Saridon Tablet | Fever/Pain | Bayer | Propyphenazone + Paracetamol + Caffeine | Strip of 10 | OTC | 5 | 3004 |
| Disprin Tablet | Fever/Pain | Reckitt | Aspirin 350mg (effervescent) | Strip of 10 | OTC | 5 | 3004 |
| Zerodol-P Tablet | Fever/Pain | Ipca | Aceclofenac + Paracetamol | Strip of 10 | H | 5 | 3004 |
| Zerodol-SP Tablet | Fever/Pain | Ipca | Aceclofenac + Paracetamol + Serratiopeptidase | Strip of 10 | H | 5 | 3004 |
| Hifenac-P Tablet | Fever/Pain | Intas | Aceclofenac + Paracetamol | Strip of 10 | H | 5 | 3004 |
| Voveran SR 100 Tablet | Fever/Pain | Dr. Reddy's | Diclofenac 100mg SR | Strip of 15 | H | 5 | 3004 |
| Voveran 50 Injection / Dynapar AQ | Fever/Pain | Dr. Reddy's / Troikaa | Diclofenac | Ampoule 1ml | H | 5 | 3004 |
| Nimulid Tablet | Fever/Pain | Panacea Biotec | Nimesulide 100mg | Strip of 10 | H | 5 | 3004 |
| Ultracet Tablet | Fever/Pain | Janssen | Tramadol + Paracetamol | Strip of 15 | H1 | 5 | 3004 |
| Ketorol DT Tablet | Fever/Pain | Dr. Reddy's | Ketorolac 10mg | Strip of 15 | H | 5 | 3004 |
| Etoshine 90 Tablet | Fever/Pain | Sun Pharma | Etoricoxib 90mg | Strip of 10 | H | 5 | 3004 |
| Naprosyn 500 Tablet | Fever/Pain | RPG / Piramal (unknown) | Naproxen 500mg | Strip of 15 | H | 5 | 3004 |
| Spasmonil Tablet | Fever/Pain | Zydus | Dicyclomine + Paracetamol | Strip of 10 | H | 5 | 3004 |
| Cyclopam Tablet | Fever/Pain | Indoco | Dicyclomine + Paracetamol | Strip of 10 | H | 5 | 3004 |
| Buscopan 10 Tablet | Fever/Pain | Sanofi | Hyoscine butylbromide 10mg | Strip of 10 | H | 5 | 3004 |
| Drotin-M Tablet | Fever/Pain | Walter Bushnell | Drotaverine + Mefenamic acid | Strip of 10 | H | 5 | 3004 |
| Augmentin 625 Duo Tablet | Antibiotic | GSK | Amoxycillin 500mg + Clavulanic acid 125mg | Strip of 10 | H | 5 | 3004 |
| Augmentin 1000 Duo Tablet | Antibiotic | GSK | Amoxycillin 875mg + Clavulanic acid 125mg | Strip of 10 | H | 5 | 3004 |
| Augmentin Duo Oral Suspension | Antibiotic | GSK | Amoxycillin 400mg + Clavulanic acid 57mg /5ml | Bottle 30ml | H | 5 | 3004 |
| Clavam 625 Tablet | Antibiotic | Alkem | Amoxycillin + Clavulanic acid | Strip of 10 | H | 5 | 3004 |
| Moxikind-CV 625 Tablet | Antibiotic | Mankind | Amoxycillin + Clavulanic acid | Strip of 10 | H | 5 | 3004 |
| Mox 500 Capsule | Antibiotic | Sun Pharma (Ranbaxy) | Amoxycillin 500mg | Strip of 10 | H | 5 | 3004 |
| Novamox 500 Capsule | Antibiotic | Cipla | Amoxycillin 500mg | Strip of 10 | H | 5 | 3004 |
| Azithral 500 Tablet | Antibiotic | Alembic | Azithromycin 500mg | Strip of 5 | H | 5 | 3004 |
| Azithral 200 Liquid | Antibiotic | Alembic | Azithromycin 200mg/5ml | Bottle 15ml | H | 5 | 3004 |
| Azee 500 Tablet | Antibiotic | Cipla | Azithromycin 500mg | Strip of 3 | H | 5 | 3004 |
| Zithrox 500 Tablet | Antibiotic | Macleods | Azithromycin 500mg | Strip of 3 | H | 5 | 3004 |
| Taxim-O 200 Tablet | Antibiotic | Alkem | Cefixime 200mg | Strip of 10 | H | 5 | 3004 |
| Taxim-O Dry Syrup | Antibiotic | Alkem | Cefixime 50mg/5ml | Bottle 30ml | H | 5 | 3004 |
| Zifi 200 Tablet | Antibiotic | FDC | Cefixime 200mg | Strip of 10 | H | 5 | 3004 |
| Mahacef 200 Tablet | Antibiotic | Mankind | Cefixime 200mg | Strip of 10 | H | 5 | 3004 |
| Cefakind 500 Tablet | Antibiotic | Mankind | Cefuroxime 500mg | Strip of 10 | H | 5 | 3004 |
| Ceftum 500 Tablet | Antibiotic | GSK | Cefuroxime axetil 500mg | Strip of 10 | H | 5 | 3004 |
| Cepodem 200 Tablet | Antibiotic | Sun Pharma | Cefpodoxime 200mg | Strip of 10 | H | 5 | 3004 |
| Monocef 1g Injection | Antibiotic | Aristo | Ceftriaxone 1g | Vial | H | 5 | 3004 |
| Monocef-O 200 Tablet | Antibiotic | Aristo | Cefpodoxime 200mg | Strip of 10 | H | 5 | 3004 |
| Ciplox 500 Tablet | Antibiotic | Cipla | Ciprofloxacin 500mg | Strip of 10 | H | 5 | 3004 |
| Cifran 500 Tablet | Antibiotic | Sun Pharma | Ciprofloxacin 500mg | Strip of 10 | H | 5 | 3004 |
| Levoflox 500 Tablet | Antibiotic | Cipla | Levofloxacin 500mg | Strip of 10 | H | 5 | 3004 |
| Oflox 200 Tablet | Antibiotic | Cipla | Ofloxacin 200mg | Strip of 10 | H | 5 | 3004 |
| Oflox-OZ Tablet | Antibiotic | Cipla | Ofloxacin + Ornidazole | Strip of 10 | H | 5 | 3004 |
| Norflox-TZ Tablet | Antibiotic | Cipla | Norfloxacin + Tinidazole | Strip of 10 | H | 5 | 3004 |
| Flagyl 400 Tablet | Antibiotic | Abbott | Metronidazole 400mg | Strip of 15 | H | 5 | 3004 |
| Metrogyl 400 Tablet | Antibiotic | J B Chemicals | Metronidazole 400mg | Strip of 15 | H | 5 | 3004 |
| Doxy-1 L-DR Forte Capsule | Antibiotic | USV | Doxycycline + Lactic acid bacillus | Strip of 10 | H | 5 | 3004 |
| Linezolid 600 (Linospan) | Antibiotic | Cipla | Linezolid 600mg | Strip of 10 | H1 | 5 | 3004 |
| Clarithromycin (Claribid 500) | Antibiotic | Abbott | Clarithromycin 500mg | Strip of 4 | H1 | 5 | 3004 |
| Nitrofurantoin (Martifur 100) | Antibiotic | Glenmark (unknown) | Nitrofurantoin 100mg | Strip of 10 | H | 5 | 3004 |
| Fluconazole (Forcan 150) | Antifungal | Cipla | Fluconazole 150mg | Strip of 1 | H | 5 | 3004 |
| Zocon 150 Tablet | Antifungal | FDC | Fluconazole 150mg | Strip of 1 | H | 5 | 3004 |
| Itraconazole (Itaspor 100) | Antifungal | Intas | Itraconazole 100mg | Strip of 10 | H | 5 | 3004 |
| Terbinafine (Terbicip 250) | Antifungal | Cipla | Terbinafine 250mg | Strip of 7 | H | 5 | 3004 |
| Albendazole (Zentel 400) | Antiparasitic | GSK | Albendazole 400mg | Strip of 1 | OTC | 5 | 3004 |
| Bandy Plus Tablet | Antiparasitic | Mankind | Albendazole + Ivermectin | Strip of 1 | H | 5 | 3004 |
| Hydroxychloroquine (HCQS 200) | Antimalarial | Ipca | Hydroxychloroquine 200mg | Strip of 15 | H | 5 | 3004 |
| Valacyclovir/Acyclovir (Zovirax 400) | Antiviral | GSK | Acyclovir 400mg | Strip of 5 | H | 5 | 3004 |
| Oseltamivir (Fluvir 75) | Antiviral | Hetero | Oseltamivir 75mg | Strip of 10 | H1 (verify) | 5 | 3004 |
| Pan 40 Tablet | Stomach | Alkem | Pantoprazole 40mg | Strip of 15 | H | 5 | 3004 |
| Pan-D Capsule | Stomach | Alkem | Pantoprazole 40mg + Domperidone 30mg SR | Strip of 15 | H | 5 | 3004 |
| Pantocid 40 Tablet | Stomach | Sun Pharma | Pantoprazole 40mg | Strip of 15 | H | 5 | 3004 |
| Pantocid DSR Capsule | Stomach | Sun Pharma | Pantoprazole + Domperidone | Strip of 15 | H | 5 | 3004 |
| Pantop 40 Tablet | Stomach | Aristo | Pantoprazole 40mg | Strip of 15 | H | 5 | 3004 |
| Pan 40 IV Injection | Stomach | Alkem | Pantoprazole 40mg | Vial | H | 5 | 3004 |
| Rantac 150 Tablet | Stomach | J B Chemicals | Ranitidine 150mg | Strip of 30 | OTC/H (verify) | 5 | 3004 |
| Aciloc 150 Tablet | Stomach | Cadila | Ranitidine 150mg | Strip of 30 | H | 5 | 3004 |
| Famocid 40 Tablet | Stomach | Sun Pharma | Famotidine 40mg | Strip of 14 | H | 5 | 3004 |
| Omez 20 Capsule | Stomach | Dr. Reddy's | Omeprazole 20mg | Strip of 20 | H | 5 | 3004 |
| Omez-D Capsule | Stomach | Dr. Reddy's | Omeprazole + Domperidone | Strip of 15 | H | 5 | 3004 |
| Razo 20 Tablet | Stomach | Dr. Reddy's | Rabeprazole 20mg | Strip of 15 | H | 5 | 3004 |
| Razo-D Capsule | Stomach | Dr. Reddy's | Rabeprazole + Domperidone | Strip of 15 | H | 5 | 3004 |
| Rablet-D Capsule | Stomach | Lupin | Rabeprazole + Domperidone | Strip of 15 | H | 5 | 3004 |
| Nexpro 40 Tablet | Stomach | Torrent | Esomeprazole 40mg | Strip of 15 | H | 5 | 3004 |
| Digene Gel Mint | Stomach | Abbott | Magnesium hydroxide + Aluminium hydroxide + Simethicone | Bottle 200ml | OTC | 5? | 3004 |
| Digene Tablet | Stomach | Abbott | Antacid + Simethicone | Strip of 15 | OTC | 5? | 3004 |
| Gelusil MPS Liquid | Stomach | Pfizer | Aluminium + Magnesium hydroxide + Simethicone | Bottle 200ml | OTC | 5? | 3004 |
| Mucaine Gel | Stomach | Pfizer | Antacid + Oxetacaine | Bottle 200ml | H | 5 | 3004 |
| Sucrafil O Gel | Stomach | Systopic | Sucralfate + Oxetacaine | Bottle 100ml | H | 5 | 3004 |
| Eno Fruit Salt Regular | Stomach | Haleon | Sodium bicarbonate + Citric acid | Sachet 5g / Bottle 100g | OTC | unknown | unknown (3004?) |
| Pudin Hara Pearls | Stomach | Dabur | Mint oil | Strip of 10 | OTC (Ayurvedic) | 5? | 3004 |
| Hajmola Regular | Stomach | Dabur | Ayurvedic digestive | Bottle 120 tabs | OTC (Ayurvedic) | unknown | unknown |
| Cremaffin Plus Syrup | Stomach | Abbott | Liquid paraffin + Milk of magnesia + Sodium picosulfate | Bottle 225ml | OTC | 5? | 3004 |
| Dulcolax 5mg Tablet | Stomach | Sanofi | Bisacodyl 5mg | Strip of 10 | OTC | 5 | 3004 |
| Duphalac Syrup | Stomach | Abbott | Lactulose | Bottle 250ml | OTC | 5 | 3004 |
| Isabgol (Sat Isabgol) | Stomach | Sidhpur | Ispaghula husk | Pack 100g | OTC | unknown | unknown (1211?) |
| Looz Syrup | Stomach | Hetero (unknown) | Lactulose | Bottle 200ml | OTC | 5 | 3004 |
| Ondem 4 Tablet | Stomach | Alkem | Ondansetron 4mg | Strip of 10 | H | 5 | 3004 |
| Emeset 4 Tablet | Stomach | Cipla | Ondansetron 4mg | Strip of 10 | H | 5 | 3004 |
| Vomikind MD 4 | Stomach | Mankind | Ondansetron 4mg mouth-dissolving | Strip of 10 | H | 5 | 3004 |
| Domstal Tablet | Stomach | Torrent | Domperidone 10mg | Strip of 10 | H | 5 | 3004 |
| Perinorm Tablet | Stomach | Ipca | Metoclopramide 10mg | Strip of 10 | H | 5 | 3004 |
| Loperamide (Imodium 2mg) | Stomach | Johnson & Johnson | Loperamide 2mg | Strip of 4 | H | 5 | 3004 |
| Eldoper Capsule | Stomach | Micro Labs | Loperamide 2mg | Strip of 10 | H | 5 | 3004 |
| Sporlac Sachet | Stomach | Sanzyme | Lactic acid bacillus | Sachet 1g | OTC | 5? | 3004 |
| Enterogermina Oral Suspension | Stomach | Sanofi | Bacillus clausii | Pack of 5 x 5ml vial | OTC | 5? | 3004 |
| Vizylac Capsule | Stomach | Abbott (verify) | Probiotic | Strip of 10 | OTC | 5? | 3004 |
| Udiliv 300 Tablet | Stomach | Abbott | Ursodeoxycholic acid 300mg | Strip of 15 | H | 5 | 3004 |
| Liv 52 DS Tablet | Stomach | Himalaya | Ayurvedic liver formula | Bottle 60 tabs | OTC (Ayurvedic) | 5? | 3004 |
| Aristozyme Syrup | Stomach | Aristo | Digestive enzymes | Bottle 200ml | OTC | 5? | 3004 |
| Unienzyme Tablet | Stomach | Torrent | Digestive enzymes + charcoal | Strip of 15 | OTC | 5? | 3004 |
| Colospa 135 Tablet | Stomach | Abbott | Mebeverine 135mg | Strip of 10 | H | 5 | 3004 |
| Electral Powder | ORS | FDC | WHO ORS | Sachet 21.8g | OTC | 5? | 3004 (verify) |
| ORS Apple/Orange (Enerzal / ORSL) | ORS | FDC / Kenvue (ORSL) | Electrolyte drink | Tetra pack 200ml | OTC | unknown | unknown (2202?) |
| Walyte ORS | ORS | Walter Bushnell (unknown) | WHO ORS | Sachet | OTC | 5? | 3004 |
| Zinconia / Zinc 20mg Dispersible | ORS | Various | Zinc sulphate 20mg | Strip of 10 | OTC | 5 | 3004 |
| Allegra 120mg Tablet | Allergy/Cold | Sanofi | Fexofenadine 120mg | Strip of 10 | H | 5 | 3004 |
| Allegra 180mg Tablet | Allergy/Cold | Sanofi | Fexofenadine 180mg | Strip of 10 | H | 5 | 3004 |
| Montair-LC Tablet | Allergy/Cold | Cipla | Montelukast 10mg + Levocetirizine 5mg | Strip of 15 | H | 5 | 3004 |
| Montair-LC Kid Tablet | Allergy/Cold | Cipla | Montelukast 4mg + Levocetirizine 2.5mg | Strip of 10 | H | 5 | 3004 |
| Montek-LC Tablet | Allergy/Cold | Sun Pharma | Montelukast + Levocetirizine | Strip of 10 | H | 5 | 3004 |
| Montina-L Tablet | Allergy/Cold | Torrent | Montelukast + Levocetirizine | Strip of 10 | H | 5 | 3004 |
| Levocet 5 Tablet | Allergy/Cold | Hetero | Levocetirizine 5mg | Strip of 10 | H | 5 | 3004 |
| Xyzal 5 Tablet | Allergy/Cold | Dr. Reddy's | Levocetirizine 5mg | Strip of 10 | H | 5 | 3004 |
| Cetzine 10 Tablet | Allergy/Cold | Dr. Reddy's | Cetirizine 10mg | Strip of 10 | OTC/H | 5 | 3004 |
| Okacet 10 Tablet | Allergy/Cold | Cipla | Cetirizine 10mg | Strip of 10 | OTC/H | 5 | 3004 |
| Avil 25 Tablet | Allergy/Cold | Sanofi | Pheniramine 25mg | Strip of 15 | H | 5 | 3004 |
| Avil Injection | Allergy/Cold | Sanofi | Pheniramine 22.75mg/ml | Ampoule 2ml | H | 5 | 3004 |
| Atarax 25 Tablet | Allergy/Cold | Dr. Reddy's | Hydroxyzine 25mg | Strip of 15 | H | 5 | 3004 |
| Sinarest Tablet | Allergy/Cold | Centaur | Paracetamol + Phenylephrine + Chlorpheniramine (+Caffeine) | Strip of 10 | OTC | 5 | 3004 |
| Sinarest Syrup | Allergy/Cold | Centaur | Paracetamol + Phenylephrine + Chlorpheniramine | Bottle 60ml | OTC | 5 | 3004 |
| Cheston Cold Tablet | Allergy/Cold | Cipla | Cetirizine + Paracetamol + Phenylephrine | Strip of 10 | OTC | 5 | 3004 |
| Vicks Action 500 Advanced | Allergy/Cold | Procter & Gamble | Paracetamol + Phenylephrine + Caffeine | Strip of 10 | OTC | 5 | 3004 |
| Vicks VapoRub | Allergy/Cold | Procter & Gamble | Menthol + Camphor + Eucalyptus oil | Jar 25ml / 50ml | OTC (Ayurvedic licence) | 5? | 3004 (verify) |
| Vicks Inhaler | Allergy/Cold | Procter & Gamble | Menthol + Camphor | Inhaler 0.5ml | OTC | 5? | 3004 |
| Otrivin Nasal Spray Adult | Allergy/Cold | Haleon | Xylometazoline 0.1% | Spray 10ml | OTC | 5 | 3004 |
| Nasivion Nasal Drops | Allergy/Cold | P&G Health (Merck) | Oxymetazoline | Bottle 10ml | OTC | 5 | 3004 |
| Nasoclear / Saline Nasal Drops | Allergy/Cold | Various | Sodium chloride 0.65% | Bottle 10ml | OTC | 5 | 3004 |
| Flutibact / Flomist Nasal Spray | Allergy/Cold | Cipla | Fluticasone 50mcg | Spray 120 MD | H | 5 | 3004 |
| Strepsils Lozenges | Allergy/Cold | Reckitt | Amylmetacresol + Dichlorobenzyl alcohol | Strip of 8 | OTC | 5? | 3004 |
| Halls / Vicks Cough Drops | Allergy/Cold | Mondelez / P&G | Menthol | Pack | OTC | unknown | unknown (1704?) |
| Karvol Plus Capsule | Allergy/Cold | Abbott | Inhalant oils | Strip of 10 | OTC | 5? | 3004 |
| Benadryl Cough Formula | Cough syrup | Kenvue (J&J) | Diphenhydramine + Ammonium chloride + Sodium citrate | Bottle 150ml | OTC | 5 | 3004 |
| Benadryl DR Syrup | Cough syrup | Kenvue | Dextromethorphan + Chlorpheniramine (verify) | Bottle 50ml | H | 5 | 3004 |
| Ascoril LS Syrup | Cough syrup | Glenmark | Ambroxol + Levosalbutamol + Guaiphenesin | Bottle 100ml | H | 5 | 3004 |
| Ascoril-D Plus Syrup | Cough syrup | Glenmark | Dextromethorphan + Phenylephrine + Chlorpheniramine | Bottle 100ml | H | 5 | 3004 |
| Grilinctus Syrup | Cough syrup | Franco-Indian | Dextromethorphan + Chlorpheniramine + Guaiphenesin + Ammonium chloride | Bottle 100ml | H | 5 | 3004 |
| Grilinctus-BM Syrup | Cough syrup | Franco-Indian | Bromhexine + Guaiphenesin + Terbutaline (verify) | Bottle 100ml | H | 5 | 3004 |
| Alex Syrup | Cough syrup | Glenmark | Dextromethorphan + Chlorpheniramine + Phenylephrine | Bottle 100ml | H | 5 | 3004 |
| Corex DX Syrup | Cough syrup | Pfizer | Dextromethorphan + Chlorpheniramine | Bottle 100ml | H | 5 | 3004 |
| Chericof / Tossex | Cough syrup | Various | Dextromethorphan combos | Bottle 100ml | H | 5 | 3004 |
| Mucolite Syrup | Cough syrup | Dr. Reddy's | Ambroxol 30mg/5ml | Bottle 100ml | H | 5 | 3004 |
| Honitus Syrup | Cough syrup | Dabur | Ayurvedic (honey, tulsi) | Bottle 100ml | OTC (Ayurvedic) | 5? | 3004 |
| Koflet Syrup | Cough syrup | Himalaya | Ayurvedic | Bottle 100ml | OTC (Ayurvedic) | 5? | 3004 |
| Asthalin Inhaler | Respiratory | Cipla | Salbutamol 100mcg | Inhaler 200 MD | H | 5 | 3004 |
| Asthalin 4 Tablet | Respiratory | Cipla | Salbutamol 4mg | Strip of 10 | H | 5 | 3004 |
| Asthalin Respules | Respiratory | Cipla | Salbutamol 2.5ml | Pack of 20 respules | H | 5 | 3004 |
| Budecort 0.5 Respules | Respiratory | Cipla | Budesonide 0.5mg | Pack of 5 respules | H | 5 | 3004 |
| Duolin Respules | Respiratory | Cipla | Levosalbutamol + Ipratropium | Pack of 5 respules | H | 5 | 3004 |
| Foracort 200 Inhaler | Respiratory | Cipla | Formoterol + Budesonide | Inhaler 120 MD | H | 5 | 3004 |
| Seroflo 250 Rotacap | Respiratory | Cipla | Salmeterol + Fluticasone | Bottle 30 rotacaps | H | 5 | 3004 |
| Deriphyllin Retard 150 | Respiratory | Zydus | Etofylline + Theophylline | Strip of 30 | H | 5 | 3004 |
| Doxofylline (Doxolin 400) | Respiratory | Alkem (verify) | Doxofylline 400mg | Strip of 10 | H | 5 | 3004 |
| Glycomet 500 SR Tablet | Diabetes | USV | Metformin 500mg SR | Strip of 20 | H | 5 | 3004 |
| Glycomet GP 1 Tablet | Diabetes | USV | Glimepiride 1mg + Metformin 500mg | Strip of 15 | H | 5 | 3004 |
| Glycomet GP 2 Tablet | Diabetes | USV | Glimepiride 2mg + Metformin 500mg | Strip of 15 | H | 5 | 3004 |
| Glycomet 1g SR | Diabetes | USV | Metformin 1000mg SR | Strip of 10 | H | 5 | 3004 |
| Gluconorm-G 1 | Diabetes | Lupin | Glimepiride + Metformin | Strip of 15 | H | 5 | 3004 |
| Amaryl 1mg Tablet | Diabetes | Sanofi | Glimepiride 1mg | Strip of 30 | H | 5 | 3004 |
| Amaryl M 1 Tablet | Diabetes | Sanofi | Glimepiride + Metformin | Strip of 30 | H | 5 | 3004 |
| Janumet 50/500 Tablet | Diabetes | MSD | Sitagliptin + Metformin | Strip of 15 | H | 5 | 3004 |
| Januvia 100 Tablet | Diabetes | MSD | Sitagliptin 100mg | Strip of 7 | H | 5 | 3004 |
| Istavel / Zita-Met (sitagliptin generics) | Diabetes | Sun / Glenmark | Sitagliptin + Metformin | Strip of 10/15 | H | 5 | 3004 |
| Galvus Met 50/500 Tablet | Diabetes | Novartis | Vildagliptin + Metformin | Strip of 15 | H | 5 | 3004 |
| Jalra-M 50/500 | Diabetes | USV | Vildagliptin + Metformin | Strip of 15 | H | 5 | 3004 |
| Trajenta 5 Tablet | Diabetes | Boehringer Ingelheim / Lupin | Linagliptin 5mg | Strip of 10 | H | 5 | 3004 |
| Tenelia-M 20/500 | Diabetes | Glenmark | Teneligliptin + Metformin | Strip of 15 | H | 5 | 3004 |
| Jardiance 10 Tablet | Diabetes | Boehringer Ingelheim | Empagliflozin 10mg | Strip of 10 | H | 5 | 3004 |
| Forxiga 10 Tablet | Diabetes | AstraZeneca | Dapagliflozin 10mg | Strip of 14 | H | 5 | 3004 |
| Dapagliflozin generics (Dapanorm etc.) | Diabetes | Mankind / others | Dapagliflozin 10mg | Strip of 10 | H | 5 | 3004 |
| Volix / Voglibose 0.3 (Volibo) | Diabetes | Various | Voglibose 0.3mg | Strip of 10 | H | 5 | 3004 |
| Mounjaro 2.5mg KwikPen | Diabetes/Obesity | Eli Lilly | Tirzepatide | Pen | H | 5 | 3004 |
| Huminsulin 30/70 Vial | Diabetes (insulin) | Eli Lilly | Human insulin 30/70 | Vial 10ml | H | 5 | 3004 |
| Mixtard 30 Penfill | Diabetes (insulin) | Novo Nordisk | Human insulin 30/70 | Cartridge 3ml | H | 5 | 3004 |
| Actrapid Vial | Diabetes (insulin) | Novo Nordisk | Regular human insulin | Vial 10ml | H | 5 | 3004 |
| Lantus SoloStar | Diabetes (insulin) | Sanofi | Insulin glargine | Pen 3ml | H | 5 | 3004 |
| Basalog One | Diabetes (insulin) | Biocon | Insulin glargine | Pen 3ml | H | 5 | 3004 |
| Novomix 30 FlexPen | Diabetes (insulin) | Novo Nordisk | Insulin aspart 30/70 | Pen 3ml | H | 5 | 3004 |
| Humalog Mix 25 KwikPen | Diabetes (insulin) | Eli Lilly | Insulin lispro mix | Pen 3ml | H | 5 | 3004 |
| Insugen 30/70 | Diabetes (insulin) | Biocon | Human insulin | Cartridge 3ml | H | 5 | 3004 |
| Ryzodeg FlexTouch | Diabetes (insulin) | Novo Nordisk | Degludec + Aspart | Pen 3ml | H | 5 | 3004 |
| Rybelsus 3mg Tablet | Diabetes | Novo Nordisk | Oral semaglutide | Strip of 10 | H | 5 | 3004 |
| Telma 40 Tablet | BP/Heart | Glenmark | Telmisartan 40mg | Strip of 30 | H | 5 | 3004 |
| Telma-H Tablet | BP/Heart | Glenmark | Telmisartan + Hydrochlorothiazide | Strip of 30 | H | 5 | 3004 |
| Telma-AM Tablet | BP/Heart | Glenmark | Telmisartan + Amlodipine | Strip of 30 | H | 5 | 3004 |
| Telmikind 40 | BP/Heart | Mankind | Telmisartan 40mg | Strip of 10 | H | 5 | 3004 |
| Telvas 40 | BP/Heart | Aristo | Telmisartan 40mg | Strip of 15 | H | 5 | 3004 |
| Amlong 5 Tablet | BP/Heart | Micro Labs | Amlodipine 5mg | Strip of 30 | H | 5 | 3004 |
| Amlokind 5 | BP/Heart | Mankind | Amlodipine 5mg | Strip of 10 | H | 5 | 3004 |
| Stamlo 5 | BP/Heart | Dr. Reddy's | Amlodipine 5mg | Strip of 30 | H | 5 | 3004 |
| Losar 50 / Losacar 50 | BP/Heart | Unichem / Zydus | Losartan 50mg | Strip of 10/15 | H | 5 | 3004 |
| Olmezest 20 | BP/Heart | Sun Pharma | Olmesartan 20mg | Strip of 10 | H | 5 | 3004 |
| Cilacar 10 | BP/Heart | J B Chemicals | Cilnidipine 10mg | Strip of 15 | H | 5 | 3004 |
| Concor 5 | BP/Heart | Merck (Abbott) | Bisoprolol 5mg | Strip of 10 | H | 5 | 3004 |
| Met XL 50 Tablet | BP/Heart | Ajanta | Metoprolol succinate 50mg | Strip of 15 | H | 5 | 3004 |
| Metolar XR 50 | BP/Heart | Cipla | Metoprolol succinate 50mg | Strip of 10 | H | 5 | 3004 |
| Nebicard 5 | BP/Heart | Torrent | Nebivolol 5mg | Strip of 10 | H | 5 | 3004 |
| Aten 50 | BP/Heart | Zydus | Atenolol 50mg | Strip of 14 | H | 5 | 3004 |
| Lasix 40 Tablet | BP/Heart | Sanofi | Furosemide 40mg | Strip of 15 | H | 5 | 3004 |
| Dytor 10 | BP/Heart | Cipla | Torsemide 10mg | Strip of 15 | H | 5 | 3004 |
| Aldactone 25 | BP/Heart | RPG Life Sciences | Spironolactone 25mg | Strip of 15 | H | 5 | 3004 |
| Vymada 50 | BP/Heart | Novartis | Sacubitril + Valsartan | Strip of 14 | H | 5 | 3004 |
| Ecosprin 75 Tablet | BP/Heart | USV | Aspirin 75mg | Strip of 14 | H | 5 | 3004 |
| Ecosprin AV 75/10 Capsule | BP/Heart | USV | Aspirin + Atorvastatin | Strip of 15 | H | 5 | 3004 |
| Clopilet 75 | BP/Heart | Sun Pharma | Clopidogrel 75mg | Strip of 15 | H | 5 | 3004 |
| Deplatt-A 75 | BP/Heart | Torrent | Clopidogrel + Aspirin | Strip of 15 | H | 5 | 3004 |
| Brilinta 90 | BP/Heart | AstraZeneca | Ticagrelor 90mg | Strip of 14 | H | 5 | 3004 |
| Rosuvas 10 Tablet | BP/Heart | Sun Pharma | Rosuvastatin 10mg | Strip of 15 | H | 5 | 3004 |
| Rozavel 10 | BP/Heart | Sun Pharma | Rosuvastatin 10mg | Strip of 15 | H | 5 | 3004 |
| Rozucor 10 | BP/Heart | Torrent | Rosuvastatin 10mg | Strip of 10 | H | 5 | 3004 |
| Atorva 10 Tablet | BP/Heart | Zydus | Atorvastatin 10mg | Strip of 15 | H | 5 | 3004 |
| Atorva 20 Tablet | BP/Heart | Zydus | Atorvastatin 20mg | Strip of 15 | H | 5 | 3004 |
| Storvas 10 | BP/Heart | Sun Pharma | Atorvastatin 10mg | Strip of 15 | H | 5 | 3004 |
| Lipicure 10 | BP/Heart | Intas | Atorvastatin 10mg | Strip of 10 | H | 5 | 3004 |
| Sorbitrate 5 | BP/Heart | Abbott | Isosorbide dinitrate 5mg (sublingual) | Strip of 50 | H | 5 | 3004 |
| Nitrocontin 2.6 | BP/Heart | Modi-Mundipharma | Glyceryl trinitrate SR | Strip of 30 | H | 5 | 3004 |
| Acitrom 2 | BP/Heart | Abbott | Acenocoumarol 2mg | Strip of 30 | H | 5 | 3004 |
| Thyronorm 25mcg | Thyroid | Abbott | Levothyroxine 25mcg | Bottle 100 tabs | H | 5 | 3004 |
| Thyronorm 50mcg | Thyroid | Abbott | Levothyroxine 50mcg | Bottle 100 tabs | H | 5 | 3004 |
| Thyronorm 100mcg | Thyroid | Abbott | Levothyroxine 100mcg | Bottle 100 tabs | H | 5 | 3004 |
| Eltroxin 50mcg | Thyroid | GSK (Aspen) | Levothyroxine 50mcg | Bottle 100 tabs | H | 5 | 3004 |
| Thyrox 50 | Thyroid | Macleods | Levothyroxine 50mcg | Bottle 100 tabs | H | 5 | 3004 |
| Neo-Mercazole 5 | Thyroid | Abbott | Carbimazole 5mg | Strip of 100 (verify) | H | 5 | 3004 |
| Becosules Capsule | Vitamins | Pfizer | B-complex + Vitamin C | Strip of 20 | OTC | 5? | 3004 |
| Becosules Z Capsule | Vitamins | Pfizer | B-complex + Zinc | Strip of 20 | OTC | 5? | 3004 |
| Shelcal 500 Tablet | Vitamins | Torrent | Calcium carbonate + Vitamin D3 | Strip of 15 | OTC | 5? | 3004 |
| Shelcal HD Tablet | Vitamins | Torrent | Calcium + D3 (high dose) | Strip of 15 | OTC | 5? | 3004 |
| Calcimax 500 | Vitamins | Meyer | Calcium + D3 + minerals | Strip of 15 (verify) | OTC | 5? | 3004 |
| Zincovit Tablet | Vitamins | Apex | Multivitamin + Zinc | Strip of 15 | OTC | 5? | 3004 |
| Zincovit Syrup | Vitamins | Apex | Multivitamin + Zinc | Bottle 200ml | OTC | 5? | 3004 |
| Neurobion Forte Tablet | Vitamins | P&G Health (Merck) | B1 + B6 + B12 | Strip of 30 | OTC | 5? | 3004 |
| Nurokind-LC / Nervz G | Vitamins | Mankind / others | Methylcobalamin + L-carnitine | Strip of 10 | OTC | 5? | 3004 |
| Methycobal 500 | Vitamins | Eisai | Methylcobalamin 500mcg | Strip of 10 | OTC | 5? | 3004 |
| Supradyn Daily Tablet | Vitamins | Bayer | Multivitamin + minerals | Strip of 15 | OTC | 5? | 3004 |
| Revital H Capsule | Vitamins | Sun Pharma | Multivitamin + Ginseng | Bottle 30 caps | OTC (nutraceutical) | unknown | unknown (2106?) |
| A to Z NS Tablet | Vitamins | Alkem | Multivitamin + minerals | Strip of 15 | OTC | 5? | 3004 |
| Evion 400 Capsule | Vitamins | P&G Health (Merck) | Vitamin E 400mg | Strip of 10 | OTC | 5? | 3004 |
| Calcirol Sachet | Vitamins | Cadila | Cholecalciferol 60000 IU | Sachet 1g | H | 5 | 3004 |
| Uprise D3 60K Capsule | Vitamins | Alkem | Cholecalciferol 60000 IU | Strip of 4 | H (verify) | 5 | 3004 |
| Limcee Tablet | Vitamins | Abbott | Vitamin C 500mg chewable | Strip of 15 | OTC | 5? | 3004 |
| Celin 500 | Vitamins | GSK | Vitamin C 500mg | Strip of 15 | OTC | 5? | 3004 |
| Folvite 5mg | Vitamins | Pfizer | Folic acid 5mg | Strip of 45 | H | 5 | 3004 |
| Livogen Tablet | Vitamins | P&G Health (Merck) | Ferrous fumarate + Folic acid | Strip of 15 | OTC | 5? | 3004 |
| Orofer XT Tablet | Vitamins | Emcure | Ferrous ascorbate + Folic acid | Strip of 10 | H (verify) | 5 | 3004 |
| Dexorange Syrup | Vitamins | Franco-Indian | Iron + B12 + Folic acid | Bottle 200ml | OTC | 5? | 3004 |
| Protinex Original | Supplements | Danone | Protein powder | Tin 250g | OTC | unknown | unknown (2106?) |
| Ensure Powder | Supplements | Abbott | Nutrition powder | Tin 400g | OTC | unknown | unknown (1901/2106?) |
| Betnovate-C Cream | Skin | GSK | Betamethasone + Clioquinol | Tube 30g | H | 5 | 3004 |
| Betnovate-N Cream | Skin | GSK | Betamethasone + Neomycin | Tube 20g | H | 5 | 3004 |
| Betnovate Cream | Skin | GSK | Betamethasone valerate 0.1% | Tube 20g | H | 5 | 3004 |
| Candid Cream | Skin | Glenmark | Clotrimazole 1% | Tube 20g | OTC | 5 | 3004 |
| Candid-B Cream | Skin | Glenmark | Clotrimazole + Beclomethasone | Tube 20g | H | 5 | 3004 |
| Candid Dusting Powder | Skin | Glenmark | Clotrimazole 1% | Bottle 100g | OTC | 5 | 3004 |
| Soframycin Skin Cream | Skin | Sanofi | Framycetin 1% | Tube 30g | OTC/H (verify) | 5 | 3004 |
| Tenovate Cream | Skin | GSK | Clobetasol propionate 0.05% | Tube 30g | H | 5 | 3004 |
| Lobate-GM Cream | Skin | Ajanta (verify) | Clobetasol + Gentamicin + Miconazole | Tube 15g | H | 5 | 3004 |
| Panderm+ / Quadriderm RF Cream | Skin | Macleods / Organon | Steroid + antibiotic + antifungal | Tube 15g | H | 5 | 3004 |
| T-Bact Ointment | Skin | GSK | Mupirocin 2% | Tube 5g | H | 5 | 3004 |
| Fucidin Cream | Skin | Leo / Abbott (verify) | Fusidic acid 2% | Tube 10g | H | 5 | 3004 |
| Luliconazole (Lulifin 1%) Cream | Skin | Sun Pharma (verify) | Luliconazole 1% | Tube 30g | H | 5 | 3004 |
| Ring Guard Cream | Skin | Reckitt | Miconazole (verify) | Tube 12g | OTC | 5? | 3004 |
| Itch Guard Cream | Skin | Reckitt | Antifungal OTC | Tube 12g | OTC | 5? | 3004 |
| Burnol Cream | Skin | Dr. Morepen | Aminacrine + Cetrimide | Tube 20g | OTC | 5? | 3004 |
| Silverex Cream | Skin | Sun Pharma (verify) | Silver sulfadiazine 1% | Tube 20g | H | 5 | 3004 |
| Permethrin 5% (Permite) Cream | Skin | Glenmark | Permethrin 5% | Tube 30g | H | 5 | 3004 |
| Lacto Calamine Lotion | Skin | Piramal | Calamine + Kaolin | Bottle 120ml | OTC (cosmetic?) | unknown | unknown (3304?) |
| Boroline Cream | Skin | G D Pharmaceuticals | Boric acid + Zinc oxide | Tube 20g | OTC | unknown | unknown |
| Boroplus Antiseptic Cream | Skin | Emami | Ayurvedic antiseptic | Tube 40ml | OTC | unknown | unknown |
| Moisturex / Venusia lotion | Skin | Sun / Dr. Reddy's | Emollient | Bottle | OTC (cosmetic) | unknown | 3304? |
| Volini Gel | Pain gel | Sun Pharma | Diclofenac + Linseed oil + Menthol + Methyl salicylate | Tube 30g / 75g | OTC | 5 | 3004 |
| Volini Spray | Pain gel | Sun Pharma | Diclofenac + Methyl salicylate + Menthol | Can 40g / 100g | OTC | 5 | 3004 |
| Moov Cream | Pain gel | Reckitt | Ayurvedic (wintergreen, eucalyptus) | Tube 50g | OTC (Ayurvedic) | 5? | 3004 |
| Iodex Balm | Pain gel | Haleon | Ayurvedic (wintergreen, turpentine) | Jar 40g | OTC (Ayurvedic) | 5? | 3004 |
| Omnigel | Pain gel | Cipla | Diclofenac + Linseed oil + Menthol + Methyl salicylate | Tube 30g | OTC | 5 | 3004 |
| Relispray | Pain gel | Ranbaxy / Sun (verify) | Diclofenac + Methyl salicylate | Can 55g | OTC | 5 | 3004 |
| Zandu Balm | Pain gel | Emami | Ayurvedic (menthol, wintergreen) | Jar 8ml | OTC (Ayurvedic) | 5? | 3004 |
| Amrutanjan Pain Balm | Pain gel | Amrutanjan | Ayurvedic | Jar 8ml | OTC (Ayurvedic) | 5? | 3004 |
| Dettol Antiseptic Liquid | Antiseptic | Reckitt | Chloroxylenol 4.8% | Bottle 125/250/550ml | OTC | unknown | unknown (3808/3004?) |
| Savlon Antiseptic Liquid | Antiseptic | ITC | Chlorhexidine + Cetrimide | Bottle 200ml | OTC | unknown | unknown (3808/3004?) |
| Betadine 10% Solution | Antiseptic | Win-Medicare | Povidone-iodine 10% | Bottle 100ml | OTC | 5 | 3004 |
| Betadine 5% Ointment | Antiseptic | Win-Medicare | Povidone-iodine 5% | Tube 20g | OTC | 5 | 3004 |
| Soframycin / Neosporin powder | Antiseptic | Sanofi / GSK | Neomycin + Polymyxin + Bacitracin | Bottle 10g | H (verify) | 5 | 3004 |
| Dettol Hand Sanitizer | Antiseptic | Reckitt | Ethyl alcohol | Bottle 50ml | OTC | unknown | unknown (3808?) |
| Hansaplast / Band-Aid strips | First aid | Beiersdorf / Kenvue | Adhesive bandage | Pack of 10/100 | OTC | unknown | 3005 (verify) |
| Cotton roll / gauze (Absorbent cotton) | First aid | Various | Cotton | Roll 50g/100g | OTC | unknown | 3005 (verify) |
| Crepe bandage | First aid | Various | Elastic bandage | Roll 10cm | OTC | unknown | 3005 (verify) |
| Ciplox Eye/Ear Drops | Eye/Ear | Cipla | Ciprofloxacin 0.3% | Bottle 10ml | H | 5 | 3004 |
| Moxicip / Vigamox Eye Drops | Eye/Ear | Cipla / Novartis(Alcon) | Moxifloxacin 0.5% | Bottle 5ml | H | 5 | 3004 |
| Refresh Tears Eye Drops | Eye/Ear | Allergan (AbbVie) | Carboxymethylcellulose 0.5% | Bottle 10ml | OTC | 5 | 3004 |
| Tear Drops / Lubistar | Eye/Ear | Various | CMC lubricant | Bottle 10ml | OTC | 5 | 3004 |
| Itone Eye Drops | Eye/Ear | Deys | Ayurvedic | Bottle 10ml | OTC | 5? | 3004 |
| Toba-DM / Tobaflam Eye Drops | Eye/Ear | Sun / others | Tobramycin + Dexamethasone | Bottle 5ml | H | 5 | 3004 |
| Lotepred Eye Drops | Eye/Ear | Sun Pharma | Loteprednol 0.5% | Bottle 5ml | H | 5 | 3004 |
| Olopat Eye Drops | Eye/Ear | Sun (verify) | Olopatadine 0.1% | Bottle 5ml | H | 5 | 3004 |
| Waxsol Ear Drops | Eye/Ear | Cipla (verify) | Docusate sodium | Bottle 10ml | OTC | 5 | 3004 |
| Candibiotic Plus Ear Drops | Eye/Ear | Glenmark | Clotrimazole + Beclomethasone + Chloramphenicol + Lidocaine | Bottle 5ml | H | 5 | 3004 |
| Otogesic / Soliwax Ear Drops | Eye/Ear | Various | Ear wax / analgesic | Bottle 10ml | OTC | 5 | 3004 |
| Meprate 10 Tablet | Women's health | Serdia | Medroxyprogesterone 10mg | Strip of 10 | H | 5 | 3004 |
| Primolut-N Tablet | Women's health | Bayer | Norethisterone 5mg | Strip of 10 | H | 5 | 3004 |
| Duphaston 10mg | Women's health | Abbott | Dydrogesterone 10mg | Strip of 20 | H | 5 | 3004 |
| Susten 200 Capsule | Women's health | Sun Pharma | Progesterone 200mg | Strip of 10 | H | 5 | 3004 |
| Unwanted-72 Tablet | Women's health | Mankind | Levonorgestrel 1.5mg | Strip of 1 | OTC | 5 | 3004 |
| i-pill Tablet | Women's health | Piramal | Levonorgestrel 1.5mg | Strip of 1 | OTC | 5 | 3004 |
| Mala-D Tablet | Women's health | Pfizer (verify) | Levonorgestrel + Ethinylestradiol | Strip of 28 | OTC (verify) | 5 | 3004 |
| Ovral-L Tablet | Women's health | Pfizer | Levonorgestrel + Ethinylestradiol | Strip of 21 | H | 5 | 3004 |
| Saheli / Chhaya | Women's health | HLL Lifecare | Centchroman (Ormeloxifene) 30mg | Strip of 8 | OTC | 5 | 3004 |
| Pause-MF / Trapic MF | Women's health | Emcure (Pause) / Sun (verify) | Tranexamic acid + Mefenamic acid | Strip of 10 | H | 5 | 3004 |
| Mifepristone/Misoprostol kit (MTP Kit) | Women's health | Cipla | Mifepristone + Misoprostol | Kit of 5 tabs | H (sale restricted) | 5 | 3004 |
| Clomiphene (Fertomid 50) | Women's health | Cipla | Clomiphene 50mg | Strip of 10 | H | 5 | 3004 |
| Prega News Kit | Women's health | Mankind | hCG test card | Kit of 1 | OTC (device) | unknown | 3822 (verify) |
| Candid V6 Vaginal Tablet | Women's health | Glenmark | Clotrimazole 100mg | Pack of 6 | H | 5 | 3004 |
| Evecare Syrup | Women's health | Himalaya | Ayurvedic uterine tonic | Bottle 200ml | OTC (Ayurvedic) | 5? | 3004 |
| Cystone Tablet | Urology | Himalaya | Ayurvedic | Bottle 60 tabs | OTC (Ayurvedic) | 5? | 3004 |
| Urimax 0.4 Capsule | Urology | Cipla | Tamsulosin 0.4mg | Strip of 15 | H | 5 | 3004 |
| Citralka Liquid | Urology | Pfizer | Disodium hydrogen citrate | Bottle 100ml | OTC | 5 | 3004 |
| Manforce 50 / Suhagra 50 | Urology | Mankind / Cipla | Sildenafil 50mg | Strip of 4 | H | 5 | 3004 |
| Manforce Condoms | Sexual health | Mankind | Latex condom | Pack of 10 | OTC (device) | unknown | 4014 (verify) |
| Durex Condoms | Sexual health | Reckitt | Latex condom | Pack of 10 | OTC (device) | unknown | 4014 (verify) |
| Alprax 0.25 Tablet | CNS | Torrent | Alprazolam 0.25mg | Strip of 15 | H1 (Sched. X? no: H1, NDPS-controlled) | 5 | 3004 |
| Clonotril 0.5 / Rivotril 0.5 | CNS | Torrent / Abbott | Clonazepam 0.5mg | Strip of 10 | H1 (NDPS) | 5 | 3004 |
| Nexito 10 | CNS | Sun Pharma | Escitalopram 10mg | Strip of 10 | H | 5 | 3004 |
| Pregabalin (Pregeb M 75) | CNS | Intas | Pregabalin + Methylcobalamin | Strip of 10 | H | 5 | 3004 |
| Gabapin NT | CNS | Intas | Gabapentin + Nortriptyline | Strip of 15 | H | 5 | 3004 |
| Vertin 16 | CNS | Abbott | Betahistine 16mg | Strip of 15 | H | 5 | 3004 |
| Stemetil MD | CNS | Abbott | Prochlorperazine 5mg | Strip of 10 | H | 5 | 3004 |
| Levipil 500 | CNS | Sun Pharma | Levetiracetam 500mg | Strip of 10 | H | 5 | 3004 |
| Eptoin 100 | CNS | Abbott | Phenytoin 100mg | Strip of 10 (verify) | H | 5 | 3004 |
| Wysolone 10 | Steroid | Pfizer | Prednisolone 10mg | Strip of 10 | H | 5 | 3004 |
| Medrol 4 / 8 | Steroid | Pfizer | Methylprednisolone | Strip of 10 | H | 5 | 3004 |
| Dexona Tablet / Injection | Steroid | Zydus | Dexamethasone | Strip of 30 / Ampoule 2ml | H | 5 | 3004 |
| Defcort 6 | Steroid | Macleods | Deflazacort 6mg | Strip of 10 | H | 5 | 3004 |
| Febustat 40 / Zyloric 100 | Gout | Sun / GSK (Aspen) | Febuxostat / Allopurinol | Strip of 10/14 | H | 5 | 3004 |
| Chymoral Forte | Pain/Inflammation | Torrent | Trypsin + Chymotrypsin | Strip of 20 | OTC/H (verify) | 5 | 3004 |
| Myospaz Forte | Pain/Inflammation | Win-Medicare | Chlorzoxazone + Diclofenac + Paracetamol | Strip of 10 | H | 5 | 3004 |
| Thiocolchicoside (Myoril 4) | Pain/Inflammation | Sun Pharma (verify) | Thiocolchicoside 4mg | Strip of 10 | H | 5 | 3004 |
| Ostocalcium Syrup | Vitamins | GSK | Calcium + Vitamin D3 | Bottle 200ml | OTC | 5? | 3004 |
| Practin / Cyproheptadine syrup | Appetite | Merck / others (verify) | Cyproheptadine | Bottle 200ml | H | 5 | 3004 |
| TT injection (Tetanus toxoid) | Vaccine | Serum Institute / Bharat Serums | Tetanus toxoid | Ampoule 0.5ml | H (cold chain) | 5? | 3002 |
| Sterile disposable syringe 2ml/5ml (Dispovan) | Consumable | Hindustan Syringes (HMD) | Syringe | Piece | OTC (device) | unknown | 9018 (verify) |
| Glucometer strips (Accu-Chek Active) | Device | Roche | Test strips | Pack of 50 | OTC (device) | unknown | 3822 (verify) |
| Digital thermometer (Dr. Morepen / Omron) | Device | Various | Thermometer | Piece | OTC (device) | unknown | 9025 (verify) |
