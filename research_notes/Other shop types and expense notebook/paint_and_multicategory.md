# Paint shops and multi-category retail billing (India, as of Oct 2026)

Research was cut short at about 12 tool calls, and an internet drop interrupted the run. Many findings come from vendor marketing or secondary sites, and each one is marked as such.

## Paint-shop needs: SKUs, tinting, painter loyalty, schemes, GST, batch

### Takeaway
A paint dealer sells two kinds of item. Some are ready-mix SKUs (product + shade + pack size). Others are tinted on demand: a base SKU plus colourants, made up on a brand tinting machine. The billing gap that matters is linking a tinted sale to the base it uses up, so the base comes off stock and the colourant cost is added to the price. Painter/contractor loyalty is mostly run by the paint brands through QR-scan apps, not by the shop's billing software.

### Cited Findings
- BUSY's paint-shop page claims all of these features: stock by shade code, colour and finish; automatic base-stock deduction when a colour is mixed; pack sizes of 1L, 4L, 10L and 20L, plus gallons and drums, with partial-container sales; stored tinting formulas or recipes chosen during billing; batches with mfg/expiry dates and blocking of expired batches; dealer schemes, volume and combo discounts; painter incentives, commissions and profit by painter; several GST rates on one bill, HSN, e-way bill and e-invoice. These are vendor claims and were not tested. — [BUSY paint shop](https://busy.in/accounting-software/paint-shop/)
- A real user request on myBillBook's feature board (27 May 2025), from a paint retailer and wholesaler. Billing a tinted shade (example: shade 0765, Apex Shyne, 20L) should deduct the matching base from stock (AY2, or the base actually used, AY11). The colourant cost (Rs 65 in the example) should be added to the material cost. He says he paid for a 5-year plan and has asked several times. The post has no official response. — [myBillBook featurebase](https://mybillbook.featurebase.app/p/need-to-changes-as-per-paint-retail-and-wholesale)
- Tinting-machine vendors (Datacolor, Chromaflo Innovatint, Fast & Fluid PrismaPro 2 POS, COROB) sell their own base + colourant dispensing software, usually bundled with the dispensing hardware. — [PrismaPro2](https://asia.fast-fluid.com/products/software/prismapro-2-pos-tinting-software); [Datacolor](https://www.datacolor.com/?p=16865)
- A rough colourant dose is 8–10% for single-pack bases, 15% for synthetics and 20% for two-pack. This comes from one manufacturer and is old (2009). — [HMG Paints](https://hmgpaint.com/news/162/new-unit-lite-gives-distributors-increased-flexibility)
- Asian Paints' tinting system ("Colour World", later "Colour Next") lets dealers offer thousands of shades without stocking each one. This comes from secondary blogs. — [Equentis](https://www.equentis.com/blog/asian-paints-the-rs-2-2-trillion-brand-that-gave-colour-to-every-indian-home/); [almonds.ai](https://almonds.ai/tag/painter-loyalty-program/)
- Painter loyalty programmes in India include Berger "Lift and Win" (cash bonuses, trips, insurance, scholarships, value tokens), Birla Opus (scan the QR coupon on each purchase), and Asian Paints Colour Academy / contractor loyalty (insurance). Detail is thin and partly comes from secondary sources. — [Berger become a painter](https://bergerpaints.com/become-a-painter); [Birla Opus/Anyline](https://anyline.com/?p=26765); [almonds.ai](https://almonds.ai/tag/painter-loyalty-program/)
- GST: paints under HSN 3208 (solvent/enamel) and 3209 (water-based/emulsion), and 3210 (distemper), dropped from 28% to 18%, effective 22 Sep 2025 (GST 2.0). Some pages still show 28% and are outdated. — [Accountune](https://www.accountune.com/hsn-code-paint); [Tally](https://tallysolutions.com/gst/paint-and-coatings-hsn-code-gst-rate/); outdated: [busy.in](https://busy.in/gst-rates/paints)
- Marg sells a "Marg ERP 9+ Hardware and Paint" edition. I found no feature detail for it. — [SoftwareSuggest](https://www.softwaresuggest.com/marg-hardware-and-paint)

### Inferences
- Model a tinted sale as a composite line: base SKU (product + base type + pack), plus a shade code (a label, not a stock item), plus a colourant charge. The base is deducted from stock and the price is base price + tint charge. This is the gap the myBillBook user was asking for.
- Shop-level painter loyalty can be simple: tag a painter/contractor on the bill, then report commission per painter. The brand QR schemes run separately in the brands' own apps.
- The 4L/10L/20L packs are separate SKUs, not unit conversions, because each pack has its own MRP.

### Gaps
- I found no official dealer documents on how brand schemes are settled (credit notes or rate differences), or on dealer credit terms.
- Whether tinting-machine software can export to POS in India is unconfirmed.
- I did not check how batch/expiry is handled in practice at paint dealers.

## How Indian billing apps handle categories, attributes, units, batch and serial

### Takeaway
Most apps use one generic item model with optional fields you switch on: batch, serial, size, model, MRP, mfg/exp, plus a secondary unit with a conversion rate. They do not have true per-category modules. Paint-specific logic such as base deduction is usually missing, and that is a real complaint.

### Cited Findings
- Vyapar has "Additional Item Fields" checkboxes for Serial No., Batch No., Model No. and Size. Batch data includes MRP, batch number, mfg/exp, size and opening qty. Each item tracks either batch or serial, not both. The cashier picks the batch or serial at sale. Advanced tracking is Premium only. — [Vyapar guide](https://vyaparapp.in/guides/how-to-add-item-batches-serial-numbers-in-vyapar)
- Vyapar lets you assign units in bulk: a base unit, plus an optional secondary unit with a conversion rate. — [Vyapar units video](https://vyaparapp.in/videos/how-to-add-inventory-units)
- The Munim uses the same pattern: turn on secondary units in settings, then set unit, secondary unit and rate per item. — [The Munim](https://themunim.com/helpdesk/how-to-use-secondary-unit-option-for-item/)
- myBillBook allows unlimited custom business fields printed on invoices. I found nothing on item variants or business-type selection. — [myBillBook KB](https://knowledge.mybillbook.in/en/articles/6861943-how-to-add-unlimited-custom-business-fields-to-invoices-in)
- Zoho Inventory item groups: you define attributes (size, colour and so on) and options, the system generates every variant combination, each variant then acts as its own item, and SKUs can be auto-generated from attributes. Composite items or assemblies bundle several items with their own stock. — [Zoho item groups](https://www.zoho.com/inventory/help/items/item-groups.html)
- Zoho community complaint: lines with the same item are merged automatically even when their custom fields or units differ. — [Zoho community](https://help.zoho.com/portal/en/community/topic/line-items-of-the-same-item-id-should-not-be-automatically-consolidated-as-it-fails-to-recognize-variation-in-custom-fields-as-well-as-distinct-items-of-the-same-id-sold-in-varying-units-of-measure?page=7)
- Khatabook reviews ask for better onboarding tutorials and multi-device access. The review samples are small. — [Techjockey](https://www.techjockey.com/reviews/khatabook); [G2](https://www.g2.com/products/khatabook/reviews)
- Vertical editions as a business model: BUSY and Marg sell editions by industry (paint shop, hardware & paint). — [BUSY](https://busy.in/accounting-software/paint-shop/); [Marg](https://www.softwaresuggest.com/marg-hardware-and-paint)

### Inferences
- Recommended design: one core item model, plus a business-type template chosen at onboarding. The template turns on certain attribute fields (paint: brand, product, base, shade, pack, finish; medical: batch/exp/schedule; clothing: size/colour variants; electrical: serial/warranty; cement: bag ↔ tonne unit). It also seeds the category's common catalogue, units and GST/HSN defaults.
- Use a JSON or EAV attribute bag per item, with an attribute definition per category template. Add a small number of real modules only where behaviour differs: composite/tint lines that deduct a component, batch/expiry FEFO, serial numbers, variant generation.
- Avoid line-merging that ignores attributes (the Zoho complaint). Let one item carry both batch and serial if needed (Vyapar's limit).
- Your existing common catalogue with per-shop prices extends naturally: one catalogue per category, so paint gets brand × product × base × pack SKUs.

### Gaps
- I could not confirm how Vyapar, myBillBook or Khatabook ask for business type at onboarding, or what that choice changes.
- Petpooja and Marg feature detail, and app-store complaint themes, were not researched before the cutoff.
