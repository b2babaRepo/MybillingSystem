# Mobile UX patterns for picking many items from a long master list (Grocery Khata)

Note on evidence: these notes come from 10 web searches. Searches returned no primary Baymard pages, no M3 spec pages and no NN/g articles beyond a video and topic index. Many findings are therefore secondary (practitioner blogs and design systems) and are labelled as such. Treat recommendations as expert consensus, not controlled research.

## Multi-select in long lists (row tap vs checkbox vs "+ Add" pill vs tile grid; selected styling; counter tray; undo)

### Takeaway
Keep multi-select, but make the whole row the tap target and show a strong selected state. Add a live selection bar with a count and an undo option. Checkboxes stay right for batch selection. Toggles are wrong here, and long plain checkbox lists feel daunting, so pair them with search and filtering.

### Cited Findings
- Checkboxes fit batch selection that is submitted together and "select all" with a master control. Toggles are for immediate, standalone settings. — [DMC Info](https://www.dmcinfo.com/blog/22434/user-interface-design-tips-checkboxes-vs-toggle-switches/); [Cieden](https://cieden.com/book/atoms/checkbox/where-and-when-is-checkbox-design-appropriate)
- A long list of checkboxes "looks intimidating" and can cause abandonment. — [UX Planet (Babich)](https://uxplanet.org/checkbox-and-toggle-in-forms-f0de6086ac41)
- For long, scrolling option sets, Visa's design system recommends a multiselect with text entry so users can filter. — [Visa Design System](https://design.visa.com/components/checkbox/usage/)
- Larger touch targets help on touch screens. A third-party guide cites a 48x48dp minimum touch target and 8dp spacing. — [UXcel mobile selection controls](https://uxcel.com/lessons/mobile-selection-controls-191); [Material Components Android chip docs](https://github.com/material-components/material-components-Android/blob/master/docs/components/Chip.md)
- NN/g's bulk-actions guidance recommends three things: a Select All option, a contextual action bar, and clear feedback with undo. — [NN/g video: Bulk Actions, 3 Design Guidelines](https://www.nngroup.com/videos/bulk-actions-design-guidelines/)
- Practitioner consensus favours undo over confirmation dialogs for reversible actions, and batching a bulk action into a single undo toast. No controlled studies compare the two. — [137foundry / DEV](https://dev.to/137foundry/how-to-choose-between-confirmation-dialogs-undo-windows-and-soft-delete-patterns-1opj); [AppMaster bulk actions](https://appmaster.io/blog/bulk-actions-done-safely-preview-permissions-undo-patterns)
- Quick-commerce product cards usually show an image, price, a clear "add" button and a quantity stepper. The cart updates in place, with no page change. (Descriptive source, not research.) — [vp0 template blog](https://vp0.com/blogs/in-blinkit-zepto-10-minute-grocery-delivery-ui-template)
- A designer's Zepto quick-add redesign used a primary-coloured add button, a bold name and a subdued variant/price line, aiming at faster adding. (Portfolio, not measured.) — [Aadhavan M portfolio](https://aadhavanm.pages.dev/skills/visual-design)
- Snackbars and modals should not cover key CTAs. — [Zepto/Blinkit teardown (NextLeap)](https://assets.nextleap.app/submissions/Blinkit-vs-Zepto-Product-TearDown-82bf9da7-9b75-4dd8-aa3c-e4f345b81b62.pdf)

### Inferences
- Recommended row: the whole row toggles selection. Show a trailing "+ Add" pill that turns into a filled "✓ Added" chip, and tint the selected row (light brand tint plus a left accent bar). This matches the quick-commerce "add" idiom shopkeepers already know from Blinkit and Zepto, and gives a bigger target than a 20dp checkbox.
- Use a list for speed at 1,000 rows. A photo tile grid shows only about 6 items per screen against about 10 to 12 list rows. Photos can go in a small 40dp thumbnail. A grid view could be optional for categories that have photos.
- Sticky bottom tray: "12 chosen · Review ▸ | Add 12". Tapping Review opens a sheet listing the chosen items, each removable. Each bulk action (category "add all", a bundle) gets one undo snackbar placed above the tray, so it does not cover the CTA.

### Gaps
- No measured A/B data comparing row-tap and checkbox selection speed for this use case.
- Baymard's own pages were not retrieved.

## Category navigation for 20 to 30 categories

### Takeaway
A single horizontal chip row hides most of 20 to 30 categories. Evidence favours category landing pages or section menus for mobile subnavigation, plus sticky section context. A good fit: a compact icon grid or "All categories" sheet for jumping, a chip row that syncs with scroll, and sticky section headers.

### Cited Findings
- NN/g's topic index lists accordions, sequential menus, section menus and category landing pages as the popular mobile subnavigation options. It calls dropdowns overused but useful for revealing a list of options. — [NN/g Navigation topic](https://www.nngroup.com/topic/navigation/?page=3)
- Only a few tabs fit horizontally, and content hidden in tabs is easy to overlook. — [Digital Doughnut](https://www.digitaldoughnut.com/articles/2021/august-2021/top-6-basic-patterns-for-mobile-navigation)
- Sticky elements should not take too much screen space on mobile. — [Framer](https://www.framer.com/dictionary/sticky-navigation)
- Tab headings hidden behind sticky headers were reported as a usability problem. — [Brizy community](https://support.brizy.io/hc/en-us/community/posts/13157679353105-Mobile-User-Experience-of-Tabs-Accordions)
- Quick-commerce apps use a category grid so users see everything at a glance with tap-friendly targets. (Descriptive.) — [vp0](https://vp0.com/blogs/in-blinkit-zepto-10-minute-grocery-delivery-ui-template)
- Filter chips are meant for refining content and can be multi-select. Single-select uses choice chips. — [Flutter FilterChip](https://api.flutter.dev/flutter/material/FilterChip-class.html); [Compose FilterChip](https://kotlinlang.org/api/compose-multiplatform/material3/androidx.compose.material3/-filter-chip.html)

### Inferences
- Keep the chip row for the 6 to 8 most common categories, and end it with an "All ▾" chip that opens a bottom-sheet grid of all categories. Each grid tile shows an icon (own artwork), a Hindi and English name, and a "3/45 added" count.
- Use a sectioned list with sticky headers, for example "आटा व दाल · Atta & Dal (45) · Add all". Sync the active chip with scroll position.
- Show bilingual labels on chips (Hindi primary, small English) for mixed-literacy users.

### Gaps
- No primary data on Blinkit, Zepto or JioMart left-rail versus grid usage. This is observational only.

## Search: instant filter, Hindi + English + transliteration

### Takeaway
Hinglish (Hindi typed in Latin script) is a large share of Indian e-commerce queries. Search should match English, Devanagari and romanised variants, filter instantly, and show recent and popular items when empty.

### Cited Findings
- About 21% of non-English queries on an Indian e-commerce platform were Hinglish (Hindi in Latin script). Literal translation of those queries gave poor results. — [arXiv 2208.03713](https://arxiv.org/pdf/2208.03713v1.pdf)
- Myntra launched vernacular search that accepts Hinglish typed queries such as "Kala Kurta". — [Business Standard](https://business-standard.com/companies/news/myntra-unveils-vernacular-search-for-users-to-shop-in-local-languages-123042500789_1.html)
- Flipkart's grocery voice assistant supported Hindi and English. Reports say BigBasket had voice search earlier. — [The Next Web](https://thenextweb.com/news/walmart-owned-flipkart-introduces-multilingual-voice-assistant-for-grocery-shopping); [Beebom](https://beebom.com/flipkart-voice-search-english-hindi-mobile-apps/amp/)
- Transliteration output varies by domain, and grocery is a distinct tuned domain in Reverie's API. — [Reverie docs](https://docs.reverieinc.com/reference/transliteration-api/supporting-domains)

### Inferences
- With 1,500 rows, search can run fully on the device. For each item, precompute a search key from the English name, the Hindi name, a romanised Hindi name and an alias list (atta/aata/aatta, besan/baisan, dal/daal). Normalise text by lowercasing, collapsing repeated vowels and folding ph/f, v/w and sh/s. Then match by prefix plus contains, debounced about 100 to 150ms.
- Empty-state suggestions: "Recent" and "Popular in kirana shops". The no-results state should offer a "Request this item" action.
- A mic button is optional. It depends on Android speech support, which was not checked.

### Gaps
- Not confirmed: does BigBasket, JioMart or Blinkit support Devanagari or transliterated text search?

## "Select all" dangers; bundles and per-category add-all

### Takeaway
NN/g endorses Select All in bulk-action contexts. But selecting 1,028 items in one tap here creates a large, hard-to-clean catalog. Scoped bulk actions with a count and undo are safer.

### Cited Findings
- NN/g recommends Select All together with feedback and undo. — [NN/g video](https://www.nngroup.com/videos/bulk-actions-design-guidelines/)
- Bulk actions deserve explicit counts, because the stakes scale with the number of items. Reversible ones should get an undo toast, not a modal. — [AppMaster](https://appmaster.io/blog/bulk-actions-done-safely-preview-permissions-undo-patterns); [137foundry](https://dev.to/137foundry/how-to-choose-between-confirmation-dialogs-undo-windows-and-soft-delete-patterns-1opj)
- More choices slow decisions (Hick's law, NN/g index summary). — [NN/g Navigation topic](https://www.nngroup.com/topic/navigation/?page=3)

### Inferences
- Replace the global "Choose all 1028 shown" with:
  - (a) Starter bundles at the top, for example "Starter kirana · 100 items" and "Daily essentials · 40". Each opens a preview sheet with everything pre-ticked, where the shopkeeper unticks what they don't stock.
  - (b) "Add all (45)" in each sticky category header.
  - (c) Keep "select all results" only when a search or filter narrows the list to 50 items or fewer, and always show the count.
- Bundle contents should come from real usage data (most-added items among shops of the same trade) once data exists. Until then, use a curated list.

### Gaps
- No public data on the optimal starter bundle size.

## Empty/disabled primary button

### Takeaway
Disabled pale buttons make users wonder why they are blocked. Hiding the CTA until it is needed, or keeping it active with a hint, is preferred. Empirical evidence is thin either way.

### Cited Findings
- "Disabled elements are rarely the right solution." — [Staffbase Spark](https://spark.staffbase.com/patterns/disabling-elements)
- Disabling a CTA makes users think about how to activate it. If you keep it, explain why it is disabled. — [UX Planet](https://uxplanet.org/disabled-buttons-ux-usability-issues-and-how-to-avoid-them-8f2246186e80); [Smashing Magazine](https://smashingmagazine.com/2021/08/frustrating-design-patterns-disabled-buttons/)
- NN/g has a video titled "Why disabled buttons hurt UX (and how to fix them)". — [NN/g](https://www.nngroup.com/videos/why-disabled-buttons-hurt-ux-and-how-to-fix-them/)
- No definitive empirical data settles the best pattern for inactive commands. — [UX Tigers](https://www.uxtigers.com/post/inactive-buttons)

### Inferences
- With 0 selected, replace the button with a slim hint bar, such as "Tap items to add them to your shop" (in Hindi as well). On the first selection, slide in the tray "1 chosen · Add 1 item". This saves vertical space and removes the dead pale button.

### Gaps
- NN/g video contents were not transcribed.

## Secondary info (pack sizes, GST) without clutter

### Takeaway
Use clear typographic hierarchy: a bold name and one subdued meta line. Move rarely needed fields (GST, HSN) out of the row, and show pack sizes as chips only when they are relevant.

### Cited Findings
- The Zepto quick-add redesign used a bold product name with a subdued variant and price line for clarity. — [Aadhavan M portfolio](https://aadhavanm.pages.dev/skills/visual-design)
- Filter and choice chips are compact selection controls. Choice chips are the single-select variant. — [Flutter FilterChip](https://api.flutter.dev/flutter/material/FilterChip-class.html)

### Inferences
- Row: a thumbnail, a bold English name with the Hindi name beside it in a lighter weight, then the meta line "500 g · ₹ MRP (if known)". Drop the category from the row (the section header already shows it) and drop GST (show it in the review sheet or item details).
- If an item has several pack sizes, selecting it expands the row inline with choice chips (250 g | 500 g | 1 kg, multi-select allowed). Keep it collapsed otherwise.

### Gaps
- No user research found on whether shopkeepers want GST visible at pick time.

## Performance on low-end Android

### Takeaway
Use a recycling virtualized list (FlashList v2), memoized rows, lightweight cached images with placeholders, and test in release builds on a low-end device.

### Cited Findings
- FlashList was built for smooth scrolling on low-end devices. Shopify reports up to 5x higher UI-thread FPS and 10x higher JS-thread FPS than FlatList on a Moto G10. (Vendor benchmark.) — [Shopify Engineering](https://shopify.engineering/instant-performance-upgrade-flatlist-flashlist)
- FlashList v2 guidance: measure in release mode; memoize `renderItem` and `getItemType` (more important in v2); use `getItemType` for separate recycle pools (for example section header vs row); wrap item-independent children in `memo`; never put `key` props inside items. — [FlashList performance docs](https://shopify.github.io/flash-list/docs/fundamentals/performance); [Writing performant components](https://shopify.github.io/flash-list/docs/fundamentals/performant-components)
- Use a faster image component and placeholders for slow images. — [Mintlify FlashList mirror](https://www.mintlify.com/shopify/flash-list/troubleshooting)

### Inferences
- Store the selection as a Set or map keyed by id, outside row props, or pass only the boolean. That way a toggle re-renders one row, not all 1,000.
- Load small thumbnails (about 96px WebP) with disk caching. Use `expo-image` or a similar component with a solid-colour placeholder, which matches the existing "soft placeholder" pattern.
- Keep sticky headers as their own item type (`stickyHeaderIndices`).

### Gaps
- No independent low-end benchmarks for FlashList v2 were found.

## Placing the non-urgent "Updates from the master list" notice

### Takeaway
People often miss or ignore banners, and frequent banners make this worse. A permanent large yellow box costs space and trains users to ignore it. Collapse it to a small dismissible row or a badge.

### Cited Findings
- NHS design system: "people often miss banners". Overuse makes it worse (it cites NN/g "Banner Blindness Revisited"). — [NHS service manual](https://service-manual.nhs.uk/design-system/components/notification-banners)
- Users rarely fixate on elements that look like ads (secondary report of NN/g eye-tracking). — [Wisepops/Medium](https://medium.com/wisepops/banner-blindness-and-how-to-design-against-it-ca4a8527135d)

### Inferences
- Replace the box with a one-line chip-row item or header icon with a badge, for example "🔔 3 updates". It opens a sheet of GST/HSN changes with "Apply all" and per-item review. Alternatively, make it a collapsible single-line banner that stays collapsed once seen. Show it expanded only when there are unseen updates.

### Gaps
- The original NN/g banner and notification articles were not retrieved.
