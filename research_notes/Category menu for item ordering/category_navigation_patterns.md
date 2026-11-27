# Category Navigation Patterns in Indian Quick-Commerce and Grocery Apps (2025-2026)

Method note: research ran on 2026-11-27. WebFetch was blocked for medium.com, baymard.com and bigbasket.com in this session ("host not allowed"), so every finding below comes from search-result snippets of the cited pages, not from full-page reads. Treat counts and labels as "as indexed by the search engine". Category labels vary by city and over time (several sources say so). Where a detail comes from a student or freelance teardown, a clone repo or an aggregator rather than the company, it is marked **[third-party]**. Statements under "Inferences" that rely on general familiarity with the apps, with no source found here, are marked **[unverified]**.

## How is the category screen structured on each app (rail vs tabs vs grid; levels; tile sizes; images vs icons)?

### Takeaway
The dominant Indian quick-commerce pattern has two parts. A "Categories" tab (or a home-screen section) shows grouped image tiles: department headings with a grid of category tiles that use product-photo thumbnails. Tapping a tile opens a listing screen with a **narrow vertical left rail of sub-categories (thumbnail plus short label, active item highlighted)** and a 2-column product grid on the right. Blinkit's own back end uses explicit L0/L1/L2 levels. The user's screenshot (left rail of departments, right side of grouped sub-category tiles) is a variant that moves the left rail up one level, onto the departments.

### Cited Findings
- **Blinkit, levels:** Blinkit's CMS maps products to parent categories (L0), with sub-categories at L1 and L2. Some collections hide sub-categories in the app. **[third-party: designer portfolio on Blinkit CMS]** — [Blinkit CMS, Prakhya Kant](https://www.prakhyakant.com/blinkit-cms)
- **Blinkit, URL structure:** public web URLs encode a two-number category ID, category ID then sub-category ID. Examples: `/cn/atta-rice-dal/atta/cid/16/1165`, `/cn/atta-rice-dal/rice/cid/16/1161`, `/cn/atta-rice-dal/toor-urad-chana/cid/16/1010`, `/cn/grocery-staples/edible-oils-ghee/cid/16/917`, `/cn/grocery-staples/salt-sugar/cid/16/933`, `/cn/masala-oil-more/dry-fruits/cid/1557/1160`. This confirms a category → sub-category model where every sub-category page sits under one parent ID. Some sub-categories are reachable under more than one slug (`grocery-staples/pulses` and `atta-rice-dal/toor-urad-chana` both map to 16/1010). — [Blinkit Atta](https://blinkit.com/cn/atta-rice-dal/atta/cid/16/1165); [Blinkit Toor, Urad & Chana](https://blinkit.com/cn/atta-rice-dal/toor-urad-chana/cid/16/1010); [Blinkit Dry Fruits](https://blinkit.com/cn/masala-oil-more/dry-fruits/cid/1557/1160)
- **Blinkit, category screen with sidebar:** a UX case study describes the category screen as options in a sidebar, with a soft green background marking the selected category. After choosing a main category, the user picks a sub-category such as seasonal or exotic vegetables. The author proposed cutting the sidebar to fewer than 7 items, citing Miller's Law. **[third-party, Medium design-bootcamp]** — [Enhancing the UX of Blinkit app](https://medium.com/design-bootcamp/enhancing-the-user-experience-of-blinkit-app-a-ux-ui-case-study-8bc70ff6a0e4)
- **Blinkit-style layout spec:** a narrow, sticky left rail of sub-category links on mobile, an active-sub-category highlight, truncated labels, and a 2-column product-card grid. **[third-party: v0 clone brief and SwiftUI clone repo; the selected category is highlighted in the left list]** — [v0 Blinkit-style project](https://v0.app/deliverylads23-4818s-projects/chat/duplicate-of-new-version-15-aug-TuPMmBwbQCw); [GitHub Blinkit clone](https://github.com/Mehul721/-BlinkitAssignment)
- **Blinkit home-screen categories:** a GrowthX teardown asks whether all category images should be on the first screen when a dedicated category page exists, and suggests showing only the top 8–12 categories there. **[third-party]** — [GrowthX Blinkit onboarding](https://growthx.club/proof-of-work/edtech/growthx/break-down-onboarding-secrets-with-blinkit|6746c88f4a0737ca03d7df8c)
- **Zepto:** the web category pages show a sub-category list per category, and the search snippets name the counts. "Tea, Coffee & More" has 11 sub-categories. "Masala, Dry Fruits & More" has 8. "Packaged Food, Noodles & Ready Meals" has 10. One snippet mentions a sidebar on the category page. — [Zepto Tea, Coffee & More](https://www.zepto.com/category/tea-coffee-more); [Zepto Masala, Dry Fruits & More](https://www.zepto.com/category/masala-dry-fruits-more); [Zepto Packaged Food](https://www.zepto.com/category/packaged-food)
- **Zepto, designer account:** Zepto has a category list on the home screen for quick navigation, plus a dedicated category screen that shows promotions and new launches. **[agency portfolio: Brucira]** — [Brucira Zepto case study](https://brucira-migration.pages.dev/case-studies/zepto)
- **Zepto, usability observations from case studies [third-party]:** one tester scanned category titles and paid little attention to category images. Another expected an Amazon-style top category bar. Item counts in the browse-category menu confused people. Some grocery sub-categories took too many taps to reach. — [Zepto UX case studies via Medium/Nextleap](https://assets.nextleap.app/submissions/HeuristicEvaluationofZepto_UXCaseStudyofanOnlineGroceryApp_byAkshhatJaiin_Apr2025_Medium-d31f9cf7-6cf9-4938-9723-540fdb8bda05.pdf); [Arsha Treesa Zepto UX review](https://arshatreesa.framer.website/blog/zepto-ux-analysis-a-comprehensive-review)
- **Swiggy Instamart:** web URLs follow `/instamart/city/{city}/sc/{category}/{sub-category}-{id}`, for example `sc/masalas-and-dry-fruits/masalas-and-pastes-…` and `sc/masalas-and-dry-fruits/papad-and-fryums-…`. The page title reads "Masalas and Dry Fruits – N items – List of all available categories", and N differs by city: 810 in Indore, 1,110 in Mumbai, 1,375 in Mysore, 1,854 in Bangalore. So the tree and assortment are per city or dark store. — [Instamart Bangalore Masalas](https://swiggy.com/instamart/city/bangalore/sc/masalas-and-dry-fruits/masalas-and-pastes-6188c4c236324011970cae23); [Instamart Indore](https://swiggy.com/instamart/city/indore/sc/masalas-and-dry-fruits/masalas-and-pastes-6188c4c236324011970cae23); [Instamart Mumbai](https://swiggy.com/instamart/city/mumbai/sc/masalas-and-dry-fruits/masalas-and-pastes-619f21efb6244b11ebb4f7b6)
- **Instamart:** the app-store copy claims about 35 categories. A student teardown found that most users spend their time browsing categories, and many don't know about products outside groceries. **[third-party]** — [mwm.ai Instamart listing](https://mwm.ai/apps/instamart-groceries-more/6738619733); [Nextleap Instamart submission](https://assets.nextleap.app/submissions/NLInstamart-b491df67-a706-4695-ae28-957845ec46ce.pdf)
- **BigBasket:** "Foodgrains, Oil & Masala" is a top-level department, and a menu lists its sub-categories directly. BigBasket also uses a sidebar-style menu with labels such as "Dals & Pulses", "Masalas & Spices" and "Noodle, Pasta, Vermicelli". — [BigBasket Foodgrains, Oil & Masala](https://www.bigbasket.com/cl/foodgrains-oil-masala/?nc=nb)
- **Flipkart Minutes:** a 2026 guide lists flat top-level categories: fruits, veggies, dairy, bakery & eggs, sweet tooth, grocery, beauty, personal care, plus non-grocery lines (gadgets, mobiles, fashion, kitchen, cleaning, electronics, toys, appliances, gifting, wedding). **[third-party: DesiDime]** — [DesiDime 10-min apps 2026](https://www.desidime.com/news/top-5-instant-10-min-grocery-delivery-apps-in-india)
- **JioMart:** top-level categories include electronics, home & lifestyle, fashion, grocery, premium fruits, industrial & professional, jewellery and "JioMart Kirana". **[third-party]** — [DesiDime 10-min apps 2026](https://www.desidime.com/news/top-5-instant-10-min-grocery-delivery-apps-in-india)
- **DMart Ready:** top-level categories are Grocery, DMart Grocery, Dairy & Beverages, Packaged Food, Fruits & Vegetables, Beauty & Cosmetics, Home & Kitchen, Personal Care, Baby & Kids, Clothing & Accessories, Appliances, Footwear, School Supplies and Specials. **[third-party, 2023]** — [DesiDime grocery apps 2023](https://www.desidime.com/news/best-online-grocery-ordering-apps-in-india)
- **Amazon Fresh:** now deeply integrated into the main Amazon app, with 2-hour slots in many cities. No category-screen detail was found. — [DesiDime 10-min apps 2026](https://www.desidime.com/news/top-5-instant-10-min-grocery-delivery-apps-in-india)
- **PhonePe Stores (adjacent):** a case study describes three-level category navigation. **[third-party]** — [PhonePe Stores case study](https://medium.com/@surabhimanchalwar/phonepe-stores-81547e01685a)

### Inferences
- The common structure across Blinkit, Zepto, Instamart and BigBasket is **department → category → sub-category**. The department level is often only a visual grouping heading on the categories screen (for example "Grocery & Kitchen"), not a separate tap. Categories are the tappable tiles. Sub-categories live in the listing screen's left rail. **[unverified for exact department headings; the category and sub-category levels are confirmed by URLs]**
- Typical tiles (from general familiarity) **[unverified]**: categories screens use 3–4 tiles per row with a product-photo collage on a light pastel square and a 2-line label underneath. The listing-screen rail is about 20–25% of screen width, with a small circular or rounded thumbnail above a 1–2 line label.
- The user's screenshot (departments in the left rail, grouped sub-category tiles on the right) resembles the Amazon India / Flipkart "All categories" screen more than the Blinkit or Zepto categories tab. Both patterns use a left rail, but at different levels. **[inference]**
- For a POS item picker, the URL evidence suggests a strict 2-level tree (category → sub-category) with image tiles, which is enough for kirana. A third level should appear only inside large categories. **[inference]**

### Gaps
- I couldn't load or verify current official screenshots of each app's categories tab, including exact department headings, tile dimensions and tiles per row. Fetching was blocked, and search returned no screenshot descriptions.
- No source here describes Flipkart Minutes, Amazon Fresh, JioMart or DMart Ready category screens at the layout level (rail vs grid).
- No source described the Instamart left-rail labels directly. One search returned BigBasket's sidebar instead.

## What does a typical grocery category tree look like? (concrete trees from at least 3 apps)

### Takeaway
All the apps converge on very similar kirana groupings: Atta/Rice/Dal (sometimes with oil), Masala/Dry Fruits (sometimes with oil), Tea/Coffee/Drinks, Packaged/Instant Food and Noodles, and Snacks/Munchies. Sub-categories are split by product type, for example "Toor, Urad & Chana" vs "Moong & Masoor", and "Powders & Pastes" vs "Dry Fruits & Nuts". Each app also adds a "Top Picks" or "Hot Deals" pseudo-sub-category at the top.

### Cited Findings
**Blinkit (from web URLs and page titles):**
- *Atta, Rice & Dal* (cid 16): Hot Deals (16/16) • Atta (16/1165) • Rice (16/1161) • Toor, Urad & Chana (16/1010) • Moong & Masoor (named in navigation, no page returned) • Pulses & Millets (16/800 slug) • Edible Oils & Ghee (16/917) • Salt & Sugar (16/933). — [Blinkit Atta, Rice & Dal Hot Deals](https://blinkit.com/cn/atta-rice-dal/hot-deals/cid/16/16); [Blinkit Rice](https://blinkit.com/cn/atta-rice-dal/rice/cid/16/1161); [Blinkit Edible oils](https://blinkit.com/cn/grocery-staples/edible-oils-ghee/cid/16/917); [Blinkit Salt & Sugar](https://blinkit.com/cn/grocery-staples/salt-sugar/cid/16/933); [Blinkit pulses-millets](https://blinkit.com/cn/pulses-millets/cid/16/800)
- *Dry Fruits, Masala & Oil / Masala, Oil & More* (cid 1557): Dry Fruits (1557/1160) covers makhana, cashew, almond, raisin and gift packs. The label varies: "Dry Fruits, Masala & Oil" vs "Masala, Oil & More". Clone sites also list "Snacks & Munchies". — [Blinkit Dry Fruits](https://blinkit.com/cn/dry-fruits/dry-fruits/cid/1557/1160); [Blinkit clone](https://puneeth-gaikwad-l.github.io/blinkit-clone/)

**Zepto (from zepto.com category pages):**
- *Atta, Rice, Oil & Dals*: Oil • Atta • Besan, Sooji & Maida • Healthy Dal • Rice & More (plus others). — [Zepto Atta, Rice, Oil & Dals](https://www.zepto.com/category/atta-rice-oil-dals)
- *Masala, Dry Fruits & More* (8 sub-categories): Top Picks • Powders & Pastes (turmeric, red chilli, garam masala, biryani masala, ginger-garlic paste) • Dry Fruits & Nuts (almonds, cashews, walnuts, pistachios, raisins, figs) • Dates & Seeds • (others not shown). — [Zepto Masala, Dry Fruits & More](https://www.zepto.com/category/masala-dry-fruits-more)
- *Tea, Coffee & More* (11 sub-categories): Top Picks • Tea • Coffee (instant, filter, decoction) • Green & Herbal Tea • Cold Coffee & Iced Tea • Premium Coffee • Premium Tea • Zepto Cafe • Drink Mixes • Kids' Nutrition • (one more). — [Zepto Tea, Coffee & More](https://www.zepto.com/category/tea-coffee-more)
- *Packaged Food, Noodles & Ready Meals* (10 sub-categories): includes Noodles & Vermicelli, ready-to-eat meals, baking essentials and baby food. — [Zepto Packaged Food](https://www.zepto.com/category/packaged-food)

**BigBasket:**
- *Foodgrains, Oil & Masala*: Atta, Flours & Sooji • Dals & Pulses • Dry Fruits • Edible Oils & Ghee • Masalas & Spices • Organic Staples • Rice & Rice Products • Salt, Sugar & Jaggery • Gourmet & World Food. — [BigBasket Foodgrains, Oil & Masala](https://www.bigbasket.com/cl/foodgrains-oil-masala/?nc=nb)
- BigBasket also uses the label "Noodle, Pasta, Vermicelli". — [BigBasket category](https://www.bigbasket.com/cl/foodgrains-oil-masala/?nc=cs)

**Swiggy Instamart:**
- *Masalas and Dry Fruits*: Masalas and Pastes • Papad and Fryums • (others). A separate *Noodles, Pasta, Vermicelli* category had about 1,457 products as of May 2026. Masalas had about 2,275 products; the top brand was Supreme Harvest with 88. **[third-party tracker: MetricsCart]** — [Instamart Papad & Fryums](https://www.swiggy.com/instamart/city/bangalore/sc/masalas-and-dry-fruits/papad-and-fryums-6821a0a01f249300018d772f); [MetricsCart Instamart noodles](https://metricscart.com/quick-commerce-analytics/reports/swiggy-instamart/noodles-pasta-vermicelli-on-swiggy-instamart/); [MetricsCart Instamart masalas](https://metricscart.com/quick-commerce-analytics/reports/swiggy-instamart/masalas-on-swiggy-instamart/)

### Inferences
- A common kirana tree synthesised from the above (labels are the most common forms):
  - **Atta, Rice & Dal**: Atta • Rice • Toor/Urad/Chana dal • Moong/Masoor dal • Besan, Sooji & Maida • Millets
  - **Oil & Ghee**: split out by BigBasket and Blinkit, merged by Zepto. Edible oils • Ghee & Vanaspati
  - **Masala & Dry Fruits**: Whole spices • Powders & Pastes • Ready masala (biryani, garam) • Dry fruits & nuts • Dates & seeds • Papad & fryums
  - **Salt, Sugar & Jaggery**
  - **Tea, Coffee & Drinks**: Tea • Green/Herbal tea • Coffee • Drink mixes / health drinks • Cold drinks & juices
  - **Instant & Packaged Food**: Noodles & vermicelli • Pasta • Ready-to-eat • Soups • Breakfast cereals • Sauces & spreads
  - **Snacks & Munchies**: Chips & namkeen • Biscuits & cookies • Chocolates & sweets
- Every app inserts a merchandising node ("Top Picks", "Hot Deals") at position 1 of each sub-category rail. A POS picker can use the same slot for "Fast movers" or "Recent". **[inference]**
- Labels pair 2–3 related nouns with "&" ("Toor, Urad & Chana"). This keeps the tree shallow and still gives users scent. **[inference]**

### Gaps
- The full Blinkit sub-category list for snacks, tea and instant food wasn't retrieved. Zepto's lists were cut off in snippets.
- No category trees for JioMart, Flipkart Minutes, Amazon Fresh or DMart Ready staples were found below the top level.

## What happens after tapping a sub-category (listing with rail, sticky chips, add buttons)?

### Takeaway
Tapping a category tile opens a **product listing with a sticky left sub-category rail and a 2-column product grid**. Each card has a product image, weight or pack size, price with MRP strikethrough and discount tag, and an **"ADD" button that turns into a − qty + stepper**. Product details open as a bottom-sheet or pop-up, so the user returns to the grid without losing their place.

### Cited Findings
- Blinkit-style listing: a narrow, sticky left sub-category rail with an active highlight, truncated labels and a 2-column product grid on mobile. **[third-party clone brief]** — [v0 Blinkit-style project](https://v0.app/deliverylads23-4818s-projects/chat/duplicate-of-new-version-15-aug-TuPMmBwbQCw)
- On Blinkit product cards, the ADD button has a green border and text. A product-listing case study notes several variations of the Add button, plus offer and urgency tags. **[third-party]** — [GitHub Blinkit clone](https://github.com/Mehul721/-BlinkitAssignment); [Recreating Blinkit PLP, Medium](https://medium.com/@srirammanogar07/understanding-ui-design-by-recreating-the-product-listing-page-of-blinkit-a-case-study-66fff1e951b)
- On Zepto, product details open in a pop-up, so users can return easily to the product grid. A reviewer said this supports exploratory browsing. **[third-party]** — [Arsha Treesa Zepto UX review](https://arshatreesa.framer.website/blog/zepto-ux-analysis-a-comprehensive-review)
- Blinkit animation references are catalogued at 60fps.design (ADD and stepper micro-interactions). **[third-party gallery; content not read]** — [60fps Blinkit](https://60fps.design/apps/blinkit)
- Blinkit, Zepto and Instamart search and sponsored listings mix ads into results. **[third-party, dev.to]** — [dev.to on q-commerce search ads](https://dev.to/anita_kapal_48632cd18fdb2/how-blinkit-zepto-and-instamart-turn-product-search-into-ad-revenue-52p)

### Inferences
- **[unverified, general familiarity]** Many listings also show a sticky top row of filter or sort chips (Sort, Brand, Price, Type). Tapping a rail item swaps the grid and does not navigate, so back takes the user to the categories screen in one step.
- For a POS, the transferable parts are a single-tap add from the tile, an in-place quantity stepper, a persistent cart total (Blinkit and Zepto show a sticky "View cart" bar), and a rail that changes content without a new screen. **[inference]**

### Gaps
- I found no official source on chip filters or rail widths for any app.

## Published UX research (Baymard, NN/g) on category navigation, left-rail vs tabs, number of categories, image tiles, common mistakes

### Takeaway
Baymard recommends three things: subdivide once a level reaches about 10 options, put sub-category navigation (with thumbnails) at the top of intermediary category pages, and make product categories the first level of mobile navigation. NN/g's India study found that simple icons helped low-English users find categories. No NN/g or Baymard source found here directly compares a left rail with top tabs.

### Cited Findings
- Users begin to feel overwhelmed when presented with more than about 10 sub-category options. On mobile this is harder still, because users must scroll. Baymard recommends subdividing at about 10 categories and aiming for at least 10 products at the deepest level. 60% of sites don't divide categories into manageable chunks (2025 article). — [Baymard: Homepage & Category Navigation best practice](https://baymard.com/blog/ecommerce-navigation-best-practice)
- 22% of sites don't make product categories the first level of the main navigation on mobile. Baymard's guideline is that categories should be the top-level items of mobile navigation, so users can start browsing at once. — [Baymard guideline 2257](https://baymard.com/guidelines/2257-main-categories-in-mobile-navigation); [Baymard main navigation article](https://baymard.com/blog/main-navigation-product-categories)
- Baymard's 2026 benchmark rates the homepage and category navigation of most leading sites "mediocre" to "poor" (61% desktop, 65% mobile). — [Baymard main navigation article](https://baymard.com/research-articles/main-navigation-product-categories)
- Intermediary category pages work best when sub-category navigation is front and centre at the top. Promotional assets should come after the sub-category links. These pages are even more useful on mobile, where broad scopes overwhelm users. — [Baymard: Ecommerce sub-category pages](https://baymard.com/research-articles/ecommerce-sub-category-pages); [Baymard: Ecommerce category page](https://baymard.com/blog/ecommerce-category-page)
- Baymard has a dedicated guideline, "Subcategory Thumbnails on Intermediary Category Pages" (#309). Its body text wasn't retrieved. — [Baymard guideline 309](https://baymard.com/guidelines/309-subcategory-thumbnails-on-intermediary-category-pages)
- Mobile gives minimal information scent, because there's no hover and less room for text descriptions. Desktop designs can't simply be scaled down. — [Baymard mobile category page benchmark](https://baymard.com/mcommerce-usability/benchmark/mobile-page-types/category-page)
- Other Baymard items, titles only: "Sub-Sub-Category Links (52% get it wrong)", "Avoid Inline Scroll Areas (26% get it wrong)", "Sibling categories". — [Baymard sub-sub-categories](https://baymard.com/research-articles/sub-sub-categories); [Baymard sibling categories](https://baymard.com/research-articles/sibling-categories)
- Baymard's research on horizontal tabs (product-page content, not app navigation) found that on mobile some tabs are likely hidden by default because of viewport width. This argues against long horizontal tab strips for many categories. — [Baymard avoid horizontal tabs](https://baymard.com/research-articles/avoid-horizontal-tabs)
- NN/g's mobile usability report covers tabs, navigation bars, hamburger menus and navigation hubs. Findings weren't available in the snippet. — [NN/g mobile navigation patterns](https://www.nngroup.com/articles/mobile-navigation-patterns/); [NN/g mobile UX report](https://www.nngroup.com/reports/mobile-website-and-application-usability/)
- In a remote study with 12 Indian participants, NN/g found that simple, flat outline category icons let even low-English participants find the right category, because the icons had few distracting elements. — [NN/g: Imagery helps international shoppers](https://www.nngroup.com/articles/international-shoppers-ecommerce-sites/)
- Common mistakes seen in Indian q-commerce teardowns: item counts in category menus, too many taps to deep sub-categories, and users skipping category images and reading only titles. **[third-party]** — [Zepto heuristic evaluation](https://assets.nextleap.app/submissions/HeuristicEvaluationofZepto_UXCaseStudyofanOnlineGroceryApp_byAkshhatJaiin_Apr2025_Medium-d31f9cf7-6cf9-4938-9723-540fdb8bda05.pdf); [Arsha Treesa Zepto UX review](https://arshatreesa.framer.website/blog/zepto-ux-analysis-a-comprehensive-review)

### Inferences
- Applied to a left rail: keep each rail level to about 10 visible items. Top-tab strips hide items off-screen on narrow widths, while a vertical rail scrolls in the same direction as the content and holds 10+ items. This explains why Indian grocery apps chose rails. **[inference from Baymard thresholds plus the horizontal-tab finding; no direct rail-vs-tabs study found]**
- Image tiles help recognition, but labels do the real work (users read titles). Tiles therefore need clear, short text labels, not images alone. **[inference]**

### Gaps
- No NN/g or Baymard study directly comparing a left vertical rail with top tabs for mobile categories was found.
- Full Baymard guideline texts are behind access walls and fetching was blocked, so tile-size recommendations and the content of the "inline scroll areas" and "sub-sub-category" guidelines are unconfirmed.

## Guidance on regional language (Hindi) labels and low-literacy users

### Takeaway
Research on Indian low-literacy users consistently favours highly representational imagery and photos over text-heavy UI. Common icons may need context before they're understood. Commercial apps (Flipkart) found strong retention for a Hindi interface. Practitioner advice favours colloquial Hinglish over formal machine translation, which matters for kirana labels such as "dal", "atta" and "masala", which users already know in that form.

### Cited Findings
- Medhi et al. ran an ethnographic study of 90 low-literacy participants in India, Kenya, the Philippines and South Africa. Text interfaces were unusable for first-time low-literacy users; graphical and spoken interfaces did better. — [Medhi et al., Designing mobile interfaces for novice and low-literacy users](https://www.researchgate.net/publication/234829313_Designing_mobile_interfaces_for_novice_and_low-literacy_users)
- A Hindi and Marathi phone-interface prototype was tested with 18 participants. — [Designing new technologies for illiterate populations](https://www.researchgate.net/publication/220054735_Designing_new_technologies_for_illiterate_populations_A_study_in_mobile_phone_interface_design); [Medhi, India HCI 2010](https://www.microsoft.com/en-us/research/wp-content/uploads/2016/02/Medhi_IndiaHCI2010.pdf)
- Tuli et al., cited in a 2026 paper: common navigation icons need "actionable" contextualisation before low-literate Indian users understand them. A lock icon is often read as "access denied". — [arXiv 2601.17025](https://arxiv.org/pdf/2601.17025)
- Illiterate rural users associated ideas with highly representational icons. The authors recommend graphics with less text and vernacular options. — [arXiv 2108.09759, E-commerce for rural micro-entrepreneurs](https://arxiv.org/pdf/2108.09759)
- NN/g (12 Indian participants): simple outline icons helped low-English users find categories. — [NN/g international shoppers](https://www.nngroup.com/articles/international-shoppers-ecommerce-sites/)
- A Zepto heuristic evaluation found that users who don't know a product's English name may not recognise which English-labelled category it belongs to. **[third-party]** — [Zepto heuristic evaluation](https://assets.nextleap.app/submissions/HeuristicEvaluationofZepto_UXCaseStudyofanOnlineGroceryApp_byAkshhatJaiin_Apr2025_Medium-d31f9cf7-6cf9-4938-9723-540fdb8bda05.pdf)
- Flipkart launched a Hindi interface in 2019 for Tier II and III users. About 95% of users who opted for Hindi kept using it. — [TechCrunch on Flipkart Hindi](https://techcrunch.com/2020/06/24/walmarts-flipkart-makes-local-languages-push-to-win-small-indian-cities-and-towns); [YourStory 2019](https://yourstory.com/2019/09/flipkart-hindi-platform-bharat-india-ecommerce)
- In 2020 Flipkart added Tamil, Telugu and Kannada, translating more than 5.4 million words through its in-house localisation platform. It later had 11 Indian-language interfaces; Hindi, Tamil, Telugu and Bengali were the most used. — [Business Today](https://businesstoday.in/technology/news/flipkart-enables-vernacular-language-interface-in-tamil-telugu-kannada/story/407953.html); [Business Standard](https://www.business-standard.com/article/companies/vernacular-language-users-share-on-flipkart-rises-18-this-year-121121601072_1.html)
- Flipkart's grocery arm (Supermart) added a voice assistant handling English, Hindi and mixed-language commands. — [AOL / TechCrunch](https://www.aol.com/walmart-owned-flipkart-introduces-multilingual-083939406.html)
- Amazon India added Hindi in 2018 and, as of 2020 coverage, no other Indian languages. — [TechCrunch](https://techcrunch.com/2020/06/24/walmarts-flipkart-makes-local-languages-push-to-win-small-indian-cities-and-towns)
- Practitioner advice: machine-translated Hindi sounds formal and unnatural; use conversational Hinglish, and add a microphone for regional voice search. **[opinion blog, not tested research]** — [Induji Technologies: Bharat-first apps](https://www.indujitechnologies.com/blog/building-bharat-first-apps-low-bandwidth)

### Inferences
- For a kirana POS picker: use real product photos on tiles (highly representational), keep a short label, and optionally show a secondary Hindi or Hinglish label, for example "Dal & Pulses / दाल". Many staples are known by their Hindi names even in English UIs ("atta", "besan", "sooji", "masala"), which the apps themselves use. **[inference]**

### Gaps
- No source found on how Blinkit, Zepto, Instamart or JioMart localise category names, or whether they offer in-app language switching (2025–2026).
- The academic low-literacy studies are 10–18 years old and based on feature phones, with small samples.
