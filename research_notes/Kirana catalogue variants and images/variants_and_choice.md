# Item options/variants and who chooses them (Grocery Khata)

Note on method: 8 web searches (no full-page fetches). Points that are only my own knowledge or inference are labelled as such and are not cited.

## How should variants be modelled (product -> variants vs flat SKUs; loose items)?

### Takeaway
The usual model is a parent product with a few option axes and one variant (SKU) for each combination. Each variant carries its own price, MRP, barcode and stock. Shopify is the reference: up to 3 options and 2,048 variants per product. Loose goods are best handled as a variant with a price per unit (per kg) and a quantity you can type in. Barcodes for loose goods come from in-store PLU codes or GS1 variable-measure codes.

### Cited Findings
- Shopify raised the variant cap from 100 to 2,048 per product for all merchants (developer changelog, Oct 15 2025). — [Shopify dev changelog](https://shopify.dev/changelog/the-product-variant-limit-is-now-2048-for-all-merchants); [Shopify blog](https://www.shopify.com/blog/2048-variants)
- Shopify still allows at most 3 options (axes) per product. — [Shopify Community](https://community.shopify.com/t/2k-variants-now-generally-available-to-all-merchants/570765); [Matrixify limits](https://matrixify.app/documentation/shopify-limits/)
- The Shopify data-warehouse schema has a separate product_variants table. A variant is its own row (price, SKU, barcode) linked to a product. — [Shopify product_variants schema](https://shopify.dev/docs/apps/data-warehouse/reference/schema/product_variants)
- GS1 rule for variable-measure retail items (loose or weighed): the barcode must carry either the price or the quantity. — [GS1 support](https://support.gs1.org/support/solutions/articles/43000734396-what-is-a-variable-measure-trade-item-); [GS1 UK](https://gs1uk.org/support-training/help-to-use-and-implement-gs1-standards/barcode-variable-measure-items); [GS1 NL 2x prefix code](https://www.gs1.nl/en/barcodes/article-codes/variable-priceweight-code)
- Oracle Retail derives quantity by dividing the price in the barcode by the system's unit price. — [Oracle docs](https://docs.oracle.com/cd/F32622_01/doc.191/f32616/item.htm)
- Grocery POS systems (for example ERPNext) give loose goods without a barcode a PLU code. — [ecosire ERPNext grocery POS](https://ecosire.com/zh/apps/erpnext/erpnext-supermarket-grocery-pos)

### Inferences
- Suggested layers for Grocery Khata:
  - CommonItem: the generic item ("Atta").
  - Option (a shared or shop-defined variant): brand, pack size and unit (5 kg), packaging (pouch or bottle), optional EAN and MRP.
  - ShopOption: the shop's price, availability and whether it is the default.
- Loose: a "Loose" option with unit = kg and is_loose = true. Its price is per kg, the quantity is a free decimal and the line total is qty × rate.
- Keep the axes to brand + size (+ packaging/flavour). For groceries that is enough. Shopify's 3-axis cap shows that more is rarely needed.
- Normalise the size into a number and a base unit (5000 g, 1000 ml). That enables unit-price display and "similar size" matching for substitutes.

### Gaps
- I did not fetch Shopify's or Square's variant docs in full. Square's item/variation model was not checked.

## How do Indian quick-commerce apps show multiple pack sizes?

### Takeaway
I found no citable teardown of the Blinkit, Zepto, Instamart, BigBasket, JioMart or Flipkart Grocery option UI. This section is mostly a gap.

### Cited Findings
- General e-commerce guidance: a variant can be chosen on the card itself through a dropdown or button/chip selectors. — [Maropost KB](https://galaxy.maropost.com/kb/articles/2187-variant-selector-for-add-to-cart)
- Merchants complain about the extra pop-up step when a product with several sizes is added to cart. — [Cornerstone theme feedback](https://cornerstone-theme.nolt.io/87)
- Zepto UX commentary warns against covering the key call-to-action buttons with modals or snackbars. — [Tear Them Down substack](https://tearthemdown.substack.com/p/zepto-prop)

### Inferences
- Uncited recollection, to be checked in the apps: Blinkit, Zepto and Instamart cards show the default pack with an "N options" label next to ADD. Tapping it opens a bottom sheet that lists each pack with its size, price, MRP strike-through and its own +/- stepper. BigBasket has historically used a pack-size dropdown on the card.
- For Grocery Khata: show the default option on the card, an "N options" chip that opens a bottom sheet, and a "Let shop choose" row at the top of that sheet.

### Gaps
- Needs a hands-on screenshot audit of each app (no reliable sources found).

## How do substitution flows work end to end?

### Takeaway
The common pattern has three steps:
1. Before checkout, the customer sets a preference for each item: best match by shopper, a specific item, or refund/don't replace.
2. During picking, the customer gets a live approve/decline prompt.
3. Billing follows a stated price rule. The rule is either "charge the substitute's price" (Walmart) or "charge the original price for an upgrade" (Woolworths NZ), with caps on the price difference (Deliveroo).

### Cited Findings
- Instacart offers three options: Best Match (the shopper picks), Specific Item, and Refund. The choice is set for each item in the cart ("Choose replacement"). If nothing is set, the default is to find a replacement. The customer is notified and can approve or decline. — [Instacart shopper community](https://company.instacart.com/shopper-community/providing-a-more-straightforward-replacements-experience); [Instacart help](https://sameday.staples.com/help/section/360007902831/360039162252)
- Instacart's guarantee counts a "poor replacement" refund only for a replacement the customer did not specifically approve. — [Instacart quality guarantee](https://instacart.ca/help/article/quality-guarantee-terms)
- Deliveroo partners set a Max Price (above it, no substitution) and a Max Price Difference allowed in either direction. — [Deliveroo FAQ](https://help.deliveroo.com/en/articles/6404744-faqs-item-substitutions-grocery-partners-only)
- Walmart Canada charges the substitute's price. A declined substitute, or no substitute, is not charged. — [Walmart CA](https://www.walmart.ca/en/help/article/substitutions/cff56618f0d744ed87046ade05792161); [Supermarket News](https://www.supermarketnews.com/retail-financial/walmart-online-shoppers-pay-list-price-product-substitutions)
- Woolworths NZ substitutes with an item of equal or higher value but charges the original price. Claims must be made within 48 hours. — [Woolworths NZ](https://www.woolworths.co.nz/help/policies/store-policies)
- DoorDash also has customer substitution preferences. — [DoorDash help](https://help.doordash.com/en-ca/consumers/article/what-are-customer-substitution-preferences)
- Baymard has UX research on grocery substitutions. — [Baymard](https://baymard.com/research-articles/grocery-substitutions)

### Inferences
- Grocery Khata line states:
  - requested: generic item and quantity, or a specific option
  - shop-proposed option
  - accepted, rejected or auto-accepted
- The household sets a preference for each item, or one default for the whole order:
  - "Shop chooses any brand"
  - "Ask me (WhatsApp or call)"
  - "Don't replace"
- The bill uses the option actually packed at the shop's price. If the price changes by more than a threshold (for example ±10% or ₹20), the customer must approve before it posts to the khata. A rejected line drops off the bill. This fits the existing ledger and dispute model: an unapproved substitution can turn into a dispute.

### Gaps
- No primary BigBasket, JioMart, Dunzo or Amazon Fresh substitution pages were found. Dunzo has largely shut down, but I did not verify that.

## How do small-shop tools handle variants, MRP vs selling price, barcode lookup and quick-add?

### Takeaway
Indian SMB tools take a light approach. Dukaan stores MRP and selling price for each size or colour variant and has a bulk template with variant weight and unit. Vyapar ties barcodes to the item code and accepts a manufacturer EAN typed in as that code. GS1 India DataKart is the national product master: 14M+ products, with API and GTIN-validation access for retailers and POS vendors.

### Cited Findings
- Dukaan: "Add Variants", then Size, where each size gets a name, MRP and selling price. The bulk upload template has variant weight and weight-unit columns, and each column must use one variant type. — [Dukaan size/colour](https://dukaan.help.center/article/5006-how-to-add-size-and-color-options-to-products); [Dukaan bulk](https://dukaan.help.center/article/5171-how-to-add-products-in-bulk)
- Vyapar builds the barcode from each item's unique item code. An existing manufacturer barcode can be stored as the item code, and standard 1D scanners are supported. — [Vyapar guide](https://vyaparapp.in/guides/how-to-generate-a-barcode-in-vyapar)
- DataKart is GS1 India's product data platform for brand owners, e-commerce, retailers and kirana stores. It is reached by API from ERP/POS software and offers GTIN validation against its database. It has been linked to Verified by GS1 since 2019 and holds over 14 million products. GS1 does not vouch for the accuracy of brand-uploaded data. — [GS1 India DataKart](https://gs1india.org/datakart/); [GTIN validation](https://www.gs1india.org/gtin-validation/); [GS1 UK article](https://www.gs1uk.org/insights/news/GS1India-building-trust-and-boosting-efficiency-with-verified-product-data)
- BigBasket uses GS1 India trusted product data. — [GS1 case study](https://gs1.org/insights-events/case-studies/benefits-trusted-product-information-bigbasket-and-other-indian-retailers-and)

### Inferences
- Quick-add flow: the shop picks a common item and sees suggested options (from a shared master that other shops add to). The shop ticks the ones it stocks and types its price. Alternatively it scans an EAN, which is looked up in the shared master and then in Open Food Facts or DataKart, and is mapped to a common item. Selling price defaults to MRP minus a discount, and the price is warned or blocked if it is above MRP.

### Gaps
- Khatabook, Marg, Gofrugal, JioMart Partner and Vyapar variant or loose-item docs were not found in this pass. Open Food Facts API coverage of Indian products and DataKart API pricing were not checked.

## Legal Metrology points relevant to options

### Takeaway
Since the 2022 amendment, India no longer mandates standard pack sizes, so any pack size is legal. Packages must declare MRP and, since the 2021 amendment, a unit sale price. Combination, group and multi-piece packs were exempted from unit price from 1 Jan 2024. Selling above MRP is not allowed (well known, but not cited here).

### Cited Findings
- The 2022 amendment omitted Rule 5 and Schedule II (standard pack quantities). Sources give conflicting effective dates (Oct 2022, Apr 2023 and Jun 2023). — [S.S. Rana](https://ssrana.in/articles/legal-metrology-packaged-commodities-amendment-rules-2022-2/); [KS&K](https://ksandk.com/legal-metrology/packaged-commodities-rules-2022/)
- The 2021 amendment requires a unit sale price alongside MRP. Combination, group and multi-piece packages have been exempt since 1 Jan 2024. — [KS&K](https://ksandk.com/legal-metrology/packaged-commodities-rules-2022/); [Lexplosion](https://lexplosion.in/amendments-to-legal-metrology-packaged-commodities-rules-2022-kick-in-extends-timeline-to-comply-with-new-labelling-requirements-till-01-10-2022/)

### Inferences
- Store the MRP and the net quantity for each packed option, and show ₹/kg or ₹/L to make comparison easier. Loose items sold by weight are not "pre-packaged", but the weighing must be honest. In-app, the quantity should be the one actually weighed.

### Gaps
- Gazette text not verified. E-commerce rules on displaying MRP were not checked.

## Recommended design for Grocery Khata (both flows)
These are inferences that combine the sections above.
- **Data model.** Three layers:
  - CommonItem.
  - Option: brand, size value and base unit, packaging, flavour, is_loose, EAN, MRP. Options are shared across shops.
  - ShopOption: the shop's price, in-stock flag and default flag.
- **Customer flow A (specific).** The card shows the default option and an "N options" chip. The chip opens a bottom sheet with each option's brand, size, price, MRP and ₹/kg. The loose option takes a typed kg amount.
- **Customer flow B (generic).** The customer adds "Atta, 5 kg" with no option, or with "any brand", plus a substitution preference.
- **Shopkeeper packing.** Each generic line shows the shop's options for that item, sorted by closeness of size and then by the household's past purchases. The shopkeeper taps one, or scans an EAN to resolve it. If a specific option is out of stock, the shop proposes a substitute.
- **Approval and price.** The final price is the packed option's shop price.
  - Auto-accept when the customer chose "shop chooses" and the price difference is within the threshold.
  - Otherwise the customer gets a prompt to accept or reject, with WhatsApp or call as fallback.
  - Nothing posts to the khata until it is accepted or auto-accepted, and later complaints go through the existing dispute flow.
- **Shop quick-add.** Tick suggested options from the shared master, scan to add, and choose "loose per kg" as an option type.
