# Own shoot vs AI-generated photos for a 3,000–4,000 item master list (India, 2026)

Not legal advice. Points marked [LAWYER] need checking by an Indian IP / consumer-law lawyer.

## In-house shooting: kit, cost, throughput, outsourcing, background removal

### Takeaway
A basic kit (LED light tent plus a recent phone) costs about ₹2,300–₹20,000 in India. Indian studios charge about ₹200–350 per product for a white-background shot, so outsourcing 3,500 items would cost about ₹7–12 lakh at list rates, before any bulk discount. Background-removal APIs cost about $0.01–$0.21 per image, which is small next to labour.

### Cited Findings
- Boxtudio 40x40x40 cm LED light box with front cover: Rs 2,299, taxes included — [Boxtudio](https://boxtudio.in/products/boxtudio®-lightbox-with-front-cover-tabletop-portable-photostudio-product-photography-light-tent-40x40x40cms)
- Neewer 16-inch tabletop light box kit with 4 backdrops: ₹13,625; Neewer 24x24-inch tent with 120 dimmable LEDs: ₹20,380 (both include import duty) — [Tanotis](https://tanotis.com/products/neewer-tabletop-photo-studio-light-box-kit-with-4-color-backdrops-17), [Tanotis](https://www.tanotis.com/products/neewer-tabletop-photo-studio-light-tent-24-x-24)
- HIFFIN portable light tent with built-in LEDs sold at Croma (price not captured) — [Croma](https://croma.com/hiffin-portable-light-tent-built-in-led-lights-/p/252237)
- Gujarat studio: ₹200–350 per product for white-background shots; packages ₹2,500 for 10, ₹4,999 for 25, ₹8,999 for 50 products; ₹300–500 per product for 3–5 angles; "bulk shoots (100+ products) unlock meaningfully lower per-image rates" — [Kenit Patel Photography](https://kenitpatelphotography.pages.dev/product-photography-ahmedabad), [Ahmedabad guide](https://kenitpatelphotography.pages.dev/blog/product-photography-in-ahmedabad-guide-ecommerce). Caveat: this is one studio's marketing page.
- IndiaMART Mumbai listings: about ₹250–350 per piece for plain white background; one package from ₹1,500 per product (photo plus editing) — [IndiaMART](https://dir.indiamart.com/mumbai/product-photography.html)
- US benchmark for basic white-background work: $25–75 per image — [Squareshot](https://www.squareshot.com/post/per-image-pricing)
- Photoroom API: Remove Background about $0.02/image (Basic $20/month at 1,000 images); Image Editing about $0.10/image; partner rate $0.01/image at 100K+/month; enterprise needs a 200K-image annual minimum — [Activepieces guide](https://www.activepieces.com/blog/photoroom-pricing-all-plans-and-costs-2026-guide), [eesel](https://www.eesel.ai/blog/photoroom-pricing). These are third-party summaries.
- remove.bg: 1 credit = 1 full-resolution image; about $1.99 for a single credit, falling to about $0.21/credit at 8,000 credits for $1,699; credits last 2 years; subscriptions from about $9 for 40 credits — [remove.bg pricing](https://remove.bg/zh/pricing); 2019 note that small images cost 0.25 credit — [remove.bg blog](https://remove.bg/b/remove-more-backgrounds-for-less-credits)

### Inferences
- Outsourcing cost for 3,500 items at ₹200–350 each is about ₹7–12.25 lakh. A bulk deal could plausibly come in lower, but no source gives a bulk rate.
- An in-house shoot needs a ₹15–25k kit plus staff time. The real limit is getting hold of physical samples: aggregates, sand, TMT bars, binding wire coils and cement bags are bulky and dusty, and not suited to a tabletop tent. Those need a small sample in a bowl or tray, or a site or shop photo.
- Background removal at about ₹2 per image (Photoroom basic) is negligible: about ₹7,000 for 3,500 images. Use one template (square, pure white #FFFFFF, product centred, filling about 80–85% of the frame, soft shadow) to keep the set consistent.

### Gaps
- No reliable photos-per-day figure was found. My estimate, unsourced: 40–100 simple items per day per person in a tent.
- No Fiverr or Upwork India per-image rates were retrieved.
- Commercial-use clauses of the remove.bg, Photoroom and Clipdrop terms were not read. Clipdrop pricing was not found.

## AI-generated images: models, cost, indemnity, quality

### Takeaway
At list prices, generating images costs about $0.02–$0.19 each. Even with 3–4 attempts per kept image, 3,500 images comes to roughly $300–$2,700, far below a shoot. Adobe Firefly (enterprise) and Google Imagen on Vertex AI offer the strongest IP indemnity. Quality for very specific trade items (20 mm aggregate, binding wire coil) is unverified and needs human checking.

### Cited Findings
- OpenAI gpt-image-1: about $0.02 / $0.07 / $0.19 per square image at low / medium / high quality (2025 figures). Newer gpt-image versions exist, so check current pricing — [PetaPixel](https://petapixel.com/2025/04/24/chatgpts-popular-new-image-generator-coming-to-adobe-figma-canva-and-more)
- Imagen 4 on Vertex AI: Fast $0.02, Standard $0.04, Ultra $0.06 per image. A snapshot of the pricing page notes a discontinuation date of June 30, 2026, so check what is available now — [Wireflow](https://www.wireflow.ai/blog/imagen-4-pricing), [st-hakky](https://book.st-hakky.com/en/data-science/imagen4-api-pricing-gemini-vertex-ai-comparison)
- Gemini image models are billed per token. A third-party estimate puts Gemini 3 Pro Image at about $0.134 per image — [laozhang blog](https://blog.laozhang.ai/en/posts/ai-image-api-pricing-comparison)
- Adobe: "eligible enterprise customers can also receive IP indemnification for select Adobe Firefly outputs". Indemnity is limited to eligible features and plans — [Adobe Firefly product description](https://helpx.adobe.com/legal/product-descriptions/adobe-firefly.html), [Computerworld](https://www.computerworld.com/article/1628682/adobe-offers-copyright-indemnification-for-firefly-ai-based-image-app-users.html)
- Firefly indemnity does not cover partner models (Imagen, GPT) used inside Firefly. Imagen on Vertex has its own indemnity, described as the most complete. OpenAI covers copyright only, on the API or Enterprise tier — [BERI comparison](https://www.beri.net/article/best-ai-image-generator-indemnity-firefly-vertex-imagen-openai-getty-shutterstock) (secondary source)
- Firefly Pro: $19.99/month for 4,000 credits; one Firefly Image 5 text-to-image costs 10 credits (so about 400 images a month) — same BERI source

### Inferences
- Generic loose goods (rice, chana, sugar, dal, whole spices in a bowl or heap; screws, hinges, padlocks, bricks, sand heaps) are common in training data and likely to render well.
- Items with a technical spec are risky. Aggregate size grades (10/20/40 mm), TMT rib patterns, wire gauge, screw head and thread types, and cement bags (which come with brand prints) can come out subtly wrong. Showing a 40 mm stone as "20 mm" misleads a buyer. Put a size reference or a caption on such images, or photograph them.
- Models tend to invent logos or text on packs. Prompt for "plain unbranded" and check every image for text or logos (trademark risk).

### Gaps
- No hands-on quality tests for Indian trade items were found. A pilot of about 50 items is advised.
- Stable Diffusion and Flux licence terms were not researched (Flux dev weights are non-commercial; Flux pro is via API — check before use).

## Legal position in India

### Takeaway
The Copyright Act 1957 assumes a human author. Purely AI-generated images may not be protected, so the company may not be able to stop others copying them. The law is under review in 2025–26. Using them is generally allowed under the vendor's terms; owning them exclusively is uncertain.

### Cited Findings
- Indian law does not recognise non-human authorship; works with no meaningful human creative input may fall outside the Act — [S.S. Rana](https://ssrana.in/articles/who-owns-what-the-machine-makes-authorship-and-ownership-of-ai-generated-works-under-indian-law/), [Khurana & Khurana](https://www.khuranaandkhurana.com/ownership-of-ai-generated-content-a-deep-dive-into-copyright-law-in-india)
- In May 2025 the Ministry of Commerce set up an expert panel; a new chapter on AI works is being considered; DPIIT issued a working paper in Dec 2025 (not law) — [K&S Partners](https://ksandk.com/intellectual-property-rights/ai-generated-content-copyright-ownership-india/), [Mondaq](https://www.mondaq.com/india/patent/1787252/the-missing-author-artificial-intelligence-and-the-limits-of-copyright-law)
- Suryast (AI-assisted artwork) registration with a human co-author is the main precedent; accounts differ on the details. A reported April 2026 Delhi HC direction to the Copyright Office is unconfirmed — [JIPR](https://or.niscpr.res.in/index.php/JIPR/article/view/5862), [LAEX](https://laex.in/prelims-fact-sheet/ai-copyright-authorship-india/)

### Inferences
- Shooting in-house (or commissioning under a written assignment, Section 17 / work for hire) gives clear copyright ownership. AI images give use rights but weak exclusivity [LAWYER].
- Human editing (compositing, retouching, curation) may strengthen a claim to authorship [LAWYER].
- Labelling: show "Representative image; actual product may vary" on every master-list photo. Optionally keep an internal "AI-generated" flag. Check the CCPA misleading-ad guidelines (2022) and any MeitY synthetic-content labelling rules (draft IT Rules amendment, 2025) [LAWYER]. Not verified here: gap.

### Gaps
- MeitY 2025 synthetic-content labelling rules and whether they apply to product images were not verified.
- The outcome of the Delhi HC matter is unknown.

## Hybrid workflow and which method for which item

### Takeaway
Use AI with human review for generic loose and commodity items. Photograph spec-sensitive and trade items. Never generate branded packs, medicines or regulated goods with AI.

### Inferences (recommendation; no further sources)
- AI with human QA: loose kirana goods (grains, dal, sugar, whole and ground spices in bowls), generic produce, plain hardware (nails, screws, hinges, plain locks), bricks, sand heap, generic paint brushes and rollers, generic beauty tools (combs, cotton pads). Budget 3–4 generations per item and reject anything with text or logos.
- Photograph, or AI only with strict QA plus a size caption: aggregates 10/20/40 mm (shoot a handful beside a ruler), TMT bars, binding wire, pipes and fittings by size, screw types, paint shade swatches (colour accuracy matters).
- Never AI: branded packs (Tata salt, cement brands, Asian Paints tins, cosmetics brands). Use brand-supplied images or the shopkeeper's own photo, because a made-up pack is misleading and a trademark risk. Also never AI: medicines and drugs, food items with FSSAI labels, anything with an MRP or label. Leave these with no image, or use a neutral category icon.
- Process: generate, then reviewer checks (correct item, no text or logos, realistic size), then normalise (Photoroom: white, square, centred), then store with provenance metadata (model, prompt, date, reviewer). Shoot about 100–200 key items in-house as references and for the hardest categories.
- Rough cost: AI for about 3,000 items at about $0.04 × 4 tries is about $480 (≈₹40k) plus review time; in-house shoot of about 500 items is a ₹20k kit plus staff time; outsourcing 500 items at ₹250 is about ₹1.25 lakh.

### Gaps
- No measured AI acceptance rates for these item types.
