# Clothing shop catalogue: categories, sizes, seasons (India, 2026)

Research note, 2026-11-06. This was a quick pass of about 7 web searches. I fetched no pages in full, so every finding below comes from search-result summaries. Many sources are commercial blogs; each one is flagged where that matters.

## Department > category > sub-category trees

### Takeaway
I found no official public category tree for Myntra, Ajio, Meesho, Trends, Zudio or V-Mart. Third-party coverage shows the same groupings recurring: Women's Ethnic (Sarees, Kurtas & Suits, Kurtis/Tunics, Lehengas), Western wear, Lingerie/Sleepwear, and Plus size.

### Cited Findings
- Myntra-focused guides list "Kurtas & Suits", "Sarees", "Ethnic Wear", "Western Wear" and "Plus Size Collections" as browse categories. Kurtis, Tunics & Tops are grouped separately. Sarees are split by fabric or occasion (silk, cotton, georgette, designer, party wear). — [Barkha Boutique guide](https://barkhaboutique.com/guides/best-ethnic-wear-brands-myntra)
- Myntra's women's range covers dresses, tops, jeans, skirts, kurtis, lehengas, activewear and sleepwear. Its app-store listing uses "lingerie" and "party wear" as terms. — [Shoppre blog](https://blog.shoppre.com/blog/myntra-women-dresses-from-canada); [Microsoft Store listing](https://apps.microsoft.com/detail/9wzdncrdlpb9?hl=en-US)
- Myntra reports "occasion wear" and "premium ethnic wear" as tracked segments, which supports Party/Occasion as a tag. — [Apparel Resources](https://apparelresources.com/business-news/retail/myntra-reports-50-growth-in-womens-occasion-wear-demand/); [India Retailing 2023](https://www.indiaretailing.com/2023/07/28/myntra-records-close-to-100-growth-in-premium-ethnicwear/)
- Meesho sells kids' ethnic items such as baby-girl sarees and kids' lehenga cholis. — [Meesho listing](https://www.meesho.com/kids-lehenga-choli/p/443a0j)

### Inferences
- A tree that fits a small shop. Occasion (Daily / Party / Festive / Bridal) and Season (Summer / Winter / All-season) work better as tags than as branches.
  - **Women:** Ethnic (Sarees, Kurtis, Salwar suits / dress material, Lehenga, Dupatta, Blouse); Western (Tops, Dresses, Jeans, Leggings/Palazzo); Innerwear (Bras, Panties, Slips/Camisoles, Shapewear); Nightwear (Nighties, Night suits, Bridal/Honeymoon sets); Winter (Shawls, Cardigans, Sweaters, Thermals).
  - **Men:** Topwear (Shirts, T-shirts, Kurtas); Bottomwear (Jeans, Trousers, Track pants, Shorts, Pyjama/Dhoti); Ethnic (Kurta-pyjama, Sherwani); Innerwear (Briefs, Trunks, Vests); Winter (Jackets, Sweaters, Thermals).
  - **Kids:** Baby 0–2 y (Girl / Boy / Unisex: rompers, jhabla, sets); Girls 2–16 y; Boys 2–16 y; Kids ethnic.
- "Branded" works best as a `brand` field plus a filter, not as a department.

### Gaps
- I did not fetch the live navigation menus of Myntra, Ajio, Trends, Zudio or V-Mart, so exact labels are not verified.

## Size systems

### Takeaway
Use one size-scale table per category: alpha (XS–4XL), waist (28–44 in), kids by age, bra band+cup, briefs by waist in cm, Free Size, and garment length for sarees.

### Cited Findings
- Indian bra charts combine a band with a cup. 32A–D, 34A–D and 36A–D are listed as standard combinations. 32C, 34B and 36A are sister sizes. — [Shyaway size chart](https://www.shyaway.com/bra-size-chart-by-experts/); [Clovia blog](https://www.clovia.com/blog/?p=3804)
- Enamor product pages list 32B among their sizes. A third-party blog says Enamor's range runs 32B–38C (not confirmed by the brand). Fit varies by brand. — [Enamor A039](https://www.enamor.co.in/products/enamor-skin-medium-coverage-cotton-bra-a039)
- Men's briefs (Freecultr, a competing brand, describing Jockey India, unverified): S 73–78 cm, M 78–83 cm, L 83–88 cm. Jockey listings on Flipkart show cm ranges such as "70-75" and "90-95". — [Freecultr](https://freecultr.com/blogs/blog/standard-underwear-size-chart-used-by-popular-indian-brands); [Flipkart Jockey](https://www.flipkart.com/jockey-men-brief/p/itm36e5923b67056)
- Baby sizes: 0–3M is about 3.5–5.5 kg and 50–58 cm. Weight is a more reliable guide than age. — [Luvlap baby size chart](https://www.luvlap.com/blogs/kids-fashion/baby-clothing-size-chart-india)
- Saree: about 5.5 m (about 6 yd), with a blouse piece of about 80 cm. One blog gives 5.5–6 m. — [Utsav Fashion help](https://help.utsavfashion.com/support/solutions/articles/14000005655-what-are-the-fabric-dimensions-of-saree-salwar-lehenga-); [Saroj Jain blog](https://blog.sarojjain.com/posts/complete-indian-ethnic-wear-size-guide/)

### Inferences
- Suggested scales:
  - **Alpha:** XS, S, M, L, XL, XXL, 3XL, 4XL.
  - **Waist (in):** 28–44, even numbers.
  - **Kids:** 0–3M, 3–6M, 6–12M, 12–18M, 18–24M, then 2–3Y, 3–4Y and so on, up to 14–15Y and 15–16Y.
  - **Bra:** bands 28–44, cups A–DD/E. Seed the common grid 30B–40D.
  - **Panties/briefs:** S–XXL, or waist ranges in cm.
  - **Other:** Free Size. Saree with blouse piece Y/N and length in metres.
- Sizes are not consistent across brands, so the size should be stored per SKU variant, not as a global equivalence.

### Gaps
- I did not retrieve official Jockey India or Lux Cozi charts, or a vest chest-size chart (commonly 75/80/85/90/95/100 cm, unverified).
- I did not research footwear sizes (UK/IND 5–11), which are probably out of scope.

## Colours, patterns, fabric

### Takeaway
I gathered little direct evidence. The variant axes are normally Size × Colour. Fabric matters for search and for HSN (knitted vs woven).

### Cited Findings
- Marketplaces group sarees by fabric (silk, cotton, georgette). — [Barkha Boutique](https://barkhaboutique.com/guides/best-ethnic-wear-brands-myntra)
- HSN chapter 61 covers knitted or crocheted garments and chapter 62 covers non-knitted (woven) garments, with the same rate split for both. — [Taxclue 6212](https://taxclue.in/hsn-code/6212)

### Inferences
- Suggested attributes:
  - **Variant:** colour and size.
  - **Item level:** fabric (Cotton, Polyester, Rayon, Silk, Georgette, Wool, Blend), pattern (Solid, Printed, Embroidered, Checked, Striped) and construction (Knitted / Woven), which drives the HSN chapter.

### Gaps
- I found no source on standard colour lists.

## Seasonality and clearance

### Takeaway
Winter-wear stock is a risk. Recent mild winters left chains with surplus stock, and they ran clearance sales early, in December and January, with discounts of up to 70%.

### Cited Findings
- In January 2025, chains including H&M, Uniqlo and Lifestyle started year-end clearance about two weeks early to clear excess winter wear. — [DFU Publications](https://dfupublications.com/index.php/news/apparel/apparel-retailers-launch-year-end-clearance-sales-two-weeks-ahead-of-schedule)
- A delayed, mild winter meant a second weak season in a row, leaving surplus stock. Margins fell 6–8% and discounts reached up to 70%. — [Third Eyesight](https://www.thirdeyesight.in/tag/apparel-industry/); [The Ken](https://the-ken.com/story/winter-wear-is-shrinking/)
- Ludhiana is the main hub for winter wear. — [Apparel Resources](https://apparelresources.com/business-news/trade/ludhiana-expecting-good-sale-winterwear/)

### Inferences
- Add a Season tag (Winter roughly Oct–Feb, Summer roughly Mar–Jun, Monsoon, All-season), an Occasion tag (Festive: Navratri/Diwali Sep–Nov; wedding season roughly Nov–Feb and Apr–May), stock-age reports, and a bulk "mark down season" action.

### Gaps
- I found no sources on how small independent shops run clearance. The wedding-season months are general knowledge and unsourced.

## Privacy norms for innerwear and bridal nightwear

### Takeaway
Discretion is a core value in Indian lingerie retail. Zivame's growth was built on privacy, and it advertises discreet packaging. I found no source on how bills should be worded.

### Cited Findings
- Reuters (2015, older) reported that Zivame grew because women prefer discreet online buying over male-staffed shops. — [Business Standard/Reuters 2015](https://www.business-standard.com/article/reuters/for-india-s-largest-lingerie-e-tailer-privacy-pays-115091700191_1.html)
- Zivame lists "discreet packaging" among its services (secondary sources). — [Medium](https://medium.com/decodein/zivame-celebrating-the-radiance-of-women-54ef69905dee); [CashKaro](https://cashkaro.com/blog/zivame-grand-lingerie-festival/191755)

### Inferences
- Give each item an optional "bill name" or discreet flag. The bill would print a neutral name such as "Innerwear" or "Nightwear set" instead of "Honeymoon babydoll", and the customer-facing app and WhatsApp messages would hide product images. Use opaque bags for delivery. Female staff or women-only browsing hours are a separate store-practice choice.

### Gaps
- I found no source on GST invoice wording rules. A generic description that matches the HSN is likely acceptable, but this is unverified.

## HSN codes and GST

### Takeaway
After GST 2.0 (from 22 September 2025), apparel in chapters 61, 62 and 6212 is taxed at 5% up to ₹2,500 per piece and 18% above it. The source is Taxclue, a secondary source; verify against the CBIC notification.

### Cited Findings
- Notification 9/2025-CT(Rate), effective 22 September 2025: 5% at or below ₹2,500 per piece, 18% above, for chapters 61 and 62 and heading 6212. — [Taxclue 6212](https://taxclue.in/hsn-code/6212)
- Busy.in still describes the old regime (5% at or below ₹1,000, 12% above). That is outdated after September 2025. — [Busy.in](https://busy.in/gst-rates/garments)
- 6212 covers brassieres (621210), girdles (621220), corselettes (621230) and others (621290). — [Eximpe 6212](https://eximpe.com/hsncode-finder/6212)

### Inferences
- Common headings, from general HS knowledge and not individually verified:
  - **Chapter 61 (knitted):** 6107 men's briefs and nightwear; 6108 women's slips, panties and nightdresses; 6109 T-shirts and vests; 6110 sweaters; 6111 babies' garments; 6104 women's suits and dresses; 6103 men's trousers.
  - **Chapter 62 (woven):** 6203 men's trousers; 6204 women's; 6205 shirts; 6206 blouses; 6207 and 6208 woven underwear and nightwear; 6209 babies'; 6212 brassieres.
- Sarees are mostly classified as fabric under chapters 50, 52, 54 and 55, not as garments; verify this.
- The app should pick the GST rate from the per-piece price automatically.

### Gaps
- I did not fetch the CBIC notification text itself.
