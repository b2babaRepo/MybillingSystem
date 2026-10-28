# Category taxonomies of Indian grocery / quick-commerce apps (for Grocery Khata)

Research note, 2026-10-28. Live app pages (blinkit.com, bigbasket.com) returned HTTP 403 to automated fetch, and zepto.com rendered empty (client-side JS). So most findings come from URL slugs, search snippets and third-party reports. Coverage is thinner than requested; see Gaps.

## What top-level and second-level categories do the major apps use?

### Takeaway
The apps converge on about 15–25 top-level "aisles" with compound names joined by "&" (e.g. "Atta, Rice, Oil & Dals", "Dairy, Bread & Eggs", "Foodgrains, Oil & Masala"). The second level is the item type (Atta, Besan/Sooji/Maida, Oil, Rice). Staples, dairy, snacks, beverages, personal care, home care and baby care appear everywhere.

### Cited Findings
- Zepto has a top-level category "Atta, Rice, Oil & Dals" (URL `/category/atta-rice-oil-dals`). Its subcategories are Olive & Cold Press Oil, Oil, Atta, Besan/Sooji/Maida, Healthy Dal, Rice & More, Healthy Rice. — [Zepto](https://www.zepto.com/category/atta-rice-oil-dals)
- Zepto has "Dairy, Bread & Eggs" (`/category/dairy-bread-eggs`), with subcategories including Milk Based Drinks, Paneer & Cream, Vegan Drinks. "Cold Drinks & Juices" and "Snacks" are separate top-level categories. — [Zepto](https://www.zepto.com/category/dairy-bread-eggs) (via search snippet)
- Blinkit category names tracked by third parties: "Dairy, Bread & Eggs" (milk, cheese, butter, bread, eggs; buns/packaged bakery also go here), "Munchies", "Cold Drinks & Juices", "Dairy & Breakfast". — [Shiprocket](https://www.shiprocket.in/blog/most-selling-products-on-blinkit/); [MetricsCart Munchies](https://metricscart.com/quick-commerce-analytics/reports/blinkit/munchies-on-blinkit/); [MetricsCart Cold Drinks](https://metricscart.com/quick-commerce-analytics/reports/blinkit/cold-drinks-juices-on-blinkit/); [MetricsCart Dairy & Breakfast](https://metricscart.com/quick-commerce-analytics/reports/blinkit/dairy-breakfast-on-blinkit/)
- Blinkit category sizes as of June 2026: Munchies had Haldiram's as the leading brand with 89 SKUs, with most items priced ₹30–59. Cold Drinks & Juices had about 1,677 products. — MetricsCart (same links)
- BigBasket's top-level slug is `foodgrains-oil-masala`, with second level `masalas-spices`. Its masala subtypes include Powdered Spices and Biryani Masala. — [BigBasket](https://www.bigbasket.com/all-offers/d/10-15/foodgrains-oil-masala/masalas-spices/)
- JioMart lists 8 grocery categories: Fruits & Vegetables, Dairy & Bakery, Staples, Snacks & Branded Foods, Beverages, Personal Care, Home Care, Baby Care. This source is from around 2020. — [Entrepreneur India](https://india.entrepreneur.com/en-in/lifestyle/jiomart-is-serving-200-cities-and-heres-how-you-can-order/351083)
- JioMart's app listing describes Staples as atta, flours, dals, edible oils, rice, salt, sugar and dry fruits, alongside biscuits, noodles, chocolates and pickles. It describes Dairy & Bakery as bread, cakes, cheese and ghee. — [App Store](https://apps.apple.com/br/app/jiomart-online-shopping-app/id1522085683?l=en-GB)
- DataWeave found private labels made up 48% of JioMart Staples and 24% of Personal Care (older data). — [DataWeave](https://dataweave.com/blog/jiomart-taking-the-e-grocery-segment-by-storm)

### Inferences
- From the URLs, there are 2 visible levels (category → subcategory). Item type is then handled by filters or brand chips, not a third tree level. BigBasket's slugs suggest it uses 3 levels (L1 Foodgrains, Oil & Masala → L2 Masalas & Spices → L3 Powdered Spices).
- The quick-commerce apps (Blinkit/Zepto) use more and narrower top-level categories, around 20+. Older e-grocers (JioMart, BigBasket) use about 8–12 broad ones.
- Note that ghee sits under Dairy at JioMart, while other apps put it with Oil. A kirana should pick one placement and allow search across both.

### Gaps
- I could not get complete current lists for Blinkit, Swiggy Instamart, Flipkart Minutes or Amazon Fresh. Their sites block scraping or render client-side.
- I found no verified count of how many tiles each home or categories screen shows.

## How are categories presented visually?

### Takeaway
I found no rigorous, screenshot-level source. Practitioner write-ups describe the same pattern: search at the top, then a scannable grid of category tiles, then product cards with price and an ADD button.

### Cited Findings
- The Blinkit/Zepto pattern is described as delivery time at the top, a category grid visible at a glance, a tap-friendly grid, and product cards with a clear price and add button. The source is a UI-kit vendor, so it has a commercial bias. — [vp0](https://vp0.com/blogs/in-blinkit-zepto-10-minute-grocery-delivery-ui-template)
- Zepto is described as clutter-free and mobile-first, optimised for instant buying. — [Medium, Design Bootcamp](https://medium.com/design-bootcamp/how-smart-design-makes-you-buy-faster-lessons-from-blinkit-zepto-rapido-minimalist-d7b9439da2bd)
- Zepto promotes new category launches (e.g. beauty) with tooltips. — [Plotline](https://www.plotline.so/inspiration/zepto-uses-tooltips-to-promote-category-launches/)

### Inferences
- Common practice, which I observed from the apps but have not sourced here: a 4-column grid of product-photo cutouts on pastel tile backgrounds with a 2-line label under each, grouped under section headers ("Grocery & Kitchen", "Snacks & Drinks", "Beauty & Personal Care", "Household Essentials"). The subcategory page has a left vertical rail of subcategory thumbnails and a product grid on the right. Treat this as unverified.

### Gaps
- I did not find sourced grid column counts, tile sizes, or any comparison of icons against photos.

## How do kirana-focused tools categorise inventory?

### Takeaway
Small-shop tools mostly use a flat, user-defined single-level "Item Category" field, not a deep tree.

### Cited Findings
- Vyapar has an optional "Item Category" setting (examples given are General, Electronics, Grocery). The category is picked when adding an item, along with Item Code and Unit. Reports include Sale/Purchase by Item Category and Stock Summary by Category. — [Vyapar guide](https://vyaparapp.in/blog/general-store-inventory-management/); [Vyapar stock guide](https://vyaparapp.in/guides/how-to-manage-stock-inventory-in-vyapar)

### Inferences
- Grocery Khata could beat these tools by shipping a preset kirana tree with images, while still letting the shop add custom categories.

### Gaps
- I found nothing on Khatabook, OkCredit, Dukaan, JioMart Partner, Bijak, ShopKirana or Jumbotail category structures.

## Public standard taxonomies (ONDC, GS1, HSN)

### Takeaway
I could not retrieve ONDC's official grocery category list in this pass. ONDC Retail v1.2 exists, and grocery is domain code RET10, which I know from training rather than a fetched source.

### Cited Findings
- A buyer-app skill references ONDC Retail v1.2 and the ONDC GitHub SDK. This is a secondary source. — [openskillindex](https://openskillindex.com/skills/ankitjh4-indic-ai-skills-ondc-buyer)
- ONDC has grown to 13 categories. — [IndBiz](https://indbiz.gov.in/ondcs-touches-50-million-transactions-in-the-past-year)

### Gaps
- I did not verify the ONDC RET10 category enum. From memory it looks like "Fruits and Vegetables", "Masala & Seasoning", "Oil & Ghee", "Foodgrains", "Dairy and Cheese", "Bakery, Cakes & Dairy", "Snacks & Branded Foods", "Beverages", "Cleaning & Household", "Pooja Items" and so on. Check this against ONDC-Official/ONDC-RET-Specifications on GitHub before relying on it.
- GS1 India DataKart categories and HSN grocery groupings were not researched. These are unverified chapters I know of: HSN 04 dairy, 07/08 vegetables/fruit, 09 spices/tea/coffee, 10 cereals, 11 flours, 15 oils/ghee, 19 bakery, 21 misc foods, 33 cosmetics, 34 soaps/detergents.

## Recommended category tree for a kirana (synthesis, not sourced)

Level 1 → Level 2. Item types can go at level 3 or be handled as tags. The Hindi names are my own suggestions, not drawn from any source.
1. Atta, Rice & Grains / आटा, चावल, अनाज: Atta, Rice/Basmati, Besan/Sooji/Maida, Poha/Daliya, Millets
2. Dal & Pulses / दाल, दलहन: Toor/Arhar, Moong, Chana, Masoor, Urad, Rajma/Chhole
3. Oil & Ghee / तेल, घी: Mustard, Refined/Sunflower, Groundnut, Ghee, Vanaspati
4. Masala & Spices / मसाले: Whole spices (खड़े मसाले), Powders (हल्दी, मिर्च, धनिया), Blended masala, Salt (नमक)
5. Sugar, Jaggery & Dry Fruits / चीनी, गुड़, मेवे
6. Dairy, Bread & Eggs / दूध, ब्रेड, अंडे: Milk, Curd, Paneer, Butter/Cheese, Bread/Pav, Eggs
7. Fruits & Vegetables / फल, सब्ज़ी: Vegetables, Fruits, Herbs (धनिया, पुदीना)
8. Snacks & Biscuits / नमकीन, बिस्कुट: Namkeen, Chips, Biscuits, Chocolates/Sweets
9. Instant & Packaged Food / इंस्टेंट खाना: Noodles, Sauces/Ketchup, Pickles (अचार), Papad, Ready mixes
10. Beverages / पेय: Tea (चाय), Coffee, Cold drinks, Juices, Health drinks
11. Personal Care / निजी देखभाल: Soap, Shampoo/Hair oil, Toothpaste, Skin care, Shaving
12. Cleaning & Detergents / सफ़ाई: Detergent, Dishwash, Floor/toilet cleaner, Brooms/mops
13. Household / घरेलू सामान: Matchbox, Candles, Batteries, Foil/bags, Mosquito repellents
14. Pooja Items / पूजा सामग्री: Agarbatti, Dhoop, Camphor (कपूर), Diya/batti, Roli/kumkum
15. Baby Care / शिशु देखभाल: Diapers, Baby food, Baby soap/oil
16. Pet / Misc (optional)

Use 15–16 tiles on the home grid, in 4 columns, each with a product-photo tile.
