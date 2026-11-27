# Category menus for Indian clothes/footwear shops and beauty/cosmetics shops (incl. salon retail)

Method note: research done 2026-11-27 using web search only. Direct page fetches of myntra.com, nykaa.com and gofrugal.com were blocked by this session's network policy, so findings come from search-result snippets of retailer pages (primary, but partial) and vendor/UX articles (marked SECONDARY). Live menus change often; treat trees as snapshots.

## 1. Clothes: how Indian fashion retailers organise categories, filters and variants

### Takeaway
Every Indian fashion marketplace found is gender/age first (Women / Men / Kids), then a department (Ethnic, Western, Footwear...), then a garment type (Kurtas, Sarees, Lehenga Choli...). Indian ethnic wear is always its own department with set-based items (kurta set, kurta-bottom set, lehenga choli set) as separate leaf categories. Filters are consistently Size, Colour, Brand, Price, Discount, Fabric, Occasion, Pattern; sleeve/neck appear only on some pages. Variants of one style are shown as one listing with colour swatches/size buttons (Baymard), and size filters work best at the top, opened by default.

### Cited Findings
**Category trees (snapshots)**
- AJIO Women > Ethnic Wear menu lists: Kurta Suit Sets, Kurta-Bottom Set, Kurtas, Kurtis & Tunics, Lehenga Choli Sets, Salwars & Churidars, Sarees. Another menu version shows Ethnic Suit Sets, Kurtas, Ethnic Jackets, Sherwani Sets (men's side). No "Dupattas" leaf found; Stoles and Shawls & Wraps sit under accessories — [AJIO women kurta sets](https://www.ajio.com/women-kurta-sets/c/830303009); [AJIO women ethnic wear](https://www.ajio.com/women-ethnic-wear/c/830303)
- AJIO also has attribute-based landing pages that act like sub-category chips: "sleeveless kurtas", "georgette kurtas kurtis", "a-line kurtas kurtis", "party wear kurta", "Anarkali", "light coloured kurtas" — [AJIO sleeveless kurtas](https://www.ajio.com/s/sleeveless-kurtas-3549-39121); [AJIO georgette kurtas](https://www.ajio.com/s/georgette-kurtas-kurtis-215137); [AJIO a-line kurtas](https://www.ajio.com/s/a-line-kurtas-kurtis-215151)
- Nykaa Fashion Women > Indian Wear uses: Sarees; Kurtis, Kurtas and Tunics; Ethnic Dresses; Lehenga Cholis — [Nykaa Fashion Indian wear](https://www.nykaafashion.com/women/indianwear/c/4?p=233)
- Flipkart URL path shows the tree directly: womens-clothing > ethnic-wear > kurtas-kurtis — [Flipkart kurtas & kurtis](https://flipkart.com/womens-clothing/ethnic-wear/kurtas-kurtis)
- Meesho: 650+ categories covering men, women, kids clothing, cosmetics, home, electronics. Women ethnic includes Sarees, Lehengas, Kurtas, Blouses, plus a separate Kurtis category (plain, printed, embroidered, long, designer), and "Suits & Dress Materials" (unstitched). Men ethnic: Kurtas, Kurta sets, suits, Sherwani sets. Saree sub-pages by work/fabric (embroidered, Banarasi silk). A Hindi-language category URL exists (meesho.com/hi/women-kurtis) — [Meesho women kurtis (Hindi)](https://meesho.com/hi/women-kurtis/pl/xd5go); [Meesho cotton suits & dress materials](https://www.meesho.com:443/cotton-women-suits-dress-materials/pl/3jk); [Meesho Banarasi silk saree](https://www.meesho.com:443/banarasi-silk-saree/pl/4hc); [Meesho ethnicwear women](https://meesho.com/ethnicwear-women/pl/5v80d)
- Myntra (press, SECONDARY): women's ethnic portfolio of 2.2 lakh+ styles; trending sub-types named: sharara sets, festive co-ords, chikankari, corset kurtis, ethnic maxi dresses, zari-work kurtas, Patiala sets, Anarkalis; saree styles: pre-draped, scalloped sheer, linen, block print, sequin — [Apparel Resources](https://apparelresources.com/business-news/retail/myntra-reports-50-growth-in-womens-occasion-wear-demand/)

**Filters**
- Flipkart kurta listing filters: Fabric, Type, Gender, Occasion, Size, Color, Brand, Pattern, Pack of, Customer Ratings, Price, Discount, New Arrivals, Country of Origin, Offers, Availability. Occasion values seen: Casual, Festive & Party, Wedding. Sleeve/neck appear as product-detail fields (with "Ideal For: Men"), not confirmed as standard listing filters — [Flipkart kurtas](https://www.flipkart.com/q/kurtas); [Flipkart men kurta product](https://www.flipkart.com/freluro-men-printed-straight-kurta/p/itm58e5719f40ca3)
- Myntra uses an "Occasion" filter (Casual, Wedding, Vacation) — SECONDARY [wizzy.ai blog](https://wizzy.ai/blog/ecommerce-filters-strategies/); Myntra packs many filters into a compact interface and uses an explicit Apply button on mobile filters — SECONDARY [Medium custx](https://medium.com/custx/filters-fb0869eec587); [Smashing Magazine](https://smashingmagazine.com/2021/07/frustrating-design-patterns-broken-frozen-filters/)
- AJIO UX critique: hard to see which filters are applied (visibility of status) — SECONDARY [Contra AJIO case study](https://contra.com/p/3PqrBYrU-ux-case-study-or-a-designathon-case-study-on-ajio)
- Baymard: putting Size at/near top of the filter sidebar, expanded by default, led 90% of test users to filter by size first; size availability is a crucial purchase factor; many apparel sites fail to label size option types (e.g. waist vs S/M/L) — [Baymard size filter](https://baymard.com/blog/apparel-put-size-filter-near-top-and-expand-for-sidebar-filtering); [Baymard size format](https://baymard.com/research-articles/apparel-how-to-format-size-options-in-the-size-filter)
- Baymard: use visual colour chips with a text label/tooltip rather than text only; users expect a Colour filter — [Baymard filter UI](https://baymard.com/learn/ecommerce-filter-ui)

**Variants of one style**
- Baymard: combine colour/size variations into a single list item with swatches under the thumbnail; separate list items per variant make it hard to find a colour — [Baymard product lists](https://baymard.com/blog/current-state-product-list-and-filtering)
- Myntra fit-picking UX case study (users rely on filters to narrow choices) — SECONDARY [Medium design-bootcamp](https://medium.com/design-bootcamp/case-study-helping-online-users-to-pick-correct-fit-in-myntra-8b2664f94404)

### Inferences
- Suggested left rail for a small clothes shop: Women, Men, Kids (Boys/Girls), Footwear, Accessories (dupattas/stoles, bags). Under Women: tiles grouped as Ethnic (Sarees, Kurtis, Kurta Sets, Salwar Suits/Dress Materials, Lehenga, Blouses, Dupattas, Leggings/Churidar/Palazzo), Western (Tops, Jeans, Dresses), Innerwear/Nightwear. Under Men: Shirts, T-shirts, Jeans, Trousers, Kurtas/Kurta sets, Sherwani, Innerwear.
- Sub-category chips can be attribute shortcuts like AJIO's pages: fabric (Cotton, Georgette, Silk, Rayon), work (Chikankari, Banarasi, Embroidered, Printed), cut (A-line, Anarkali, Straight).
- Filter order for apparel: Size first (expanded), then Colour chips, Price, Brand, Fabric, Occasion, Pattern, Discount. Keep "unstitched" vs "stitched" as a type (Meesho's dress materials) — common in small Indian shops.
- One style = one tile; size buttons and colour chips appear on the tile/sheet; grey out out-of-stock sizes.

### Gaps
- Exact live menus of Myntra, Reliance Trends, Zudio/Westside and Max Fashion could not be retrieved (sites blocked/no search hits). Myntra's filter list (Categories, Brand, Price, Color, Discount Range, Size, etc.) is from general knowledge only, not verified here.
- Footwear category trees and footwear size systems (UK/IND sizes) not sourced.
- No source found confirming how Myntra renders size-colour on listing tiles in 2025–26.

## 2. Clothes POS: how Indian shops find a style at the counter

### Takeaway
Indian apparel POS vendors (GoFrugal, Ginesys, LOGIC ERP) all centre on a style–size–colour matrix: one style master with a grid of size × colour SKUs, each with its own barcode, so the counter mostly scans; search/quick lookup is the fallback. All evidence is vendor marketing (self-reported).

### Cited Findings
- GoFrugal: item product type set to "Matrix" in Inventory > Master > Items; option "Apply Barcode Manual [Matrix]" in the Packing tab lets barcodes be entered per variant at purchase entry; separate barcode generation profiles for apparel — [GoFrugal KB: Manual Barcode for Matrix Item](https://community.gofrugal.com/portal/en/kb/articles/manual-barcode-for-matrix-item-8-1-2023)
- GoFrugal lifestyle POS: manage items under a category by size, colour, brand and other variants, "bill in a click" using item-wise barcodes; barcode-based discounts — [GoFrugal lifestyle POS](https://www.gofrugal.com/retail/lifestyle-pos/); [GoFrugal garments software](https://www.gofrugal.com/retail/apparel-pos/garments-software.html)
- Ginesys: style-size-colour matrix built natively into ERP/POS; variant-level billing and cross-store exchanges; barcodes encode size, colour, style, brand; Ginesys claims GoFrugal treats matrix as a workaround (competitor claim, weigh accordingly) — [Ginesys matrix blog](https://www.ginesys.in/blog/style-size-color-matrix-why-generic-software-fails); [Ginesys Cloud POS comparison](https://www.ginesys.in/blog/ginesys-cloud-pos-for-fashion-retail); [Ginesys barcode blog](https://www.ginesys.in/blog/navigating-modern-marketplace-with-barcode-technology-for-apparel-or-clothing-stores)
- LOGIC ERP: tracks size, style, colour, pattern by matrix; bulk matrix entry in purchase and sales; barcode per SKU; "barcode scanning and quick searches" for billing speed; alteration module, item images, exchange/returns — [LOGIC ERP apparel POS](https://www.logicerp.com/retail/pos/apparel); [LOGIC ERP blog](https://www.logicerp.com/blog/what-is-apparel-retail-software/); [LOGIC readymade garment](https://www.logicerp.com/retail/readymade-garment-retail-software)
- Comparable non-Indian model: style code = manufacturer SKU, each variant has its own code/barcode auto-generated from the style code during matrix creation — SECONDARY [Retail Express](https://retailexpress.com.au/solutions/matrix-variant-products)

### Inferences
- Counter flow to design: scan barcode (instant variant) → else type style code / name → pick from size × colour grid showing stock per cell. Category tiles are a third path for unbarcoded items.
- Variant barcode should be derived from style code + size + colour so labels can be printed in bulk at goods receipt.

### Gaps
- Zwing POS: no information found. No documentation of exact on-screen counter search UI for any vendor.

## 3. Beauty: how Nykaa, Purplle, Myntra Beauty, Tira and SUGAR organise categories and filters

### Takeaway
Indian beauty retailers use top-level departments Makeup, Skin, Hair, Bath & Body, Fragrance, Personal care, Men, plus Tools/Appliances; makeup is split by face area (Face, Eyes, Lips, Nails, Tools & Brushes). Filters combine product facts (Brand, Price, Discount, Formulation, Finish, Colour/shade, Coverage) with "who it's for" facets (Skin type, Skin tone, Concern, Hair type, Ingredient, Benefits). Salon professional lines appear as brand pages and a "Professional Products" category, with a separate trade membership (Nykaa PRO).

### Cited Findings
- Nykaa makeup splits into Face, Eyes, Lips, Nails, Tools & Brushes, plus Makeup Kits & Combos (grouped lips/eyes/face) — [Nykaa makeup](https://www.nykaa.com/makeup/c/12); [Nykaa makeup kits & combos](https://www.nykaa.com/makeup/makeup-kits-combos/c/22670)
- Nykaa listing filters seen: Price, Category, Discount, Color, Formulation, Finish, Skin type, Concern, Avg Customer Rating; Coverage and Skin tone on some pages — [Nykaa makeup](https://www.nykaa.com/makeup/c/12); [Nykaa Cosmetics brand page](https://www.nykaa.com/brands/nykaa-cosmetics/c/1937)
- Nykaa skin/hair filters also include Ingredient, Concern, Skin type, Hair type, Formulation, Benefits, Country of origin — [Nykaa editor's choice](https://www.nykaa.com/editor-s-choice/c/6981); [Nykaa best of beauty](https://www.nykaa.com/best-of-beauty/c/21447)
- Nykaa skin routine order used in category copy: face wash → serum → moisturiser → sunscreen — [Nykaa skin](https://www.nykaa.com/skin/c/8377)
- Nykaa Hair has a "Shop by Concern" category (shampoos, oils, conditioners) — [Nykaa hair concern](https://www.nykaa.com/hair-care/shop-by-concern/c/2058)
- Nykaa Skin Compass quiz uses skin types Normal, Oily, Dry, Sensitive, Combination and concerns like acne, dullness — [Nykaa Skin Compass](https://www.nykaa.com/skin-compass)
- Purplle top-level: Skincare, Makeup, Haircare, Bath & Body, Personal Care, Fragrance; skincare filters by skin type and concern; sunscreens grouped mineral/chemical/hybrid, tinted/untinted; haircare by concern (dandruff, hair fall, frizz, heat damage, scalp); personal care = intimate, menstrual, oral, wellness supplements, grooming; shade ranges described fair→deep for Indian undertones — [Purplle llms.txt](https://www.purplle.com/llms.txt)
- Tira: covers makeup, skincare, fragrances, haircare for men and women; top nav Makeup, Skin, Hair, Brands, Tira Red (luxury); shade finder on product page scans face for tone/undertone to suggest foundation shade; virtual try-on via photo or model image — [Tira App Store](https://apps.apple.com/in/app/id1619767136); [Tira shade finder article](https://www.tirabeauty.com/article/articles/find-your-exact-fenty-beauty-foundation-match); [Tira tech article](https://www.tirabeauty.com/article/articles/from-virtual-try-on-to-beauty-quizzes-tiras-tech-that-transform-shopping)
- SUGAR shade naming: numbered named shades, e.g. Mettle Satin Lipstick with 10 shades such as "03 Emma" (brick red) — SECONDARY retailer listing [reach.dog](https://reach.dog/shop/sugar-cosmetics/products/mettle-satin-lipstick)
- Salon/professional: Nykaa PRO is a membership for salons, makeup artists, beauticians, hair stylists and academies with special prices; Nykaa has a Men > Hair Care > Professional Products category (253 items); pro brands L'Oréal Professionnel (74 products), Schwarzkopf Professional hair colour (68), Streax Professional (54; filters include Hair Colour, Frizz Control), Raaga Professional colour (e.g. Natural Brown 90 g) — [Nykaa PRO](https://www.nykaa.com/pro-intro); [Nykaa men professional products](https://www.nykaa.com/mens/hair-care/professional-products/c/1415); [L'Oréal Professionnel on Nykaa](https://www.nykaa.com/brands/l-oreal-professionnel/c/1251); [Streax Professional on Nykaa](https://www.nykaa.com/brands/streax-professional/c/3135); [Raaga Professional colour](https://www.nykaa.com/raaga-professional-pro-10-express-permanent-hair-colour/p/3698270)
- Baymard (general): visual colour chips with label/tooltip beat text-only options — [Baymard filter UI](https://baymard.com/learn/ecommerce-filter-ui)

### Inferences
- Suggested left rail for a small beauty shop: Makeup, Skin, Hair, Bath & Body, Fragrance, Men's Grooming, Tools & Accessories, Salon Professional, Bridal/Traditional (bindi, sindoor, kajal, mehendi, alta). Makeup tiles grouped Face (foundation, compact, concealer, primer, blush), Eyes (kajal, eyeliner, mascara, eyeshadow), Lips (lipstick, liquid lipstick, lip balm, gloss), Nails (polish, remover).
- Salon Professional tiles: Hair colour, Developer, Keratin/Smoothening, Shampoo & Conditioner (pro), Hair masks/serums, Waxing, Facial kits, Bleach — split by "for retail sale" vs "salon use" (back-bar) if the shop also runs a salon.
- Shades should be variants of one product shown as round colour chips with shade number + name (e.g. "03 Emma"), mirroring colour chips in apparel; at the counter, barcode per shade.
- Filters for beauty: Brand, Price, Shade/Colour chips, Finish (matte/satin/glossy), Skin type, Concern, Size (ml/g), Discount; "vegan/cruelty-free" as a flag.

### Gaps
- Myntra Beauty and SUGAR's own category trees and swatch UI not retrieved (blocked/no results).
- No source confirmed vegan/cruelty-free or size/ml as named filters on Nykaa/Purplle in 2025–26.
- No reliable source on how Indian salon POS systems categorise retail vs service-use stock.

## 4. Hindi/regional names commonly used

### Takeaway
Indian retailers use native garment names directly as category labels in English UI (Saree, Kurti, Kurta, Lehenga Choli, Salwar, Churidar, Sherwani, Anarkali, Sharara, Patiala, Chikankari, Banarasi); Meesho also offers a Hindi-language site. Beauty-item names (bindi, sindoor, kajal, mehendi) were not confirmed as retailer categories in this search.

### Cited Findings
- AJIO labels: Kurtas, Kurtis & Tunics, Lehenga Choli Sets, Salwars & Churidars, Sarees, Sherwani Sets, Anarkali — [AJIO women ethnic](https://www.ajio.com/women-ethnic-wear/c/830303); [AJIO Anarkali](https://www.ajio.com/find/Anarkali?page=741)
- Myntra trend terms: sharara, chikankari, zari, Patiala, Anarkali, pre-draped saree — SECONDARY [Apparel Resources](https://apparelresources.com/business-news/retail/myntra-reports-50-growth-in-womens-occasion-wear-demand/)
- Meesho Hindi interface for women kurtis; Banarasi silk saree, mirror-work kurtis category pages — [Meesho Hindi kurtis](https://meesho.com/hi/women-kurtis/pl/xd5go); [Meesho mirror work kurtis](https://www.meesho.com:443/mirror-work-kurtis/pl/3v7)

### Inferences
- Keep English-transliterated labels as primary (Saree, Kurti, Dupatta, Chunni as search synonym of Dupatta, Kajal, Bindi, Sindoor, Mehendi, Alta, Choodi/Bangles), add Devanagari/regional synonyms to search (साड़ी, कुर्ती, दुपट्टा/चुन्नी, काजल, बिंदी, सिंदूर, मेहंदी). Search should match synonyms: chunni = dupatta = odhni; kurti ≈ short kurta; salwar suit = dress material (unstitched).

### Gaps
- No retailer source fetched for beauty Hindi terms or regional (Tamil/Telugu/Bengali) category names; glossary above is inference, not sourced.
