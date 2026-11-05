# Stock photo licences for a platform-wide master item list (Grocery Khata)

Use case: one generic photo per master item (aggregate, sand, bricks, cement bag, TMT, loose chana, rice, dal, screws, hinges, brushes...), stored by Grocery Khata and shown to thousands of shops and their customers in bills, app screens and online storefronts. Not legal advice; points marked LAWYER need review.

## Free sources: Unsplash, Pexels, Pixabay, Wikimedia Commons, Openverse

### Takeaway
Unsplash and Pixabay licences allow free commercial use with no attribution, and showing a photo inside a product as an item thumbnail looks like normal use. All three free sites ban selling or handing out unaltered images on their own and building a similar or competing image service. Pixabay also bans commercial use of images that show recognisable brands. Wikimedia Commons works if attribution is shown; CC BY-SA adds share-alike duties on edited images.

### Cited Findings
- Unsplash grants an "irrevocable, nonexclusive, worldwide copyright license to download, copy, modify, distribute... and use images", including commercial use, with no attribution required (credit is appreciated) — [Unsplash License](https://unsplash.com/license)
- Unsplash limits: images "cannot be sold without significant modification"; the licence does not cover "compiling images from Unsplash to replicate a similar or competing service" — [Unsplash License](https://unsplash.com/license)
- Pixabay: free, no attribution needed, can be modified. Prohibited: selling or distributing unaltered content as a standalone item (standalone means no creative effort was applied); commercial use of recognisable trademarks, logos or brands in connection with goods or services; misleading use. The summary does not mention apps directly — [Pixabay licence summary](https://pixabay.com/service/license-summary/)
- Pexels (licence page returned 403; help centre used instead): you may not sell or distribute content on a "Standalone" basis, and you may not compile content to build a similar or competing service. Wallpaper apps that hand out Pexels images on their own are named as not allowed — [Pexels help](https://help.pexels.com/hc/de-de/articles/900005880463); secondary summaries at [licenseorg](https://licenseorg.com/guide/images/pexels)
- Wikimedia Commons: credit the original creator, name the licence (and link to it where needed), and follow any attribution the author asks for. For CC BY-SA, edits must be shared under the same licence. CC BY-SA 3.0 lets the credit be given in a way "reasonable to the medium". The page does not cover apps specifically — [Commons: Reusing content](https://commons.wikimedia.org/wiki/Commons:Reusing_content_outside_Wikimedia)

### Inferences
- Showing one photo per item as a catalogue thumbnail inside a billing app, with no download or export button for the image file, is not "selling standalone copies". Fair risk: low. Things to avoid: a public "image library" or an API that lets shops browse or download the whole photo set (this could count as standalone distribution or a "competing service"), and letting shops export raw image files in bulk.
- LAWYER: if Grocery Khata builds a master list of thousands of photos mostly taken from Unsplash or Pexels, could the "compiling ... competing service" clause apply? Arguably no, because the product is billing software and not an image service. This still needs a lawyer's view, and a written email from Unsplash or Pexels would help.
- Keep a provenance log for every image (source URL, author, licence, download date). Free-site photos are sometimes uploaded by people who do not own the rights, and the free sites give no indemnity.
- Commons/Openverse: credit can sit on an "Image credits" page linked from the item. Use CC0 or CC BY over CC BY-SA, and avoid CC BY-NC entirely, because a SaaS product is commercial use.

### Gaps
- Could not fetch the full Pexels licence page (403). Openverse was not fetched; it collects CC-licensed images, so the Commons rules apply to whatever it returns.
- Did not check how well the free sites cover Indian items such as TMT sariya, binding wire or loose dal. They probably cover them poorly; test with searches.

## Paid stock: Shutterstock, Adobe Stock, iStock/Getty, Freepik, Depositphotos, 123RF

### Takeaway
Standard paid licences are usually tied to one seat or user and ban letting others download standalone files or use the images in templates or databases. Showing purchased images to thousands of third-party shops therefore leans toward the Enhanced or Extended licence, or a custom platform or API deal. Freepik's terms are the hardest to fit: they ban letting third parties use the content and ban putting it in a database.

### Cited Findings
- iStock: a licensed use must not allow others to "download, extract, or redistribute content as a standalone file" (s.3.3). Electronic templates "intended for resale or other distribution" need an extended licence (s.3.13). Under credit licences only one person may access content at a time, and multi-user server access needs an extended licence (s.5). No sublicensing (s.4). Editorial-only content is barred from commercial use (s.3.2). AI/ML use is banned (s.3.15) — [iStock Content License Agreement](https://www.istockphoto.com/legal/license-agreement)
- iStock extended add-ons (Multi-seat unlimited, Unlimited reproduction, $250k legal guarantee, Products for resale) cost about 18 credits each per image, according to a third-party site — [Photutorial](https://photutorial.com/istock-pricing)
- Shutterstock (licence page returned 403; these points come from search snippets of its own pages): Standard is "a single seat license" and multi-seat use needs a team subscription or a "premier platform" account. Enhanced adds merchandise, web templates, unlimited print runs and $250k indemnity (Standard has $10k) — [Shutterstock licence comparison](https://www.shutterstock.com/license-comparison); [Shutterstock license](https://www.shutterstock.com/license)
- Apps that embed Shutterstock content for their own end users use its API Platform licence. Enhanced on-demand packs are reported at about $199, $499 and $1,999 (source dated January 2026, not verified) — [Shutterstock API pricing](https://www-shutterstock-com-0t8h.imagizer.com/api/pricing); [Fastlancer review](https://www.fastlancer.org/en/fastlancer-blog/shutterstock-review/)
- Adobe Stock: an Extended licence is about $79.99 per asset with credits (third-party figure). Reseller packs of Extended team credits: 16 for $163.99, 40 for $391.99, 80 for $716.99 — [Photutorial](https://photutorial.com/adobe-stock-licenses-explained); [CDW](https://www.cdw.com/product/adobe-stock-extended-license-team-consumption-16-credit-pack/5446182). The official licence page returned 403.
- Freepik (now redirects to magnific.com): content may not go "in a database, archive or in any other media/stock product". Users may not authorise "any third parties to use" the content. Content may not be the main element of items for resale, including apps. Free use requires attribution; Premium removes it. Prices are not given in the terms — [Magnific/Freepik terms](https://www.magnific.com/legal/terms-of-use)

### Inferences
- With a Standard licence, a shop's customers looking at a thumbnail in a bill are viewers, not licensees, which is probably fine. Unclear: thousands of shop accounts each "using" the image in their own storefront could be read as sublicensing or multi-seat use. LAWYER, or ask each vendor in writing.
- Rough cost: the master list would have about 2,000–5,000 items. iStock Extended at about 18 extra credits per image, or Adobe at about $80 per image, comes to roughly $160k–$400k at 2,000–5,000 images. That is not realistic, so negotiate an enterprise or platform deal or use paid stock only for a few hero images.
- Freepik is effectively unsuitable for a shared master catalogue.

### Gaps
- Depositphotos and 123RF were not researched (out of tool budget). Both are known to sell Extended licences that may cover "unlimited users"; check their licence pages.
- Prices in INR, and current 2026 prices from the official pages, could not be verified (403 errors).
- Did not look for a written FAQ entry from any vendor on SaaS catalogue use.

## Indian stock agencies and India coverage

### Takeaway
No pricing or licence data was found for Indiapicture, ImagesBazaar or Stock Photo India. Getty.in prices individual royalty-free Indian images at around Rs 14,000, far too much for a large catalogue.

### Cited Findings
- Getty India lists a royalty-free Indian street-vendor image at Rs 14,000, and a brick-kiln image at up to $375 — [Getty Images India](https://www.gettyimages.in/detail/photo/indian-street-vendor-preparing-food-chapatti-flat-royalty-free-image/499781514); [Getty](https://www.gettyimages.com/detail/photo/loading-bricks-from-the-kiln-royalty-free-image/641171292)
- ImagesBazaar claims to dominate the Indian market (older article) — [Selling Stock](https://www.selling-stock.com/Article/imagesbazaarcom-claims-india-market-dominance)

### Inferences
- Agencies focused on India sell people and lifestyle images, not white-background product photos of binding wire or loose dal. Commissioning a photographer, or taking the photos in-house (a one-day studio shoot in a kirana and hardware market), gives full ownership and probably costs less than paid stock for hundreds of items. LAWYER: use a written assignment of copyright from the photographer.

### Gaps
- Indiapicture, Stock Photo India and Dreamstime licence terms were not fetched.

## Branded packs and trademarks

### Takeaway
Avoid photos that show brands (an UltraTech cement bag, Tata Tiscon markings) for generic items. Pixabay bans commercial use of recognisable brands, iStock and Getty mark many such images editorial-only, and editorial images cannot be used commercially.

### Cited Findings
- Pixabay prohibits commercial use of content with recognisable trademarks or brands for goods or services — [Pixabay](https://pixabay.com/service/license-summary/)
- iStock: editorial-only content may not be used for commercial or promotional purposes; content may not be used as a trademark — [iStock licence](https://www.istockphoto.com/legal/license-agreement)

### Inferences
- A generic item ("Cement bag 50 kg") should use a plain, unbranded bag photo, or a photo with the logo blurred or removed. Showing a brand's photo against a generic item could suggest a false link to the brand, a passing-off or trademark risk under India's Trade Marks Act 1999 (LAWYER).
- For a branded SKU ("UltraTech OPC 53"), the safest option is a photo of the actual product, taken by the shop itself or supplied by the brand or distributor. Showing an accurate product photo to identify a genuine product being sold is generally lower risk, but confirm with a lawyer.

### Gaps
- No Indian case law on stock photos of trademarked packaging was found.

## Practical recommendation

### Takeaway
Use free sources (Unsplash, then Pexels and Pixabay, then Commons CC0/CC BY) for generic items, in-house or commissioned photos for Indian-specific loose and building materials, and paid stock only under a written platform licence.

### Cited Findings
- See the sections above for the licence terms each source rests on — [Unsplash](https://unsplash.com/license); [Pixabay](https://pixabay.com/service/license-summary/); [iStock](https://www.istockphoto.com/legal/license-agreement); [Freepik](https://www.magnific.com/legal/terms-of-use)

### Inferences
- Generic hardware (screws, hinges, paint brushes, binding wire): Unsplash, Pexels or Pixabay, with no brand visible.
- Loose kirana goods (chana, rice, sugar, dal): Unsplash or Pexels have heaps and bowls; for Indian varieties, run your own shoot.
- Building materials (10/20/40 mm aggregate, sand, bricks, TMT): Commons (credited) or your own shoot at a building-materials yard. These are cheap to photograph and you own the result.
- Branded packs: unbranded or blurred photos for generic items; shop or brand-supplied photos for branded SKUs.
- Engineering: store a licence and credit field for each image, show a credits page, and give no raw-download endpoint. LAWYER: the "competing service" clause, Standard licences across many tenants, and photographer assignment contracts.

### Gaps
- Depositphotos, 123RF, Dreamstime and Indian agencies need follow-up research.
