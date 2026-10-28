# Grocery product cards, category tiles and product images (Blinkit / Zepto / Flipkart Minutes) for Grocery Khata

Note on method: blinkit.com terms and the main Medium teardown returned HTTP 403; no public, citable teardown of Blinkit/Zepto card anatomy was found. Card/tile anatomy below is therefore mostly in Inferences (from general familiarity with the apps, not verified in this session) and should be checked against live screenshots before being treated as fact.

## (a) What appears on a Blinkit / Zepto / Flipkart Minutes product card, in what order, and what sizes/spacing are typical?

### Takeaway
No primary or reliable secondary source documenting the exact card anatomy was found; the commonly observed pattern is: square cut-out image on white/light grey -> delivery-time chip -> 2-line name -> pack size -> price + struck MRP (+% off) -> ADD button that turns into a -/qty/+ stepper.

### Cited Findings
- General UX reviews of Blinkit exist but focus on navigation and CTA wording (e.g. replacing "Next" with "Buy now"/"View Cart"), not card components — [YellowSlice](https://www.yellowslice.in/blog/blinkit-app-user-experience-how-it-became-popular-and-how-to-make-it-even-better)
- A Zepto teardown focuses on the 10-minute delivery USP as the activation driver, not card layout — [Tear Them Down](https://tearthemdown.substack.com/p/zepto-prop)
- Common e-commerce pattern: products with variants show "Choose options" instead of a quick-add button, opening a variant + quantity picker; sale badges show text, % or fixed amount — [Foxecom Zest theme docs](https://docs.foxecom.com/zest-theme/theme-settings/product-card)
- Data fields typical of a Blinkit product record: product ID, name, category, price, stock quantity — [Product dissection doc (student exercise)](https://docs.google.com/document/d/15aujvhBgS2HalSveJOijbU7uytuJwReM/export?format=pdf)

### Inferences (unverified observation, check live)
- Blinkit card: square image on white with thin grey border and ~8–12 px radius; small grey chip with clock icon and "8 MINS"/"10 MINS"; product name (2 lines max, bold-ish 12–13 px); pack size in grey ("500 g", "1 L", "Pack of 2"); bottom row price (bold) with MRP struck through beside/below; green-outlined "ADD" button at bottom-right which becomes a filled green "– 1 +" stepper. A blue/purple corner ribbon "12% OFF" sits on the image's top-left. Variant products show "2 options" under ADD and open a bottom sheet of sizes.
- Zepto card: similar; ADD button often overlaps the image's bottom-right corner (pink/purple outline); price block with "₹X OFF" savings text in green; recent versions show a rating/"bestseller" tag.
- Flipkart Minutes: Flipkart-blue accents, "% off" in green next to struck MRP, ADD button bottom, delivery-time callout mostly at page/header level rather than per card.
- Veg/non-veg FSSAI mark (green/brown square-with-dot) appears small near the name/top of image on food items in these apps.
- Out-of-stock: image greyed/desaturated, ADD replaced by "Out of stock" or "Notify me" text, card sorted to bottom.
- Grid: 2 columns on home rails / 3 columns in category listings (Blinkit's category page uses a left sub-category rail + 2-column grid); card width roughly 105–170 dp depending on columns.

### Gaps
- Exact pixel sizes, font sizes and spacing — no published spec or teardown; needs measuring from screenshots.
- Current (2025–26) Flipkart Minutes card specifics — no source found.

## (b) How are category tiles designed (background tint, cut-out photos, columns, labels, section headers)?

### Takeaway
No citable source; the observed pattern on Blinkit/Zepto is a 4-per-row grid of rounded light-tinted squares, each holding cut-out product photos, with a 1–2 line label underneath, grouped under bold section headers — essentially the same as the owner's beauty-app reference.

### Cited Findings
- No teardown found documenting category tile design (searches returned business case studies only) — [search results incl. Young Urban Project](https://youngurbanproject.com/blinkit-case-study)

### Inferences (unverified observation)
- Blinkit home: section headers like "Grocery & Kitchen", "Snacks & Drinks", "Beauty & Personal Care"; under each, 4 tiles per row; tile is a rounded square (~16 px radius) with a pale tint (light blue/green/peach, often one tint for the whole section), containing a collage of 2–3 cut-out packs; label below in 2 short lines ("Dairy, Bread & Eggs", "Atta, Rice & Dal").
- Zepto: similar 4-col grid, sometimes 3-col with larger tiles, with per-category pastel backgrounds.
- Labels are short nouns, max ~20 characters, centred, wrapping to 2 lines.

### Gaps
- Exact tile sizes, tint hex values; whether tints are per-tile or per-section — needs screenshots.

## (c) What image formats/sizes and loading strategies do they use?

### Takeaway
Blinkit serves product images from its own CDN (cdn.grofers.com) through an on-the-fly transform layer (Cloudflare-style `cdn-cgi/image/f=auto`), meaning format (WebP/AVIF) is negotiated per browser and size requested per slot.

### Cited Findings
- Blinkit product images observed at `https://cdn.grofers.com/da/cms-assets/cms/product/<id>.jpg` — [Apify Blinkit scraper sample](https://apify.com/123webdata/blinkit-scraper.md)
- Another observed pattern: `https://cdn.grofers.com/cdn-cgi/image/f=auto/app/images/products/sliding_image/14639.jpg` (`f=auto` = Cloudflare Image Resizing auto-format) — [Apify grocery scraper](https://apify.com/axlymxp/blinkit-grocery-scraper.md)
- Blinkit was formerly Grofers, hence grofers.com hosts — [Netify](https://www.netify.ai/resources/applications/blinkit)

### Inferences
- `f=auto` means WebP/AVIF served to supporting browsers from a JPG master; width params (`w=`) are likely added per slot — not confirmed.
- Zepto and Flipkart presumably use similar CDN resizing (Flipkart's rukminim CDN encodes size in the URL path, e.g. /image/312/312/) — from memory, not verified here.
- Lazy-loading of below-fold cards and grey/blurred placeholders are standard; not verified for these apps.

### Gaps
- Zepto/Flipkart image CDN conventions and exact byte sizes — not retrieved.

## (d) Which image sources can a small Indian startup legally use, and under what licences?

### Takeaway
Safe options: Open Food Facts (photos CC BY-SA, database ODbL, with attribution + share-alike and trademark caveats), GS1 India DataKart (brand-verified images, paid/subscription), brand-supplied images, own/shop-taken photos, and generated illustrations. Copying images from Blinkit/Zepto/Flipkart/BigBasket is not allowed.

### Cited Findings
- Open Food Facts database: ODbL; contents: DbCL; product photos: CC BY-SA. Reusers must name the licence, credit Open Food Facts and link to the site/product page; derivatives share-alike — [OFF Terms of use](https://world.openfoodfacts.org/terms-of-use)
- OFF photo licence covers only the photographer's rights; packaging designs, people's likeness and trademarks may still be protected; reusers must check — [OFF Terms of use](https://world.openfoodfacts.org/terms-of-use)
- GS1 India DataKart is the national repository of Indian retailed product data including images, MRP and net content — [GS1 India: What is DataKart](https://gs1india.org/blog/how-datakart-is-revolutionising-product-data-exchange-across-sectors)
- DataKart images produced to the GS1 Product Image Specification; Premium tier has an image library and API integration (ERP/custom apps), requires GS1 India subscription — [DataKart Premium](https://gs1india.org/datakart/datakart-premium); [DataKart for retailers](https://gs1india.org/datakart/datakart-for-retailers)
- BigBasket uses GS1 India data query/verification services to build listings — [GS1 India BigBasket case study](https://admin.gs1india.org/uploads/Big_Basket_Case_Study_a809bd7489.pdf)
- Third-party scrapers sell Blinkit images/data (Apify, Actowiz) — their existence does not grant rights — [Apify](https://apify.com/123webdata/blinkit-scraper.md); [Actowiz](https://www.actowizsolutions.com/datasets/details/blinkit-datasets)

### Inferences
- Copying from Blinkit/Zepto/Flipkart/BigBasket: images are their (or brands') copyright and their terms prohibit scraping/reproduction (blinkit.com/terms returned 403 so clause not quoted); under the Indian Copyright Act 1957 this is infringement — avoid even if scraper services offer it.
- OFF India coverage is patchy for kirana SKUs; images are user photos (not clean cut-outs) and need background removal; CC BY-SA share-alike likely applies to edited images, so keep an attribution/credits screen.
- Brand images: FMCG brands/distributors often share press/catalogue packshots on request; get written permission.
- Shop-uploaded photos: shop owner photographs the pack; background removal (on-device or a service) gives cut-out on tint. The owner holds photo copyright; package artwork trademarks are generally tolerated for identifying the genuine product being sold (nominative use) — legal advice advisable.
- Generated/flat illustrations (generic "atta bag", "milk packet") avoid brand issues and suit category tiles.
- Stock libraries (Unsplash/Pexels licences free for commercial use; Shutterstock/Freepik paid) fit category tiles, rarely exact SKUs.

### Gaps
- DataKart pricing and API terms for small retailers/apps — contact datakart@gs1india.org.
- Exact Blinkit/Zepto/Flipkart terms text (fetch blocked).

## (e) How should a PWA cache product images for offline use without blowing storage limits? (plus recommendations)

### Takeaway
Use a runtime cache-first image strategy with Workbox expiration (maxEntries + maxAgeSeconds + purgeOnQuotaError), small WebP thumbnails (~10–25 KB), persistent storage request, and an emoji/initial-on-tint fallback.

### Cited Findings
- Workbox expiration: `maxEntries` evicts least-recently-requested entries; `maxAgeSeconds` expires by age (an expired entry may be served once); `purgeOnQuotaError` option exists — [Workbox expiration](https://developer.chrome.com/docs/workbox/modules/workbox-expiration)
- Chrome: an origin may use up to 60% of disk; incognito ~5%; with "clear on exit" ~300 MB; storage is "best effort" (evictable) unless persistent storage is granted; check with `navigator.storage.estimate()` — [web.dev Storage for the web](https://web.dev/articles/storage-for-the-web)

### Inferences / recommendations for Grocery Khata
- Category tiles: 4 per row on 360 dp phones (3 on <340 dp), rounded square (16 px radius), per-section pastel tint, cut-out image ~70% of tile, label below max 2 lines/~18 chars, bold section headers ("Atta, Rice & Dal", "Dairy"), matching the beauty-app reference.
- Product card (household ordering): 3-col grid; square image on light tint; veg/non-veg mark top-left; % off ribbon only if shop sets MRP > price; name (2 lines), pack size, price + struck MRP; ADD -> stepper; "2 options" opens a bottom-sheet of pack sizes; OOS = greyed image + "Out of stock". Drop delivery-time badge (not relevant to kirana) or replace with "Ready in 20 min"/"Udhaar ok".
- Billing screen: keep denser list rows with 40 px thumbnails, not big cards.
- Image specs: master 512x512 square, transparent or #F5F5F5-ish background, product filling ~80%; serve WebP thumbnails at 160 px and 320 px (2x), target <=15 KB/20 KB; AVIF optional. Do resizing server-side at upload (no client JS cost against 130 KB budget); use native `loading="lazy"`, width/height attributes, and a CSS tint placeholder (no JS skeleton library).
- Offline caching: separate runtime image cache, cache-first, maxEntries ~300–500 (≈5–10 MB at 15–20 KB each), maxAgeSeconds ~30 days, purgeOnQuotaError true; serve images same-origin or with CORS to avoid opaque-response quota padding (Chrome pads opaque responses heavily — known behaviour, not cited here); optionally pre-cache only the shop's top ~100 SKUs and category tiles after first login on Wi-Fi; request persistent storage.
- Fallback when no image: keep current emoji-on-gradient, or the first letter/short name on the category's tint; never a broken-image icon; let shop owner "tap to add photo" (camera + background removal) to progressively fill the catalogue.
- Source priority: shop photos + background removal for actual SKUs; OFF (with attribution screen) as optional seed; DataKart if budget allows; illustrations for category tiles.

### Gaps
- Opaque response padding size not confirmed from fetched source.
- Real-world low-end Android storage quotas for this user base not measured.
