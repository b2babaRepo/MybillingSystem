# Platform patterns for sharing one product photo across many shops

Researched November 2026. The search budget was limited, so primary sources are thin for Flipkart, JioMart, Udaan, ElasticRun, Jumbotail, GoFrugal, Marg and OkCredit. Those platforms are listed under Gaps.

## How catalogue platforms handle shared images

### Takeaway
Amazon is the reference model. It keeps one detail page per product, and every seller's offer sits on that page. Amazon's system picks which contributed image is shown, and sellers license their content to Amazon. Indian billing apps such as myBillBook and Vyapar ship item libraries, but I found no proof that those libraries include photos. Shop photos in those apps are mostly uploaded by each shop for its own online catalogue.

### Cited Findings
- Amazon's rules say each detail page must cover a single unique item. When several authorised contributors conflict, Amazon's automated system may choose what is displayed, using signals such as sales, refund rate and buyer feedback. Amazon does not mediate between contributors and does not say whose contribution is displayed. — [Amazon Product detail page rules](https://sellercentral.amazon.com/help/hub/reference/external/G202024200)
- Amazon says that when required images are missing, it may add images from other selling partners or from Amazon itself. Seller forum posts describe Amazon combining the "best" data from several sellers. — [Amazon Image issues](https://sellercentral.amazon.com/gp/help/external/GT8RSE9S9NK4LP6Z); [ValueAddedResource](https://valueaddedresource.net/amazon-product-images-from-multiple-sellers)
- Sellers often find their images changed or overwritten on shared listings. This is a frequent complaint. — [hustlegotreal FAQ](https://hustlegotreal.com/faq/amazon/amazon-detail-page-changed-why-were-my-title-images-or-bullets-overwritten); [Seller forum: another seller is using my images](https://sellercentral-europe.amazon.com/seller-forums/discussions/t/043010990542b754eb37a5cc7bff8380)
- myBillBook advertises a library of more than 1 lakh FMCG and pharma items for adding inventory quickly. — [myBillBook help](https://mybillbook.featurebase.app/en/help/articles/4381436-how-to-add-and-manage-inventory-in-mybillbook)
- Vyapar lets shops import items from an Items Library covering pharma, electronics and FMCG. Separately, shops add items to the Vyapar online store with their own images and prices, then share the store as a link. — [Vyapar features](https://vyaparapp.in/blog/vyapar-app-features/)
- Swipe lets shops add images to their own products and share catalogues on WhatsApp. — [Swipe](https://getswipe.in)
- The Kirana Fast app claims more than 2 million preloaded products, and one user review says the products come with pictures. Neither claim is verified. — [App Store listing](https://apps.apple.com/in/app/kirana-fast/id1567324958?l=hi)
- ONDC sellers on GoFrugal upload front and back item images named by SKU code. The back image must not show the expiry date. — [GoFrugal ONDC v1.2 guide](https://community.gofrugal.com/portal/en/kb/articles/ondc-v1-2-configuration-guide)
- The MartPe ONDC buyer app requires at least one image of at least 300x300 px, and rejects placeholder images and seller logos. — [MartPe](https://www.martpe.in/product-sorting-parameters)
- Shopify product images belong to each store. They are served from a per-store CDN path, for example `/cdn/shop/files/x.jpg?v=1683744744&width=450`. The `v` timestamp works as a cache-buster, and `width` resizes on the fly up to 5760 px, never larger than the original. — [Shopify image_url docs](https://shopify.dev/docs/api/liquid/filters/image_url)
- Open Food Facts product photos are licensed CC BY-SA (deed 3.0), and the database is ODbL. The project warns that photos may contain third-party copyrighted elements, such as brand artwork. — [OFF data page](https://github.com/openfoodfacts/openfoodfacts-web/blob/main/lang/en/texts/data.html); [OFF licence tutorial](https://openfoodfacts.github.io/openfoodfacts-server/api/tutorials/license-be-on-the-legal-side)

### Inferences
- There are two archetypes. A marketplace keeps one canonical page and the platform picks the image; Amazon works this way. A store builder gives each store its own images; Shopify, Vyapar and Swipe work this way. Grocery Khata's master list plus per-shop items fits a hybrid: a platform-curated canonical photo with an optional per-shop override.
- Amazon's experience shows the main pitfall of letting many sellers contribute to one shared image. Contributors fight over it, images get overwritten, and nobody knows who is responsible. A curated model, where only the admin decides the master photo and shops can only suggest, avoids this.
- Open Food Facts is a legitimate source, and Grocery Khata already uses it. The share-alike licence needs attribution and could arguably extend to adapted images, so store the source and licence with each photo and show credit where it is practical.

### Gaps
- I found no primary pages on how Flipkart, JioMart, Udaan, ElasticRun or Jumbotail share images, or whether ONDC has a shared catalogue image standard. The network-level ONDC image specification was not found.
- I could not confirm whether the myBillBook or Vyapar item libraries include photos, or where those photos come from.

## Inheritance designs: copy-on-pick, live link, override, versioning

### Takeaway
No platform documented this directly. Shopify's versioned CDN URL (`?v=timestamp&width=N`) is a good precedent for cache keys. The recommendation below is a design inference: a live link by default, with a per-shop override.

### Cited Findings
- Shopify adds a version (`v=`) to image URLs and resizes on request through a `width` parameter. Re-encoding means the output is not byte-identical. Building these URLs by hand is discouraged. — [Shopify image_url](https://shopify.dev/docs/api/liquid/filters/image_url); [Shopify community](https://community.shopify.com/t/changing-image-size-by-url/7352)
- On Amazon, a change to the shared image appears for every seller on the ASIN at once, because there is one detail page. — [Amazon detail page rules](https://sellercentral.amazon.com/help/hub/reference/external/G202024200)

### Inferences (recommended design)
- **Live link by default.** A shop item that comes from the master list stores `master_item_id`. When the shop has no override, it shows the master photo. Admin fixes then reach every shop immediately, which is the Amazon-like benefit, without copying files per shop.
- **Shop override.** A shop item gets an optional `shop_photo_id`. The order is: the shop's override, then the master photo, then a category placeholder. The override is private to that shop unless the shop opts in to share it.
- **Content-addressed files.** Name each stored file by a hash of its content, for example `/img/{sha256}/{320|640}.webp`, and serve it with `Cache-Control: immutable` and a one-year max-age. When the admin replaces a master photo, the master row points to a new hash, so caches never go stale and old bills can keep the old file if needed. This follows the same idea as Shopify's `v=`.
- **Bills.** For printed or PDF bills, either snapshot the photo hash at bill time or leave photos off bills. Most Indian thermal bills carry no images anyway.
- **History.** Keep master photo history (who changed it, when, the old hash) so a bad update can be rolled back.

### Gaps
- I found no public documentation of how the Indian billing apps propagate library updates to shops.

## Image standards for a master library

### Takeaway
Amazon's main-image rules are the de facto standard: pure white background, product only, and no text, logos or props. The often-quoted "product fills 85% of the frame" figure comes from third-party guides, not from an Amazon page I could verify. In India, ONDC buyer apps set a floor of 300x300 px.

### Cited Findings
- Amazon's main image needs a pure white background (RGB 255,255,255). Alternate images may use other backgrounds. Non-compliant main images can get a listing suppressed. — [Amazon Non-white background help](https://sellercentral.amazon.com/gp/help/external/G75PWC4THA8J269P); [Image issues](https://sellercentral.amazon.com/gp/help/external/GT8RSE9S9NK4LP6Z)
- Third-party guides say the product should fill at least 85% of the frame, with no text, logos or props, and at least 1000 px on the longest side so zoom works. These figures are not confirmed on an official Amazon page. — [Photoroom guide](https://www.photoroom.com/blog/amazon-product-images); [imresizer 2026 guide](https://imresizer.com/blog/amazon-product-image-requirements-2026)
- MartPe requires at least 300x300 px with no placeholders or logos. GoFrugal asks for front and back images, with no expiry date in the back image. — [MartPe](https://www.martpe.in/product-sorting-parameters); [GoFrugal](https://community.gofrugal.com/portal/en/kb/articles/ondc-v1-2-configuration-guide)

### Inferences
- Suggested master standard: a square 1:1 image on a white or near-white background, front of pack, product filling about 85%, no watermark, price sticker or hand. Store the source at 1000 px or more, and serve 320 px and 640 px WebP versions; the app already makes 320 px copies.
- Show the pack size in the item name, not in the photo, so one photo can serve variants only when the packs really look the same. Otherwise keep one photo per pack size, because a wrong pack size is the most common "wrong photo" case.

### Gaps
- An official Amazon source for the 85% figure and the 1000 px minimum was not retrieved.

## Terms language: platform images and shop-contributed photos

### Takeaway
Marketplaces take a broad, royalty-free, worldwide, non-exclusive licence over seller-supplied materials, so the platform can show them to everyone. Grocery Khata needs two clauses. The first is a limited licence from the platform to shops for the master photos. The second is a licence from a shop to the platform, with explicit opt-in consent, before a shop's photo is shared with other shops.

### Cited Findings
- Amazon's Business Solutions Agreement has sellers grant Amazon a royalty-free, non-exclusive, worldwide licence to use their materials for Amazon's services. Some versions say "irrevocable", and the duration runs for as long as the seller's rights last. Whether the licence is sub-licensable is disputed between sources. — [BSA PDF (archived copy)](https://abilityone.gov/laws,_regulations_and_policy/documents/Amazon%20Services%20Business%20Solutions%20Agreement.pdf); [Amazon Sellers Lawyer on the licence clause](https://amazonsellerslawyer.com/?p=17315)
- Amazon's Conditions of Use for user content are broader: perpetual, irrevocable and fully sub-licensable. — [ConductAtlas summary](https://conductatlas.com/platform/amazon/amazon-conditions-of-use/perpetual-sublicensable-user-content-license/)
- Sharing an ASIN does not transfer copyright between sellers. Content is licensed to Amazon, not to the other sellers. — [Third Chair blog](https://usethirdchair.com/blog/amazon-copyright-rules-for-sellers-authors-and-brands)
- Open Food Facts contributors place their photos under CC BY-SA, and people who reuse them must credit the contributors. — [OFF licence tutorial](https://openfoodfacts.github.io/openfoodfacts-server/api/tutorials/license-be-on-the-legal-side)

### Inferences (draft language, not legal advice; have a lawyer review)
- *Platform images:* "Grocery Khata grants you a non-exclusive, non-transferable, revocable licence to display master-list images only within your use of the Service (your bills, catalogue, online store and WhatsApp shares) while your account is active. Product names, logos and packaging shown remain the property of their brand owners. Some images are supplied under third-party licences (for example CC BY-SA from Open Food Facts) and carry their attribution."
- *Shop uploads:* "You confirm you took the photo or have the right to upload it. You keep ownership. You grant Grocery Khata a worldwide, royalty-free, non-exclusive licence to store, resize and display it for your shop. If you tick 'Share this photo with other shops', you also grant Grocery Khata a perpetual, sub-licensable licence to use it in the master list for all shops. That licence survives deletion of your account, but you may ask us to remove the photo from the master list."
- Make sharing opt-in for each photo, or at least off by default. Never reuse a shop's photo silently, because forum complaints on Amazon show that sellers resent it.

### Gaps
- I did not retrieve the current Indian BSA text from Amazon.in, or the Flipkart seller terms.

## Moderation: reporting wrong images and takedown

### Takeaway
Amazon handles image problems through automated compliance checks, listing suppression and seller-reported "image issues". It does not mediate between contributors. Under India's intermediary rules, a SaaS that hosts user images needs a grievance or takedown route. That point is an inference and was not researched in depth here.

### Cited Findings
- Amazon suppresses listings that have non-compliant main images until a compliant image is supplied. It runs an "Image issues" help flow for sellers. — [Amazon Image issues](https://sellercentral.amazon.com/gp/help/external/GT8RSE9S9NK4LP6Z); [Nightjar on suppression](https://nightjar.so/blog/amazon-listing-suppressed-main-image)
- Amazon does not decide disputes between contributors and does not reveal whose content is shown. — [Amazon detail page rules](https://sellercentral.amazon.com/help/hub/reference/external/G202024200)

### Inferences (recommended flow)
- A "Wrong photo" button for each item, which already exists in the app. A report records a reason: wrong product, wrong pack size, poor quality, or copyright/brand complaint. When several shops report the same item, it goes higher in the admin queue. One report hides the photo only for the reporting shop, by making the shop's view fall back to the placeholder. Once reports pass a threshold, or after admin review, the master photo is replaced or removed for everyone.
- Use a separate copyright or brand takedown channel with an email address and a defined turnaround. Remove the photo from the master list and keep a record of the action.
- Log every master-photo change with the old hash, so the admin can roll it back.

### Gaps
- The deadlines under India's IT Rules 2021 for intermediary takedown and grievance handling were not verified in this session.
