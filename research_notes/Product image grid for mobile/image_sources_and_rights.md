# Product image sources and rights for an Indian shop app (2026)

Not legal advice. Points marked [LAWYER] should be checked by an Indian IP lawyer.

## Copying pack-shot photos from Flipkart, Amazon, BigBasket, brand sites or Google Images

### Takeaway
High risk. Under the Copyright Act 1957 a product photo is its own copyrighted work, owned by the photographer or brand. Recent Delhi High Court rulings went against sellers who reused other people's product photos, and the marketplaces' terms ban scraping. Google Images only finds photos; it gives no licence to use them.

### Cited Findings
- Delhi HC (Justice Prathiba M Singh) granted an injunction against Meesho sellers. It held that sellers "do not have any right to copy the photographs, images, product design" of the plaintiff. It also said platforms must verify seller details. — [Verdictum](https://verdictum.in/court-updates/high-courts/sellers-do-not-have-right-to-copy-photographs-or-product-design-of-small-businesses-delhi-high-court-grants-injunction-against-meesho-for-copyright-infringement-1500150); [SCC Online](https://www.scconline.com/blog/post/2023/10/09/dhc-grants-injunction-to-seller-on-meesho-against-copyright-infringement-legal-news/)
- Louis Vuitton v Haute24 (2024): Delhi HC imposed Rs 5 lakh costs for unauthorised use of copyrighted product photographs. The court focused on the photo use, not on whether the goods were genuine. — [SCC Online](https://scconline.com/blog/post/2024/08/28/dhc-imposes-rs-5-lakhs-costs-on-website-for-using-copyrighted-photographs-of-louis-vuitton-without-authorization); [Mondaq](https://www.mondaq.com/india/copyright/1515922/louis-vuitton-vs-haute24-protecting-iconic-brand-identity)
- A 2025 Delhi HC judgment found infringement where photos were "substantially reproduced" from the plaintiff's photographs. I saw only fragments of it. — [Delhi HC PDF](https://delhihighcourt.nic.in/app/showFileJudgment/ABL28042025SC1802024_134412.pdf)
- Amazon's Conditions of Use (UK and US versions) ban "any use of data mining, robots, or similar data gathering and extraction tools". I did not read the amazon.in text directly. — [Amazon UK CoU](https://digprjsurvey.amazon.co.uk/csad/help/node/GLSBYFE9MGKKQXXM); [ConductAtlas](https://conductatlas.com/platform/amazon/amazon-conditions-of-use/provision/CA-P-018441/prohibition-on-data-mining-and-scraping-tools/)
- Amazon reportedly blocked Meta's Muse shopping agent under its Conditions of Use. — [Search Engine Journal](https://searchenginejournal.com/amazon-blocked-metas-muse-and-robots-txt-had-nothing-to-say/590494)
- A vendor blog describes catalogue hijacking and photos reused across Indian platforms. It is low-quality evidence. — [Marketian](https://www.marketian.io/blog/how-indian-market-places-are-stealing-products-images-from-small-players)

### Inferences
- Scraping marketplace photos into a SaaS product puts two kinds of risk on us: copyright infringement and breach of the sites' terms. As a platform we could also face takedown demands, and the platform-duty reasoning in the Meesho case could reach us.
- Trademark: showing a brand's logo on its own genuine product, to identify goods we actually sell, is likely nominative or descriptive use (Trade Marks Act 1999 s.30). The trademark is not the main problem; the copyright in the photo is. [LAWYER]
- Many small Indian sellers informally copy brand images. Brands rarely sue kirana shops over this, but an app that does it systematically at scale is a much bigger target.

### Gaps
- I found no Indian case where a brand sued a reseller specifically for using the brand's own pack shot to sell genuine goods. This is a possible implied-licence argument. [LAWYER]
- I did not verify the Flipkart or BigBasket terms of use text.

## Open and free sources (Open Food Facts and related)

### Takeaway
Open Food Facts (OFF) is the best free, legal source. Its photos are CC BY-SA, and commercial use is allowed if we give attribution and share alike. India coverage is about 20k food products, against an estimated 90k food products sold in India. It covers mostly big FMCG brands and no loose or local items.

### Cited Findings
- Images are under CC BY-SA 3.0 and data under ODbL. Commercial use is allowed. We must credit OFF with a link to the site or product page, and derivative works must be shared alike. The CC licence covers only the photo itself. Packaging design copyright and trademarks may still apply, and re-users must check those themselves. — [OFF Terms](https://world.openfoodfacts.org/terms-of-use); [OFF data page](https://openproductsfacts.org/data)
- India database counts: 10k products (September 2024), more than 15k (June 2025), 20k (February 2026). About 3,300 brands as of September 2025. These counts come from community blogs. — [India Food Stats Feb 2026](https://indiafoodstats.blogspot.com/2026/02/wikipedia-of-food-in-india-just-got.html); [OFF forum](https://forum.openfoodfacts.org/t/open-food-facts-india-database-reaches-10k-product-milestone/681); [Sept 2025](https://indiafoodstats.blogspot.com/2025/09/understanding-indias-packaged-foods.html)
- An estimated 90,000 food products are sold in India. — [OFF wiki India dashboard](https://wiki.openfoodfacts.org/Dashboard/India)
- Open Products Facts uses the same licences for non-food items. — [openproductsfacts.org/data](https://openproductsfacts.org/data)

### Inferences
- Lookup by barcode: `GET https://world.openfoodfacts.org/api/v2/product/{EAN}.json` returns `image_front_url` and related fields. This endpoint is from my training knowledge and was not verified this session. OFF asks API users to send a custom User-Agent and recommends bulk data dumps for heavy use.
- For attribution, show a small "Photo: Open Food Facts (CC BY-SA)" credit with a link on the item detail screen, plus a credits page.
- Cache images on our own CDN to avoid repeated calls to OFF.
- Shopkeeper photos we contribute back to OFF would become CC BY-SA. This should be opt-in.
- Coverage outside food (medicines, pumps, hardware) will be very thin in Open Products Facts and Open Beauty Facts. Wikimedia Commons is not a practical source of pack shots.

### Gaps
- I did not verify OFF API rate limits, the share of India entries that have a front image, or India coverage numbers for Open Beauty Facts and Open Products Facts.

## Paid and licensed sources

### Takeaway
GS1 India DataKart is the authoritative source, built from brand-uploaded data and images. API access is a Premium feature for GS1 subscribers, and prices were not found. Global barcode APIs are cheap but cover Indian EANs poorly, and they do not grant image copyright. I could not confirm ONDC's image rules.

### Cited Findings
- Basic DataKart access comes bundled with the GS1 India barcode subscription. API integration with third-party platforms is a Premium feature, and a Premium fee-structure PDF exists. — [DataKart Premium](https://www.gs1india.org/datakart/datakart-premium); [Fee PDF](https://www.gs1india.org/wp-content/uploads/DataKart-Premium-Service-new.pdf)
- Brands can upload unlimited images. Images follow the GS1 Product Image Specification and package measurement rules, as PNG or JPG files of up to 5 MB. — [DataKart for brand owners](https://gs1india.org/datakart/datakart-for-brand-owners)
- BigBasket has used DataKart, per a GS1 case study. — [GS1 case study](https://admin.gs1india.org/uploads/Big_Basket_Case_Study_a809bd7489.pdf)
- UPCitemdb: free for 100 requests a day with no signup. Paid tiers go up to about $99 a month. These figures come from competitor ShopSavvy, checked 2026-10-12. — [ShopSavvy vs UPCitemdb](https://shopsavvy.com/compare/shopsavvy-data-api-vs-upcitemdb)
- Go-UPC: no free tier, only a trial key. About $74.95 a month for 5,000 requests per ShopSavvy; an older Geekflare source says from $49 a month. — [ShopSavvy vs Go-UPC](https://shopsavvy.com/compare/shopsavvy-data-api-vs-go-upc); [Geekflare](https://geekflare.com/dev/upc-lookup-apis/); [Go-UPC plans](https://go-upc.com/plans/api)
- Barcode Lookup returns name, description and images. I could not see its prices. — [barcodelookup.com/api](https://barcodelookup.com/api)

### Inferences
- The images these APIs return are often scraped from retailers, so using them does not clear copyright. Check each vendor's terms for any image licence before relying on it.
- Partnering with GS1 India, or getting brand or distributor permission, is the only clean route to official pack shots at scale. FMCG distributors often share brand catalogues with retailers informally. Get written permission before using them.

### Gaps
- DataKart Premium fees and whether re-users can redistribute images. Contact GS1 India.
- ONDC catalogue image norms. No official source was found.
- Indian EAN hit rates for UPCitemdb, Go-UPC and Barcode Lookup.

## Shopkeeper photographing the pack

### Takeaway
This is legally the cleanest source. The shopkeeper owns their photo; the pack design and logo inside it are incidental and the goods are genuine. Uniform-looking photos need an in-app capture guide plus automatic background removal and cropping.

### Cited Findings
- Marketplace norms: white background for the main image, product only, no watermarks or seller logos, not blurry, at least about 1000 px. — [Noon](https://helpcenter.noon.partners/en/category/product-listing/image-requirements-and-rejection-reasons-for-the-seller-sku); [VTEX](https://newhelp.vtex.com/docs/tutorials/best-practices-for-using-images-in-the-catalog); [Fynd/Amazon guide](https://documentation.fynd.com/commerce/docs/openapi/listings-management/amazon/image-guidelines-amazon.md)

### Inferences
- Best practices for the capture screen:
  - Square framing overlay with "fill 85%" guides.
  - Tap to focus, plus a warning if the photo is blurry or too dark.
  - On-device background removal (for example ML Kit subject segmentation or Apple Vision), then place the product on white or light grey, auto-crop to its bounding box with padding, and export a 1:1 square at about 800–1000 px in WebP.
- Ask the shopkeeper to tick a checkbox: "photo taken by me".
- Share shopkeeper photos across shops (crowd catalogue keyed by EAN) only under terms of service where the shopkeeper grants us a licence.

### Gaps
- I did not benchmark segmentation SDKs.

## Medicines

### Takeaway
I found no Indian rule specifically about images. The 2018 draft e-pharmacy rules, still not finalised as far as I found, ban e-pharmacies from advertising drugs. Showing prescription-drug pack shots promotionally could be risky. [LAWYER]

### Cited Findings
- The draft e-pharmacy rules (G.S.R. 817(E), 28 August 2018) amend the Drugs and Cosmetics Rules 1945. They say "No e-pharmacy shall advertise any drug on radio or television or internet...". They exclude narcotics, psychotropics and tranquillisers from online sale, and require registration and pharmacist details on the portal. — [Mondaq](https://mondaq.com/india/food-and-drugs-law/747792/centre-proposes-draft-e-pharmacy-rules-to-regulate-online-sale-of-drugs); [Scroll](https://scroll.in/pulse/893044/health-ministry-releases-draft-rules-to-regulate-online-sale-of-medicines); [LKS](https://www.lkslaw.com/insights/articles/diagnosing-the-issues-of-e-pharmacies)

### Inferences
- In-store billing item cards are probably fine. For an online storefront, show Schedule H/H1/X items with a neutral image (generic pill icon plus name) and no promotional framing. The Drugs and Magic Remedies Act 1954 also limits advertising for certain diseases. [LAWYER]

### Gaps
- Whether the 2018 draft has been finalised or replaced as of 2026.
- Any CDSCO guidance on product images.

## Recommended safe approach for a small Indian SaaS app in 2026

### Takeaway
Use a layered approach in this order:
1. Shopkeeper's own photo, with a capture guide and auto background removal.
2. Open Food Facts or Open Products Facts lookup by EAN barcode, with attribution and images cached on our CDN.
3. Brand, distributor or GS1 DataKart images, only with written permission or a paid licence.
4. Fallback: a generated category icon or a text tile using the first letter of the name and a colour.

Never scrape Amazon, Flipkart, BigBasket or Google Images.

### Cited Findings
- See the sections above: OFF licence ([OFF Terms](https://world.openfoodfacts.org/terms-of-use)), the court cases ([Verdictum](https://verdictum.in/court-updates/high-courts/sellers-do-not-have-right-to-copy-photographs-or-product-design-of-small-businesses-delhi-high-court-grants-injunction-against-meesho-for-copyright-infringement-1500150)), and DataKart ([GS1 India](https://www.gs1india.org/datakart/datakart-premium)).

### Inferences
- Cost: OFF is free, apart from storage and CDN (a few hundred rupees a month at small scale). UPCitemdb is free at 100 lookups a day as an extra source of names, but do not trust its images. A paid barcode API costs about $50–100 a month. GS1 pricing is unknown.
- Build a shared EAN catalogue: once one shop photographs a pack (with licence consent), every shop gets it. Coverage improves fastest this way.
- Add a takedown or report-image flow and an IP policy page. This helps with intermediary safe-harbour under IT Act s.79. [LAWYER]

### Gaps
- Whether CC BY-SA share-alike applies to our app UI or only to the image. Generally it applies only to the image and its adaptations, such as cropped versions. [LAWYER]
