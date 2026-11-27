# How retail catalogue platforms link taxonomy, master product, seller listing and storefront

Research date: 2026-11-27. Network limits in this session: help.shopify.com, sellercentral.amazon.com, docs.google.com and GitHub mirror hosts could not be fetched directly. Findings for those sources come from search-engine summaries of the pages, not full reads, and are marked as such. github.com was readable.

## ONDC: retail domains, category attributes, seller catalogue mapping, buyer app display

### Takeaway
ONDC splits retail into numbered domains (RET10 grocery, RET11 F&B, RET12 fashion, RET13 BPC, RET14 electronics, RET16 home & kitchen). Each domain has a category list with mandatory and optional attributes per category, plus statutory-requirement blocks. Sellers publish their own catalogue (items with `category_id` and `parent_item_id` for variants) in the `on_search` response. Buyer apps group what they show by domain and category. I could not read the official taxonomy file in this session.

### Cited Findings
- Domain codes as listed in an ONDC GitHub org description (seen through a mirror): grocery RET10, food & beverages RET11, fashion RET12, beauty & personal care RET13, electronics RET14, home & decor RET15. — [ONDC Official GitHub (mirror)](https://github.com/ONDC-Official)
- A category-attribute spreadsheet in ONDC circulation labels RET16 "Home & Kitchen". Under it, items such as Stationery carry the statutory block `@ondc/org/statutory_reqs_packaged_commodities`. — [ONDC category attribute sheet (Google Sheets export)](https://docs.google.com/spreadsheets/d/1APAvavF_BNbTA89benAlGtv0GuFvpn2b6XXi4lSdTTw/export?format=pdf)
- The same sheet marks each attribute per category as M (mandatory) or O (optional), with "*" where suggested enums exist. It includes attributes such as size chart for fashion, and statutory tags for food and packaged goods. — [ONDC category attribute sheet](https://docs.google.com/spreadsheets/d/1APAvavF_BNbTA89benAlGtv0GuFvpn2b6XXi4lSdTTw/export?format=pdf) (seen only through a search summary; not an official spec page)
- Sources disagree on RET14 and RET15. One ONDC-related page lists RET14 as Home & Decor, while the GitHub org text lists RET14 as electronics and RET15 as home & decor. — [ONDC Official GitHub](https://github.com/ONDC-Official); [ONDC issue-logging wiki](https://ondc-issue-logging-cohort1.atlassian.net/wiki/x/DoBAB)
- In an ONDC inventory-less retail sample `on_search`, each item carries a GTIN/EAN/ISBN code and a `parent_item_id`. This is how variants are grouped under a parent. — [ONDC doc (Google Docs export)](https://docs.google.com/document/d/11lRZErGXPDHTJcP97NXIT7_VgzTGf4_Ixh_OMJS4eJ4/export?format=pdf) (search summary only)
- ONDC developer pages link a separate Retail taxonomy for each B2C and B2B spec version, and the enums sit in the protocol-network-extension `enums/retail` folder. — [search summary of ONDC developer guide references](https://gh.hhj520.top/ONDC-Official)
- A Catalog & Store Rejection Framework applies to the Retail B2C domain, so buyer apps or the network can reject non-conforming catalogue items or stores. — [ONDC Official GitHub (mirror)](https://github.com/ONDC-Official)
- The ONDC-RET-Specifications repo currently shows B2B Retail v2.0.2 (branch `release-2.0.2`) and B2C Retail Exports v2.0 (branch `b2c_exports_2.0`). It is built on the Beckn protocol, and its seller-side APIs cover inventory management. — [ONDC-RET-Specifications](https://github.com/ONDC-Official/ONDC-RET-Specifications)
- Healthcare and agriculture services sit outside the retail RET domains, under separate services domains. — [Assam BRAP ONDC annexure](https://eodb.assam.gov.in/assets/docs/ondc/annexure_a_brap_ondc_reforms_v3_1.pdf)

### Inferences
- ONDC has no shared master product record that sellers attach to. Each seller app sends its own items, tagged with a domain and a category from ONDC's fixed list, and fills in the attributes that category requires. The "master" is the taxonomy and attribute rules, not a product database. A GTIN, where present, is the only cross-seller product key.
- For the billing app, each common-list item could carry an ONDC domain code and category. The app could then export a shop's items to ONDC and fill the mandatory attributes for that category (for example veg/non-veg and FSSAI details for grocery, size, colour and size chart for fashion).
- Medical, hardware, paint, electrical and jewellery shops do not map cleanly to RET10–RET16 in what I could verify. Check the official enum list for codes such as RET18 or RET19 before relying on any mapping.

### Gaps
- I could not open the official taxonomy file or the attribute sheet in full, so the complete domain list (RET17, RET18, RET19 and any others) and their meanings are not verified.
- The RET14/RET15 conflict is unresolved.
- I found no description of how buyer apps (Paytm, Magicpin and others) render category menus from ONDC data.

## Amazon / Flipkart / Meesho: master product vs seller offer, categories, attributes, variations

### Takeaway
All three marketplaces separate one shared product record (Amazon ASIN, Flipkart FSN) from each seller's offer (the seller's SKU, price, stock and delivery time). A seller selling something already in the catalogue "matches" or "latches" to it and sets only offer fields. Content changes to the shared page go through "suggest changes" or brand-owner control, not direct edits. Variations use a parent record with child items, tied together by a variation theme (size, colour) that must stay consistent across children and with their category.

### Cited Findings
- Amazon policy: if a product is already in the catalogue, the seller matches it to the existing ASIN by creating an offer. A new ASIN is created only when the product is absent. Changing a parent or child page so it no longer describes the original product, or so the parent no longer matches its children, is prohibited. — [Amazon ASIN creation policy](https://sellercentral.amazon.com/help/hub/reference/external/G201844590) (search summary; direct fetch blocked)
- Amazon India: search by UPC/EAN, name, model number or brand. If your product is an exact match, add your price and quantity to the existing ASIN. — [Amazon India: List your products](https://sell.amazon.in/sell-online/list-your-products)
- Detail pages are shared with every seller of the same product. Each seller controls their own offer (price, shipping, condition). — [Amazon: Product detail pages and offers](https://sellercentral.amazon.com/gp/help/external/help.html?itemID=51)
- An offer on a restricted brand's existing ASIN may need approval from Amazon or the brand owner. — [Productsup: Amazon offer creation export](https://help.productsup.com/docs/help-center/export-data-feeds/add-export/add-amazon-offer-creation-export)
- Amazon supports more than one offer or SKU per ASIN for one seller (for example different fulfilment channels). — [Amazon SP-API: multiple-offer use cases](https://developer-docs.amazon.com/sp-api/docs/manage-advanced-multiple-offer-multiple-fulfillment-use-cases)
- Amazon variations: you add a child to an existing parent using the parent ASIN, and the variation theme must match the parent's. — [eComEngine](https://www.ecomengine.com/sell-on-amazon/add-variation-existing-amazon-listing)
- Seller forum reports (anecdotal) say a child can come loose from its parent when its category drifts from the parent's, and that correcting the category reattaches it. Merges are also rejected when brand or attributes do not match. — [Amazon seller forums UK (category)](https://sellercentral.amazon.co.uk/seller-forums/discussions/t/93ea070a-7950-4026-9aef-03cb06bf5af1); [Amazon seller forums UK (merge)](https://sellercentral.amazon.co.uk/seller-forums/discussions/t/d63b89ba-9e95-46af-b3f3-4cc241946c9e)
- Flipkart: the FSN identifies the product in Flipkart's catalogue. A listing holds what the buyer sees before ordering: stock, price and delivery time. Each listing is keyed to the seller's own SKU ID. — [Flipkart Listing Management API glossary](https://seller.flipkart.com/api-docs/glossary.html); [Flipkart LM API overview](https://seller.flipkart.com/api-docs/listing-api-docs/LMAPIOverview.html)
- Flipkart "latch to existing catalogue": search by brand, FSN or product URL, pick the category, confirm the product, and submit for approval. Bulk latching needs brand approval. Mandatory fields are product link, seller SKU ID, MRP, selling price, procurement SLA, stock, and stock available for buyers. — [Fynd: latching single listing from existing Flipkart catalog](https://documentation.fynd.com/commerce/docs/openapi/listings-management/flipkart/existing-products); [Fynd: bulk existing](https://documentation.fynd.com/commerce/docs/openapi/listings-management/flipkart/bulk-existing)
- Flipkart rules: selling price may not exceed MRP, a seller may not list the same product twice (use quantity instead), and duplicate "similar product" listings are not supported. — [Fynd Flipkart FAQ](https://documentation.fynd.com/commerce/docs/openapi/listings-management/flipkart/faq-flipkart)
- Through the Flipkart API, sellers can get and update stock at location level, and update listing attributes such as selling price, MRP and procurement SLA. — [Flipkart LM API reference](https://seller.flipkart.com/api-docs/listing-api-docs/LMAPIRef.html)
- Meesho: bulk upload uses an Excel template for the chosen category, filled with that category's attributes, followed by validation and QC. Single upload follows the same flow in the Catalog Upload section. — [Streamoid Meesho seller guide](https://streamoid.com/guide/meesho-seller-guide); [Meesho learning hub: QC errors](https://supplier.meesho.com/learning-hub/lessons/how-to-find-and-fix-qc-errors)
- A Meesho "catalog" groups related products or variants under one entry. In the template each size or colour is its own row, and all variants share the same main image link (unofficial sources). — [Base.com blog](https://base.com/en-IN/blog/?p=21018); [Slideshare: catalogue upload](https://www.slideshare.net/slideshow/upload-a-new-product-248988481/248988481)
- Meesho QC error types include Wrong Category, Duplicate SKU ID/Product ID, Image Link, Product Description and Image Upload. A catalogue goes live only after it passes QC. — [Meesho learning hub](https://supplier.meesho.com/learning-hub/lessons/how-to-find-and-fix-qc-errors)

### Inferences
- The billing app can copy this split directly. A master item record (name, brand, pack size, HSN, GST rate, images, category, barcode) is shared. A shop item record (shop SKU, selling price ≤ MRP, stock, online on/off) holds the master item's ID. "Add from common list" is the equivalent of Flipkart latching or Amazon matching.
- Shared content (title, image, HSN) should be owned by the master record. Shop-level changes to it should be either local overrides or "suggest a correction" requests, never edits to the shared record.
- Category-specific attribute templates (Meesho, ONDC) suggest that each category in the common list should declare which attributes it needs: size and colour for clothes, shade and volume for paint, purity and weight for jewellery, batch and expiry for medical.
- Variation family: one parent (for example "Cotton kurta, style X") with children per size and colour. Children must share the parent's category and variation theme.

### Gaps
- I could not read the official Amazon help pages directly, so the exact list of fields an offer-only seller may set or suggest changing (title, bullet points and so on) in India is unconfirmed.
- I found no detailed Amazon browse-node or category-template (flat file per product type) documentation from 2025–2026 in this session.
- Meesho's official colour and size field definitions are unverified.
- The Flipkart cap of 25 stock units per listing, seen in an old API glossary, may be outdated.

## Shopify Standard Product Taxonomy and Google Product Taxonomy: IDs, versions, attributes, collections

### Takeaway
Both use a single standard category tree with stable IDs. Each product gets exactly one standard category, which drives tax and marketplace or channel mapping. Store navigation comes from a separate, merchant-defined layer: Shopify collections, Google `product_type`. A product can appear in many collections, and collections can be rule-based, including "this category and all its descendants".

### Cited Findings
- Shopify's taxonomy is open source (MIT) on GitHub. The source of truth is `data/categories/`, `attributes.yml`, `values.yml`, `return_reasons.yml`, `integrations/` (mapping rules to other taxonomies) and `localizations/`. It covers 25+ verticals. — [Shopify/product-taxonomy](https://github.com/Shopify/product-taxonomy)
- Versioning is CalVer, synced to Shopify's API releases (for example `2024-07`, `2024-10`). Stable releases come at most once a quarter, and an `unstable` release tracks `main`. The repo badge showed version 2026-11 at the time of reading. Distribution files are `txt` or `json` gzip files per locale, and the committed `dist/` folder is deprecated, with removal scheduled for 31 Oct 2026. — [Shopify/product-taxonomy](https://github.com/Shopify/product-taxonomy)
- The taxonomy has more than 10K categories and about 2K attributes. It has been in the Admin API since 2024-04 through `taxonomy.categories`, and each category reports whether it is root, leaf or archived and lists its own category-specific attributes. — [Shopify changelog: Taxonomy API](https://shopify.dev/changelog/taxonomy-api.md); [Shopify blog](https://shopify.com/blog/shopify-taxonomy)
- The October 2024 release (v2024-10) added categories, attributes and values. — [Shopify dev changelog Oct 2024](https://shopify.dev/changelog/updates-to-shopify-s-standard-product-taxonomy-october-2024)
- Each Shopify product can have only one standard product category, and that category is used for tax calculation and some sales channels. Checkout on Instagram and Facebook still needs Google product categories for tax. — [Shopify Help: product category](https://help.shopify.com/en/manual/products/details/product-category) (search summary)
- Category attributes appear as "category metafields" on the product. — [Shopify Help: Add category metafields](https://help.shopify.com/en/manual/custom-data/metafields/category-metafields/add-category-metafields)
- Automated (smart) collections can use product category as a condition, matching all conditions or any one, but not a mix. — [Shopify Help: automated collection conditions](https://help.shopify.com/manual/products/collections/automated-collections/auto-sort)
- The rule column `PRODUCT_CATEGORY_ID_WITH_DESCENDANTS` matches products assigned to a category or any of its descendants. — [Shopify dev changelog](https://shopify.dev/changelog/introducing-productcategoryidwithdescendants-in-collectionrulecolumn-for-smart-collections)
- Shopify's newer collections model combines rule-based conditions, manual picks and sub-collections. — [Shopify dev: new collections model](https://shopify.dev/docs/apps/build/product-merchandising/products-and-collections/use-new-collections-model)
- Google Merchant Center: `google_product_category` accepts either the numeric ID (for example 4990) or the full path from Google's taxonomy, not both. Google assigns a category automatically, and the attribute overrides that. Use English values or the numeric ID if the taxonomy is not available in your language. — [Google Merchant Center: google_product_category](https://support.google.com/merchants/answer/6324436?hl=en)
- `product_type` is the merchant's own taxonomy, matching store navigation, unlike Google's fixed category list. — [Google Merchant Center: product_type](https://support.google.com/merchants/answer/6324406?hl=en)
- Variant grouping uses `item_group_id`. All products sharing an item group ID must use the same variant attributes. — [Google Merchant Center: item_group_id](https://support.google.com/merchants/answer/6324507)
- Dukaan exports a product feed for Facebook Shops and Google Merchant Center, so Indian storefronts already map to Google's taxonomy downstream. — [Dukaan help: product catalog feed](https://dukaan.help.center/article/1029-product-catalog-feed-for-facebook-shops-google-merchant-center)

### Inferences
- The billing app should keep two layers. The first is the standard category: one per master item, with a stable ID and versioned, and it carries GST/HSN defaults, required attributes, and mappings to ONDC and Google. The second is shop storefront sections, which work like collections: many per item, either chosen by hand or rule-based such as "category X and below".
- Putting one item under both Women and Men, or under several departments, belongs in the storefront section layer. It should never mean giving the item two standard categories.
- For taxonomy changes, follow Shopify: dated releases, categories marked "archived" rather than deleted, and a mapping file from old IDs to new IDs that is applied to shop items in a migration.

### Gaps
- I did not verify how Shopify migrates products when their category is archived (whether reassignment is automatic or the merchant is prompted), or the exact ID format (`gid://shopify/TaxonomyCategory/...`).
- I did not confirm whether `data/integrations/` contains a Shopify-to-Google mapping.
- I did not check Google taxonomy versioning (file dates, change policy).
- Shopify help was not readable in this session. That a product can sit in many collections is standard Shopify behaviour, but this session did not confirm it from a fetched page.

## Indian quick-commerce, retail apps and POS products: picking from a central catalogue

### Takeaway
Among Indian small-shop tools, a true shared master catalogue that shops "pick from" is documented mainly for B2B or aggregator platforms (JioMart Partner, GS1 DataKart as a data source). Billing-app storefronts I could verify (Vyapar, Dukaan) use a different model: the shop creates its own items, then chooses which ones go online. Khatabook MyStore has been shut down.

### Cited Findings
- JioMart Partner: kiranas order stock through the Partner app. Those that keep a set level of ordering and inventory on JioPOS and can do home delivery are integrated into the JioMart consumer app as "JioSmart Kirana" stores. — [The News Mill, Jul 2021](https://thenewsmill.com/2021/07/jiomart-partner-guwahati-benefit-grocery-stores/)
- An older low-authority source says JioMart shows a catalogue of items for kirana owners to list online inventory, add products and run deals. — [FreshersLive (c. 2020)](https://news.fresherslive.com/articles/jiomart-app-place-order-through-jiomart-whatsapp-number-127015)
- Khatabook discontinued its MyStore shopfront app from 15 Nov 2021. — [Inc42](https://inc42.com/buzz/khatabook-pulls-the-plug-on-digital-shopfront-platform-mystore-within-a-year-of-its-launch)
- Vyapar online store: the shop adds items in the billing app (with images), then goes to "My Online Store" and adds products to it, within the item limit of its plan. The store is shared as a link. — [Vyapar guide](https://vyaparapp.in/guides/how-to-create-an-online-store-in-vyapar); [Vyapar blog](https://vyaparapp.in/blog/start-online-store-vyapar/)
- Dukaan adds products one at a time or in bulk (CSV), and "Magic Upload" (2021) moves inventory into the store. I found no Dukaan "master catalogue". — [Dukaan help: add product](https://dukaan.help.center/article/5000-how-to-add-a-single-product); [Dukaan bulk](https://dukaan.help.center/article/5010-how-to-add-products-in-bulk); [Business Standard](https://business-standard.com/article/companies/dukaan-simplifies-inventory-management-with-magic-upload-121032500879_1.html)
- Some Indian commerce platforms do use a master catalogue with fallback. Unizap documents adding a product "that is not available in the master catalogue", and Shiprocket has "import master catalog". — [Unizap KB](https://unizap.zohodesk.in/portal/en/kb/articles/bulk-product-upload); [Shiprocket KB](https://www.shiprocket.in/knowledgebase/import-master-catalog-shiprocket)
- GS1 India DataKart is the national product master-data repository. Brand owners load GTIN (EAN/UPC), HSN code, FSSAI licence number, images, and size and weight data. Brands can link GTIN to HSN. Retailers and marketplaces (GS1 names Amazon, Flipkart, More, BigBasket, Reliance and JioMart) draw product data from it. Login uses the GS1 company prefix. — [GS1 India DataKart for retailers](https://gs1india.org/datakart/datakart-for-retailers); [DataKart for brand owners](https://gs1india.org/datakart-for-brand-owners); [GS1 global case study (BigBasket)](https://gs1.org/insights-events/case-studies/benefits-trusted-product-information-bigbasket-and-other-indian-retailers-and)
- GS1 India's Smart Consumer app shows product information from this data to shoppers. — [GS1 India DataKart blog](https://www.gs1india.org/blog/understanding-gs1india-datakart) (search summary; GS1's own marketing)

### Inferences
- A common list keyed by barcode (GTIN) and seeded from DataKart-style data would give HSN, images and pack size for branded FMCG, BPC, medical OTC, paint and electrical items. Loose and unbranded items (kirana staples, tailored clothes, jewellery) need list entries without a GTIN, or shop-created items.
- Following Vyapar, a separate "show in online store" step per item after it exists in billing, rather than automatic publication, matches what Indian shopkeepers already use.

### Gaps
- I found no current (2025–2026) official documentation of JioMart Partner's catalogue-pick flow, Swiggy Instamart's seller or brand catalogue model, or DataKart API access terms and pricing.
- Swiggy Instamart was not researched successfully in this session.

## Patterns: overrides, master updates, links, multi-membership, versioning, visibility

### Takeaway
Across platforms the pattern is the same. A shared product record owns the identity fields, and the seller or shop record holds a pointer to it plus its own commercial fields. Navigation groupings are many-to-many and separate from the one standard category. Taxonomies are versioned, with archive-and-map rather than delete. Items go live only after validation gates.

### Cited Findings
- **Link from shop item to master item:** a Flipkart listing is keyed by the seller's SKU and points to the FSN. An Amazon offer is created against an ASIN. ONDC items can carry a GTIN and `parent_item_id`. — [Flipkart glossary](https://seller.flipkart.com/api-docs/glossary.html); [Amazon India list products](https://sell.amazon.in/sell-online/list-your-products); [ONDC sample doc](https://docs.google.com/document/d/11lRZErGXPDHTJcP97NXIT7_VgzTGf4_Ixh_OMJS4eJ4/export?format=pdf)
- **What a seller may override:** offer fields only. Amazon: price, quantity, condition and shipping. Flipkart: MRP, selling price, SLA and stock. Shared page content may not be changed so that it describes a different product. — [Amazon detail pages and offers](https://sellercentral.amazon.com/gp/help/external/help.html?itemID=51); [Fynd Flipkart existing](https://documentation.fynd.com/commerce/docs/openapi/listings-management/flipkart/existing-products); [Amazon ASIN policy](https://sellercentral.amazon.com/help/hub/reference/external/G201844590)
- **Master data flowing down:** DataKart holds GTIN-to-HSN links and images for retailers to consume, so a brand-side change can reach every retailer from one place. — [GS1 India DataKart](https://gs1india.org/datakart/datakart-for-retailers)
- **Multi-membership:** Shopify gives one standard category per product but supports manual and rule-based collections, including category-with-descendants rules. Google separates the fixed `google_product_category` from the merchant's own `product_type`. — [Shopify changelog](https://shopify.dev/changelog/introducing-productcategoryidwithdescendants-in-collectionrulecolumn-for-smart-collections); [Google product_type](https://support.google.com/merchants/answer/6324406?hl=en)
- **Taxonomy versioning:** Shopify uses CalVer quarterly releases, an archived flag on categories, and mapping rules in `integrations/`. ONDC publishes a taxonomy per spec version. — [Shopify/product-taxonomy](https://github.com/Shopify/product-taxonomy); [Shopify Taxonomy API changelog](https://shopify.dev/changelog/taxonomy-api.md)
- **Visibility gates:** Meesho catalogues go live only after QC. Flipkart latching needs approval, and brand approval for bulk. Flipkart stock must be set and selling price ≤ MRP. ONDC has a catalogue and store rejection framework. Vyapar publishes only items the shop adds to the online store. — [Meesho QC](https://supplier.meesho.com/learning-hub/lessons/how-to-find-and-fix-qc-errors); [Fynd Flipkart FAQ](https://documentation.fynd.com/commerce/docs/openapi/listings-management/flipkart/faq-flipkart); [Vyapar guide](https://vyaparapp.in/guides/how-to-create-an-online-store-in-vyapar)
- **Variation consistency:** Google requires the same variant attributes across an item group. Amazon children must match the parent's variation theme and category. — [Google item_group_id](https://support.google.com/merchants/answer/6324507); [eComEngine](https://www.ecomengine.com/sell-on-amazon/add-variation-existing-amazon-listing)

### Inferences
These are design suggestions for the billing app, built from the patterns above.
- **Shop item record:** `shop_item.master_item_id` (nullable for shop-created items). Optional overrides: `display_name`, `extra_storefront_sections[]`, `hidden`. Fields the shop always owns: price, MRP, stock, online toggle.
- **Field resolution:** when a field is shown, use the shop's override if one exists, otherwise the master value.
- **Master changes:** when HSN, GST rate, image or category changes on the master, apply it to every linked shop item that has no override of that field. Tax fields (HSN, GST) should have no override, or need confirmation, because they affect GST filing. Keep a version or `updated_at` on the master so that changes are pushed out, or reviewed by the shop, rather than applied silently.
- **Shop-level re-categorising:** let the shop place an item under extra storefront sections or rename a section. Its standard category and GST mapping stay the same. A whole category can be hidden at shop level with a flag.
- **Taxonomy migration:** never delete a category ID. Mark it archived, publish an old-to-new mapping, and run a migration that moves both master items and shop section rules.
- **Customer visibility rule (mirroring the gates above):** show an item online only when online toggle on AND price set (and ≤ MRP) AND (stock > 0 OR shop allows out-of-stock display) AND required category attributes filled AND, optionally, has a photo. Hide a category when it has no visible items or the shop hid it.

### Gaps
- I found no public documentation of exactly how Amazon or Flipkart push master-content changes (for example a corrected image or category) to existing offers, or how sellers are notified.
- I found no Indian billing app that documents shop-level overrides on top of a shared master list. This appears to be an unfilled gap rather than an established pattern.
