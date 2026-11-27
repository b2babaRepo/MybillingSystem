# Search and Filter Design for Picking Grocery/Kirana Items in Mobile Ordering Apps

Method note: research was done on 2026-11-27. Page fetches to most hosts were blocked in this session (baymard.com, medium.com, analyticsindiamag.com, aclanthology.org, stories.flipkart.com and productgrowth.in all failed to resolve), so nearly every finding below comes from search-engine summaries of the cited pages, not full reads. Baymard's detailed mobile guidelines are paywalled anyway. Claims from secondary teardowns, blogs or press coverage are marked **[secondary]**.

## 1. How do Blinkit, Zepto, Swiggy Instamart, BigBasket, JioMart and Amazon Fresh India do search?

### Takeaway
Indian grocery search is built around messy, phonetic, code-mixed input. Zepto (LLM spellchecker), Flipkart (Hinglish-to-English query translation, contextual spell correction) and Swiggy (neural search, Sarvam voice in Indian languages) all have documented systems for it. Search is also the main way shoppers find items: one study puts it at 73% of online grocery shoppers. Public, verifiable detail on UI specifics was not found: image autosuggest, recent or trending lists, and global versus in-category scope.

### Cited Findings
- **Search is the primary finding tool.** A Blinkit search-flow case study cites an Adobe study: 73% of online grocery shoppers use search as their primary way to find products on e-grocery apps. **[secondary]** — [UXM / Medium, Blinkit search case study, 2023](https://medium.com/uxm-community/aim-to-improve-the-search-flow-of-blinkit-app-da8e8338a243)
- **Blinkit search bar visibility.** The same 2023 case study reported that users could not see the search bar clearly, and it evaluated the flow from home, to name search, to results, to product variants. **[secondary, student-style study]** — [UXM / Medium](https://medium.com/uxm-community/aim-to-improve-the-search-flow-of-blinkit-app-da8e8338a243)
- **Blinkit never returns an empty page.** A viral June 2026 post showed Blinkit returning chocolates for random gibberish. Blinkit made no official statement. Commentators guessed it falls back to letter-based matching, autocomplete or popular products when there is no exact match. **[secondary, speculative]** — [Digit](https://www.digit.in/news/general/type-toddler-like-gibberish-on-blinkit-and-get-chocolates-viral-post-sparks-concerns-over-algorithm.html); [Analytics Insight](https://www.analyticsinsight.net/news/blinkit-shows-gems-perk-munch-for-random-text-is-it-a-glitch-or-smart-algorithm)
- **Zepto's spellchecker.** It is an LLM spellchecker for vernacular and phonetic queries such as "balekayi cheeps" (raw-banana chips) and "kottimbeer pudina" (coriander and mint). It uses retrieval-augmented generation: the top-k matching product and brand names are passed to the LLM along with the noisy query. Reported model: Llama3-8B, instruct fine-tuned, hosted on Databricks. Reported gain: +7.5% conversion on affected queries. **[secondary; company-reported figure]** — [Analytics India Magazine](https://analyticsindiamag.com/ai-features/zeptos-spellchecker-fixes-errors-with-a-little-help-from-llms/); [Analytics Vidhya, Jul 2025](https://www.analyticsvidhya.com/blog/2025/07/replicate-zeptos-multilingual-query-resolution-system/)
- **Zepto learns from reformulations.** When a user quickly rewrites a query (for example "banan chips" to "banana chips"), the correction is treated as a self-learning training signal. Phonetic spellings such as "kothimbir" (coriander) break traditional search. **[secondary]** — [Analytics India Magazine](https://analyticsindiamag.com/ai-features/zeptos-spellchecker-fixes-errors-with-a-little-help-from-llms/)
- **Flipkart's Hinglish query translation.** Flipkart defines Hinglish queries as Hindi words written in Latin script. It trains encoder-decoder models to translate them into English so the standard English search stack can handle them, and reports the model is live on the app and website, serving millions of queries. Only the non-brand words are translated, so a query like "veer brand oil" keeps its brand intact. The training corpus was built by transliterating and translating monolingual Hindi queries, and the test set has 15k hand-tagged Hinglish queries. — [arXiv 2208.03713, Flipkart](https://arxiv.org/pdf/2208.03713v1.pdf)
- **Grocery search is heavily code-mixed.** An ACL 2023 industry paper on grocery search says a significant share of Indian search queries are code-mixed Hinglish. It also notes that brand names are often Hinglish words with ordinary English meanings, which makes naive translation risky. — [ACL 2023 Industry track](https://aclanthology.org/2023.acl-industry.10.pdf)
- **Flipkart and Microsoft India contextual spell correction.** A Transformer model trained on synthetic data. Phonetic errors are common, for example "cenityjer" for "sanitizer". — [ACL 2023 industry paper 66](https://preview.aclanthology.org/fix-sig-urls/2023.acl-industry.66.pdf)
- **Vernacular users are a growing share.** Flipkart's vernacular user share rose from 12% (Q4 2020) to 18% a year later. **[secondary]** — [Scale Exchange, "How Flipkart used AI"](https://exchange.scale.com/public/blogs/how-flipkart-used-ai-to-scale-e-commerce-7-lessons-learned)
- **Flipkart voice grocery assistant.** Flipkart has a voice assistant for grocery shopping in English and Hindi. — [Flipkart Stories](https://stories.flipkart.com/flipkart-voice-assistant/); [Beebom](https://beebom.com/flipkart-voice-search-english-hindi-mobile-apps/amp/)
- **Swiggy Instamart neural search.** Swiggy announced neural search for Instamart (2023) so customers can find groceries "in a conversational manner". — [Business Standard](https://business-standard.com/companies/news/swiggy-builds-generative-ai-powered-search-for-personalized-recommendations-123072100660_1.html); [YourStory](https://yourstory.com/2023/07/swiggy-artificial-intelligence-voice-search-technology-food-grocery)
- **Swiggy and Sarvam voice ordering.** Multilingual voice-led ordering for Food, Instamart and Dineout, covering 11 Indian languages and reportedly handling Hinglish and mixed-language queries. — [Swiggy press release](https://www.swiggy.com/corporate/press-release/swiggy-partners-with-sarvam-to-bring-indias-first-multilingual-voice-led-commerce-to-food-delivery-instamart-and-dineout); [Free Press Journal](https://www.freepressjournal.in/tech/swiggy-partners-with-sarvam-ai-to-bring-voice-ordering-in-11-indian-languages-how-does-it-work)
- **BigBasket voice and regional-language search.** BigBasket added a voice assistant built with Slang Labs (about 2020). It searches and navigates to checkout but does not add items to the basket directly, and it reads aloud what it is searching for. Search reportedly works across seven regional languages, with examples like "bhindi" for "ladies finger". **[secondary]** — [Beebom](https://beebom.com/bigbasket-adds-voice-assistant-to-ease-shopping-experience); [Slang Labs / Medium](https://medium.com/slanglabs/voice-to-cart-powering-your-e-commerce-app-with-voicevoice-f4104db940ae)
- **Reference pattern from outside India (Instacart).** Search suggestions are personalised from past searches and purchases, and previously bought items carry a "Buy it again" tag in results. This is not an Indian app, but the pattern applies directly. — [Instacart docs](https://docs.instacart.com/storefront/learn_about_your_storefront/shopping/search/search_results); [Instacart ML autocomplete](https://company.instacart.com/tech-innovation/how-instacart-uses-machine-learning-driven-autocomplete-to-help-people-fill-their-carts)
- **Query understanding pulls out structured concepts.** Product type, brand and attributes are extracted from the query and can then drive automatic filters, for example "amul butter 500g" gives brand = Amul, type = butter, size = 500 g. — [Instacart Intent Engine](https://company.instacart.com/tech-innovation/building-the-intent-engine-how-instacart-is-revamping-query-understanding-with-llms)

### Inferences
- **Synonym dictionary first.** A kirana app should keep a curated synonym and alias table per item: Hindi, Hinglish and regional names mapped to English names, for example haldi to turmeric, chini to sugar, jeera to cumin, atta to wheat flour, bhindi to okra/ladies finger, kothimbir/dhania to coriander. Add phonetic or edit-distance matching on top. That gives most of the benefit of the Zepto and Flipkart ML systems at almost no cost, and it works offline. Brand names must be protected from "translation", as Flipkart's brand-preserving approach shows.
- **Log reformulations.** Logging zero-result queries and quick rewrites (Zepto's signal) is a cheap way for a shop owner or admin to grow the synonym table over time.
- **Never dead-end.** Blinkit's behaviour suggests no grocery app shows a blank "no results" page; they fall back to fuzzy or popular matches. A clearer approach is to show "Did you mean…" plus the closest items.
- **Voice is secondary.** Voice search is common (Flipkart, BigBasket, Swiggy) but is an add-on. Text search with transliteration tolerance is the core need, especially for a shopkeeper ordering stock.

### Gaps
- No verifiable, current (2025–26) source documents for any of the six apps whether autosuggest shows product images, whether there are recent or trending search lists, or whether search inside a category is scoped to that category or global. These are widely observed in the apps but I found no citable teardown. Confirm by hands-on testing.
- No primary source on JioMart or Amazon Fresh India search behaviour, including Hindi support.
- I could not find a Swiggy Bytes article on Instamart Hinglish query understanding. A Fifth Elephant 2024 talk on search relevance using small language models exists, but only the listing was retrievable: [HasGeek](https://hasgeek.com/fifthelephant/2024/sub/improving-search-relevance-in-hyperlocal-food-deli-WJdfQMDtbvxJTH8v11zUTQ).

## 2. What filters and sort options do Indian grocery apps offer, and which are quick chips versus a filter sheet?

### Takeaway
Little citable documentation exists. Indian quick-commerce listing pages are known for being light on filters and leaning on sub-category rails and brand chips, but I found no source that documents their current filter sets. BigBasket documents category and brand filters. A Dunzo case study cites missing sort and filter as a usability problem.

### Cited Findings
- **BigBasket filters.** The app lets users filter by category or brand, and "Smart Basket" automatically holds the products the user buys most often. **[secondary app-store and blog summary]** — [BigBasket App Store listing](https://apps.apple.com/in/app/id660683603); [itechguides](https://www.itechguides.com/products/bigbasket/)
- **Dunzo case study.** A redesign study (Nov 2024, which compared Zepto among others) found "no sort and filter option in any category for price, brand etc." and proposed adding sort, filters and quick brand filters. **[secondary]** — [Contra case study](https://contra.com/p/LBrgXRqs-redesigning-shopping-experience-of-dunzo)
- **Seller-side category trees.** On quick-commerce apps, the category node a product is listed under decides its attribute set and where it appears. Filterable attributes are therefore category-specific by construction. **[secondary, seller guide 2026]** — [Blooprint quick-commerce listing guide](https://blooprint.in/blog/quick-commerce-listing-guide/)
- **eBay design system.** Common, high-priority binary filters (Free shipping, Buy It Now, New) work as toggle chips, and filters or sorts that affect the whole result set should be prioritised. — [eBay Playbook, filtering patterns](https://playbook.ebay.com/design-system/patterns/filtering-patterns)
- **SAP Fiori (Android).** Recommends a single-select choice chip for sort, with a default preselected. — [SAP Fiori Android sort and filter](https://www.sap.com/design-system/fiori-design-android/v26-4/patterns/sort-and-filter/usage)

### Inferences
Grocery filter candidates, ranked by value for a kirana catalogue:
- **Quick chips (top of list, one tap):** In stock, On offer/discount, Veg-only (the green-dot mark is legally required on packaged food in India, so the data exists), Brand (the top 4–6 brands as chips), Bought before.
- **Filter sheet:** Price range, Pack size/weight (bucketed, e.g. <250 g, 250 g–1 kg, 1–5 kg, >5 kg), Brand (full list with search), and Rating, which matters less for staples.
- **Sort:** Relevance (default), Price low to high, Price per kg/litre (unit price, very useful when comparing pack sizes), Discount %, Popular.
- **Shopkeeper stock ordering:** Supplier/distributor, Low stock / below reorder level, Last ordered date, and Margin are more useful than rating or discount.

### Gaps
- No current, citable documentation of the exact filter and sort sets, or chip versus sheet split, on Blinkit, Zepto, Instamart, JioMart or Amazon Fresh. This needs hands-on app review or screenshots.

## 3. What do Baymard Institute and Nielsen Norman Group recommend for mobile filtering and sorting?

### Takeaway
The research converges on a few rules:
- Put filters in their own overlay or bottom sheet behind a sticky "Filter & Sort" trigger.
- Batch selections with an explicit "Show X results" apply button.
- Show applied filters as removable chips above the list.
- Promote the most important category-specific filters inline as chips.
- Show value counts so users avoid zero-result dead ends.

### Cited Findings
- **Sticky trigger.** The "Filter & Sort" button should stay visible while users scroll, because they often want to adjust filters mid-scroll. — [Baymard, ecommerce filter UI](https://baymard.com/learn/ecommerce-filter-ui)
- **Own layer.** Filters should occupy their own layer, a full-screen overlay or bottom sheet. Squeezing a sidebar into a mobile viewport doesn't work. — [Baymard, ecommerce filter UI](https://baymard.com/blog/ecommerce-filter-ui)
- **Deliberate apply.** Mobile filtering should use a deliberate apply action with a prominent "Show X Results" button. — [Baymard guideline 1125: apply buttons in mobile filtering](https://baymard.com/guidelines/1125-apply-buttons-in-mobile-filtering-interfaces); [Baymard filter UI](https://baymard.com/learn/ecommerce-filter-ui)
- **Applied-filters overview.** Without an overview of applied filters in the product list, mobile users had to reopen the filter interface to confirm or remove selections. Show selections as removable chips in a horizontal scroll or a stacked list above the results. — [Baymard, how to design applied filters](https://baymard.com/blog/how-to-design-applied-filters); [Baymard guideline 2572: applied filters on mobile](https://baymard.com/guidelines/2572-applied-filters-on-mobile)
- **Clear all and per-group reset.** Provide both a global "Clear All" and per-group reset links. — [Baymard filter UI](https://baymard.com/learn/ecommerce-filter-ui)
- **Promote important filters.** 61% of sites don't promote important filters. Promoted filters especially help mobile users, because most filters are otherwise hidden in the filter interface. Promoted filters must also stay in their original position in the full filter list, and they don't make up for a poor category taxonomy. — [Baymard, promoting product filters](https://baymard.com/blog/promoting-product-filters)
- **Multiple values per filter.** Users should be able to select several values within one filter (for example two brands). — [Baymard, allow applying multiple filter values](https://baymard.com/research-articles/allow-applying-of-multiple-filter-values)
- **Benchmark data exists, detail is paywalled.** Baymard benchmarks mobile filtering options across major sites. — [Baymard mcommerce benchmark, filtering options](https://baymard.com/mcommerce-usability/benchmark/mobile-page-types/filtering-options)
- **NN/g on filters vs facets.** Filters eliminate items. Facets turn search into navigation by suggesting relevant categories and attributes. Facets often show the number of items under each value, which helps users avoid zero-result combinations. — [NN/g, Filters vs. Facets](https://www.nngroup.com/articles/filters-vs-facets/)
- **NN/g on mobile facets.** Facet controls on a separate screen make it harder for users to see how they affect results. NN/g favours a tray (push-out or overlay) shown over the results, and collapsed accordions for filter values on mobile. — [NN/g, Mobile Faceted Search](https://www.nngroup.com/articles/mobile-faceted-search/); [NN/g, Filter Categories vs Values](https://www.nngroup.com/articles/filter-categories-values/)
- **NN/g on batch vs interactive filtering.** Covers when to apply filters instantly versus in a batch. — [NN/g, Applying Filters](https://www.nngroup.com/articles/applying-filters/)
- **Counts and disabled zero options.** Showing item counts and greying out zero-result options is attributed to Baymard and NN/g research by a third-party vendor. **[secondary, not verified against originals]** — [Tweakwise best practices](https://docs.tweakwise.com/docs/best-practises-filtering)
- **Contentsquare agrees.** Make the filter menu sticky and show applied filters at the top of results. **[vendor, secondary]** — [Contentsquare mobile filter UX](https://contentsquare.com/blog/mobile-filter-ux/)

### Inferences
- **Recommended structure for a category screen:**
  - Sticky header with the category name and an in-category search field.
  - Horizontally scrolling sub-category tabs.
  - A chip row combining promoted filters and applied-filter chips with "x" to remove.
  - A sticky "Sort / Filter" button that opens a bottom sheet with accordion groups, value counts, and "Clear all" plus "Show N items".
- **Category-specific filters.** Atta/rice should filter by weight and grain type, oil by type and pack size, dairy by fat % or brand. This follows Baymard's category-specific guidance and the seller attribute trees.
- **Zero results.** If filters lead to zero results, show which filter to remove (for example "0 items. Remove 'Brand: X'?") rather than an empty screen.

### Gaps
- Full Baymard guideline text and the percentages for mobile applied-filter compliance are paywalled and could not be read.
- No NN/g article specifically on zero-result search pages was retrieved in this session.

## 4. What are the patterns for quick re-ordering: "buy again", "frequently bought", "your usual items"?

### Takeaway
Re-ordering is central to grocery, and the documented patterns are a "Smart Basket" or "Buy it again" list of most-bought items and a "bought before" tag in search results. Hard data for Indian apps is weak; the widely quoted "70% of Zepto orders are reorders" figure is unsourced.

### Cited Findings
- **BigBasket Smart Basket.** It automatically includes the products the user shops for most often. — [BigBasket App Store listing](https://apps.apple.com/in/app/id660683603); [itechguides](https://www.itechguides.com/products/bigbasket/)
- **Instacart "Buy it again".** Previously purchased items are tagged in results, and suggestions are personalised from past searches and purchases. — [Instacart docs](https://docs.instacart.com/storefront/learn_about_your_storefront/shopping/search/search_results)
- **Unsourced reorder statistics.** A UX blog claims "quick commerce users don't shop; they reorder", that about 70% of Zepto orders are reorders, and that a one-tap reorder prompt lifts repeat frequency by 20%. **[secondary, unsourced, treat as unverified]** — [ProductGrowth, quick-commerce UX](https://productgrowth.in/insights/ecommerce/quick-commerce-ux/)
- **Blinkit post-order additions.** Since about Dec 2025, Blinkit lets users add items to an order after placing it at no extra delivery fee. This lowers the cost of forgetting a usual item. **[press]** — [Republic World](https://www.republicworld.com/business/blinkit-now-lets-you-add-items-after-payment-all-details-user-reactions); [CIOL](https://www.ciol.com/news/blinkit-enables-last-minute-order-additions-at-no-extra-fee-10872275)

### Inferences
- **Inside each category screen:** pin a "Your usual items" or "Bought before" rail at the top, ranked by purchase frequency and recency within that category. Offer a "Bought before" quick-filter chip, and show a last-bought quantity hint on item cards (for example "Last: 2 × 1 kg").
- **For a shopkeeper ordering stock:** offer "Repeat last order" from a supplier with editable quantities, and a "Below reorder level" chip. These matter more than discovery features.

### Gaps
- No primary or official source confirms the current buy-again or frequently-bought UI on Blinkit, Zepto, Instamart, JioMart or Amazon Fresh India. No verified reorder-share statistics for Indian quick commerce were found.

## 5. What performance patterns suit large catalogues on low-end Android phones?

### Takeaway
Indian consumer apps report large gains from fixing jank and image memory on low-end devices. The documented techniques are:
- network-aware image resolution
- modern compressed formats (AVIF/WebP)
- bitmap memory reduction
- recycled or virtualized lists
- lightweight PWA-style approaches for weak networks

### Cited Findings
- **Swiggy image loading.** Glide is configured to look up cached images at multiple resolutions and to request reduced-resolution images on poor networks (for example 60×60 instead of 100×100). Swiggy also uses AVIF for better quality at smaller file sizes. — [Swiggy Bytes, Media on Swiggy's mobile apps](https://bytes.swiggy.com/media-on-swiggys-mobile-apps-e97452fceb3f)
- **Swiggy jank work.** Swiggy worked with Google Android DevRel in H2 2021 to reduce jank and reports +50% user interaction. — [Android Developers story: Swiggy](https://developer.android.com/stories/apps/swiggy)
- **ShareChat.** It moved to Coil for image loading in Jetpack Compose components after Glide issues, optimised for its most-used low-end devices, and reports +60% feed scrolling. — [Android Developers story: ShareChat](https://developer.android.com/stories/apps/sharechat?hl=en)
- **Jumbotail (B2B grocery for kirana stores).** Its app slowed and froze on low-end devices, and the team cut bitmap memory footprint by about 50%. — [freeCodeCamp, "How we reduced memory footprint by 50%"](https://www.freecodecamp.org/news/how-we-reduced-memory-footprint-by-50-in-our-android-app-49efa5c93ad8)
- **Flipkart Lite.** A PWA built for weak networks and low-end phones. **[secondary summary]** — [eChai summary](https://echai.ventures/startingup/r/building-flipkart-lite-a-progressive-web-app--r-building-flipkart-lite-a-progressive-web)
- **Virtualization in general.** Rendering only visible rows is the standard mobile approach for long lists. — [Educative, virtualization in mobile](https://www.educative.io/courses/mobile-system-design/lta/virtualization-for-performance-optimization-in-mobile); [Instamobile, React Native on older Android](https://instamobile.io/blog/improve-react-native-performance-older-android/)

### Inferences
- **Lists:** use virtualized lists (RecyclerView / LazyColumn, or FlashList/FlatList in React Native, or a windowed list on web) with fixed-height item cards.
- **Images:** serve thumbnails sized to the card (for example 96–160 px) in WebP or AVIF, lazy-load them with placeholders, and cap the memory cache.
- **Search:** keep a local offline index of item name, aliases, brand, category and barcode (for example SQLite FTS5 or an in-memory trigram or prefix index) so search works without network and answers in under 100 ms. Sync deltas in the background.
- **Typing:** debounce keystrokes (about 150–250 ms) and show local results first, then merge server results.

### Gaps
- No official Zepto, Blinkit or Flipkart engineering post on list virtualization or offline search was found. The Zepto React Native teardown on Hashnode is from an individual, not Zepto. **[secondary]** — [Hashnode](https://hashnode.com/posts/reverse-engineering-a-world-class-app-5-lessons-from-zepto-for-react-native-developers/68a754cd2b686d3ee36c5879)
- No measured benchmarks for offline catalogue search on low-end devices in Indian apps were found.
