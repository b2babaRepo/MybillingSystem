# Medicine rack and shelf locations (Indian retail chemists, 2026)

## How chemists lay out stock

### Takeaway
No law sets one layout. Manufacturer-wise (company-wise) grouping is a long-standing Indian practice, often combined with dosage form. Legal rules apply only to segregation: Schedule X under lock and key or in a restricted area, cold-chain items stored as the label says, and separate records for Schedule H1.

### Cited Findings
- An Indian community pharmacy text describes stores arranging drugs manufacturer-wise, for example all Glaxo products in one cupboard — [drugxpert blog](https://drugxpert.blogspot.com/2019/04/medical-product-arrangement-community-retail-pharmacy.html)
- Stores may combine several arrangement methods. Dosage form is called the best single method, and pure alphabetical-by-brand order has drawbacks — [Remedial Health, Medium](https://medium.com/@remedialhealth/how-to-organize-and-manage-inventory-in-your-pharmacy-or-patent-medicine-store-6a808f43034b)
- Schedule X stock at retail must be stored under lock and key or in a restricted area. It needs its own licence forms (20F/21G), and records are kept for 2 years. A commercial guide says a marked carton on a separate rack, checked monthly, covers it in practice — [OkCredit 2026 guide](https://okcredit.in/blog/dos-and-donts-for-medical-store-owners/) (secondary source, commercial blog)
- Each Schedule H1 supply is recorded in a separate register: prescriber, patient, drug and quantity. H1 was added in 2013 — [GKToday](https://www.gktoday.in/schedule-h-schedule-x-and-schedule-h1-drugs/); [Wikipedia, D&C Rules 1945](https://en.wikipedia.org/wiki/Drugs_and_Cosmetics_Rules,_1945)
- Storage conditions are governed by Rule 96 and Schedule P (life period and storage), and by the label — [OkCredit](https://okcredit.in/blog/dos-and-donts-for-medical-store-owners/); [thehealthmaster](https://thehealthmaster.com/2021/11/02/duties-of-retail-chemists-medical-store-pharmacy)
- The Health Ministry has proposed mandatory CCTV at stores selling Schedule H, H1 and X drugs. This is not confirmed as in force — [BioSpectrum](https://www.biospectrumindia.com/news/73/28496/health-ministry-proposes-amendment-to-drugs-rules-1945.html)
- General tip: keep fast movers at easy reach, label every shelf and section, and keep the layout consistent so new staff can find products — [Remedial Health](https://medium.com/@remedialhealth/how-to-organize-and-manage-inventory-in-your-pharmacy-or-patent-medicine-store-6a808f43034b)

### Inferences
- The app should not assume one scheme. Locations should be free-form and per shop. "Zones" with special meaning should be optional: Fridge, Schedule X locked, H1, Expiry/Return, Godown.
- Fridge (2–8°C) and expired/return shelves are standard practice, but I found no primary source for either in this pass.

### Gaps
- No primary text found for the exact 2–8°C requirement or for a mandated expired-stock shelf.
- No survey data on how common each layout is among Indian chemists.

## Common location codes and labelling

### Takeaway
Software uses a Rack / Shelf / Box hierarchy. GoFrugal names exactly these three levels. I found no published Indian standard code format such as "R3-S2-B14".

### Cited Findings
- GoFrugal RetailEasy Pharmacy describes a numbered system for rack, shelf or box positions — [GoFrugal KB](https://community.gofrugal.com/portal/en/kb/articles/view-item-name-location-on-sales-screen-rack-shelf-box-details)
- Marg uses a single "Rack No" text field on the item master — [Marg Care](https://care.margcompusoft.com/margerp/inventory-master/107301/1/How-to-disable-rack-no.)

### Inferences
- A single short text code (e.g. "R3-S2-B14", "A-4", "Fridge") plus optional structured parts is enough. Shops copy whatever they have painted or stickered on their racks.

### Gaps
- I found no source on "dabba" drawer numbering or almirah letter conventions. These are probably real, but no documentation turned up.

## Support in existing software

### Takeaway
Marg and GoFrugal have rack fields. GoFrugal shows the rack on the billing screen. Marg supports rack-wise stock and moving stock between racks. For eVitalRx, Medeil and Vyapar I found no public documentation of a rack field. Vyapar only has godowns.

### Cited Findings
- GoFrugal: Tools > Configuration > Business Configuration > Sales > "Billing Pharma Settings – Show Item position (Rack/Shelf/Box) along with item name". The article claims this cuts location-related customer queries — [GoFrugal KB](https://community.gofrugal.com/portal/en/kb/gofrugalretaileasy/configurations/sales/articles/view-item-name-location-on-sales-screen-rack-shelf-box-details)
- Marg: there is no direct import for rack numbers. The workaround imports them through the Barcode field and renames it "Rackno" in Self Designed Reports — [Marg Care](https://care.margcompusoft.com/margerp/copy/184076/1/how-to-import-rack-number-in-all-item-of-marg-software)
- Marg: the rack no. field can be disabled. Stock is managed rack-wise — [Marg Care](https://care.margcompusoft.com/margerp/inventory-master/107301/1/How-to-disable-rack-no.)
- Marg: stock can be transferred from one rack number to another (Control Room > Search in All) — [Marg Care](https://care.margcompusoft.com/margerp/inventory/1633/1/-How-to-transfer-Stock)
- Marg has a help article titled "Rack wise indexing of items in bill", which suggests bill lines can be sorted by rack. I could not confirm the content — [Marg Care search result](https://care.margcompusoft.com/margerp/inventory-master/107301/1/How-to-disable-rack-no.)
- Vyapar: godown-to-godown stock transfer only. No rack field found — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-godowns-in-vyapar); [Vyapar pharmacy page](https://vyaparapp.in/free/inventory-management-software/pharmacy)
- eVitalRx and Medeil: listings mention inventory, expiry and multi-location features, but no rack field — [Capterra eVitalRx](https://www.capterra.com/p/237227/eVitalRx/); [Capterra Medeil](https://www.capterra.com/p/117249/Medeil/)

### Inferences
- Gaps in competitors that our app can fill: bulk import of rack numbers (Marg lacks it), rack shown on the bill or pick list, and search or filter by rack.

### Gaps
- Batch-wise location and rack-wise stocktake are not confirmed for any vendor.
- eVitalRx, Medeil and Vyapar in-app features may exist but are undocumented publicly.

## Benefits (measured or claimed)

### Takeaway
Only qualitative vendor claims were found. There is no measured data.

### Cited Findings
- GoFrugal claims fewer location queries and fewer inventory mismatches — [GoFrugal KB](https://community.gofrugal.com/portal/en/kb/articles/view-item-name-location-on-sales-screen-rack-shelf-box-details)
- A consistent, labelled layout helps new staff find products — [Remedial Health](https://medium.com/@remedialhealth/how-to-organize-and-manage-inventory-in-your-pharmacy-or-patent-medicine-store-6a808f43034b)
- Rack-wise records help staff locate items during billing (generic claim) — [Refrens](https://www.refrens.com/grow/best-billing-software-for-medical-shops/)

### Gaps
- No quantified figures for picking speed or wrong-medicine errors in Indian chemists.

## Edge cases

### Takeaway
Software evidence only covers moving stock between racks (Marg) and between godowns (Vyapar). Multi-location items, batch-level spots, unnumbered shops and renumbering are undocumented.

### Cited Findings
- Rack-to-rack transfer in Marg — [Marg Care](https://care.margcompusoft.com/margerp/inventory/1633/1/-How-to-transfer-Stock)
- Godown transfer in Vyapar, which suits the counter vs back-store case — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-and-manage-godowns-in-vyapar)

### Inferences
- Design suggestions:
  - Allow multiple locations per item, one marked as primary (counter) with the others as godown.
  - Add an optional location per batch.
  - Make the field optional so shops with no numbering are not blocked.
  - Offer bulk rename or renumber, for example "R3 → R5", plus Excel import.

### Gaps
- No chemist forum data found on how often racks get renumbered or on shops without numbering.
