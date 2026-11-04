# Product image grid for mobile (Flipkart-style cards)

Note on evidence: public, citable pixel specs for Flipkart/Blinkit/Zepto/JioMart/BigBasket/Instamart/Amazon cards were NOT found (no official design docs or measured teardowns surfaced). Baymard's detailed product-list findings are behind a paywall, and NN/g and Material 3 pages were not retrievable. Items marked "Inference" are pattern descriptions from general familiarity with these apps, not verified measurements. Treat them as starting values to check against screenshots.

## How Indian e-commerce / quick-commerce apps lay out product cards (mobile vs desktop)

### Takeaway
No public specs exist. Visible patterns: 2 columns (quick-commerce category pages often 3 small tiles) on phones, 4-6 on desktop. Cards are square image first, then price, MRP and discount, then a full-width or corner ADD button that turns into a stepper.

### Cited Findings
- Blinkit card data includes selling price, MRP, a discount % that is null (not shown) when price equals MRP, and an optional offer label ("discountText") on the card — [Apify Blinkit scraper](https://apify.com/solidcode/blinkit-scraper); [Apify Blinkit search scraper](https://apify.com/architjn/blinkit-search-scraper)
- A Zepto portfolio case study describes product tiles with thumbnails, discount badges and large "+" buttons on an 8pt spacing grid — [Aadhavan M, visual design](https://aadhavanm.pages.dev/skills/visual-design)
- Generic 10-minute-grocery UI template pattern: image, price, quantity stepper and a clear "add" button. It also recommends a virtualized list for long catalogues — [VP0 blog](https://vp0.com/blogs/in-blinkit-zepto-10-minute-grocery-delivery-ui-template)
- Shoppers scan rather than read, so a consistent card layout speeds up comparison — [Huemor glossary](https://huemor.rocks/glossary/product-card/)

### Inferences (unverified pattern notes)
- Quick-commerce (Blinkit, Zepto, Instamart): a white card with a light 1px border and 8-12px radius. The image box is square with the product on a light-grey or white background. A small timer/"x mins" chip and a discount ribbon sit at the top-left of the image. Below that: a 2-line clamped name (about 12-13px, semibold), then pack size ("500 g", "1 × 500 g") in about 11-12px grey. The bottom row has the price (about 13-14px bold) with the struck MRP under or beside it in grey, and an outlined green "ADD" button on the right (about 28-32px tall visually) that becomes a −/qty/+ stepper.
- Flipkart / Amazon India search grids: 2 columns on phones, often with thin dividers instead of card borders. Details: square or portrait image, 2-line name, a rating chip (green with a star on Flipkart), price + struck MRP + green "% off", then badges such as "Assured" or "Bestseller". Most have no ADD button in the grid, so tapping opens the product page.
- BigBasket / JioMart: a similar 2-column mobile layout with a pack-size dropdown on the card. Desktop shows 4-5 columns.
- Desktop: about 180-240px wide cards, 4-6 columns.

### Gaps
- No measured dp/px values, font sizes or column counts from any brand's official source. Measure from your own screenshots at 360px width.

## Responsive grid rules and touch targets

### Takeaway
Use 2 columns at 360-412px (3 only for compact tiles of at least 110px with no long text), and auto-fill minmax for wider screens. The add button's hit area should be 48×48 CSS px, even if it looks smaller.

### Cited Findings
- Minimum tap target is about 48×48 dp (about 9mm). Smaller visuals can be padded up to it, and targets should be spaced about 8px apart — [web.dev accessible tap targets](https://web.dev/articles/accessible-tap-targets)
- In grids, images take most of the space and text is shortened to avoid wrapping. Users often see only 4-6 grid items at a time on a phone — [UX Movement](https://uxmovement.com/?p=8647); [UX Planet](https://uxplanet.org/mobile-ux-design-list-view-and-grid-view-8f129b56fd5b)
- Grids suit image-led browsing, lists suit detail comparison — [Envato forum](https://forums.envato.com/t/grid-view-vs-list-view-what-to-use-on-your-product-pages/46997)
- 2-column mobile product grids are a common merchant need on Shopify/Webflow themes, and some platforms offer a 1/2 column toggle — [Shopify community](https://community.shopify.com/t/how-to-have-2-columns-for-product-list-on-mobile/93924); [Shopware switcher](https://store.shopware.com/en/great98140330654m/mobile-grid-switcher-1-or-2-columns-for-product-listings.html)

### Inferences (suggested spec)
- At 360px with 12px gutters and 8px gap, each column is (360−24−8)/2 = 164px. 3 columns give about 104px, which is too narrow for a 2-line name plus price row plus ADD at readable sizes.
- Tailwind: `grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6`, or `grid-template-columns: repeat(auto-fill, minmax(150px, 1fr))` on mobile and `minmax(180px,1fr)` at md+.
- Card: `rounded-lg border p-2` on mobile, `p-3` on desktop. Image `aspect-square w-full object-contain bg-gray-50`. Name `text-[13px] leading-tight line-clamp-2 min-h-[2lh]` (reserve 2 lines so rows align). Pack size `text-xs text-gray-500`. Price `text-sm font-bold`. MRP `text-xs line-through text-gray-500`. Off `text-xs font-semibold text-green-700`.
- ADD button: visible height 32px, full width at the bottom or right-aligned. Extend the hit area with padding or a pseudo-element so it reaches 48px. Keep it at least 8px from other targets.
- Use `loading="lazy"`, explicit width/height, and small WebP thumbnails (about 2× of 164px, so roughly 320px) for cheap Androids.

### Gaps
- No Baymard or NN/g data on optimal column count retrieved (Baymard behind paywall: [Baymard mobile product list](https://baymard.com/mcommerce-usability/benchmark/mobile-page-types/product-list)).

## Items without a photo

### Takeaway
No sourced guidance found. Use a neutral fixed-ratio placeholder so the grid never collapses.

### Cited Findings
- None found from the target apps or UX research.

### Inferences
- Keep the same `aspect-square` box with a soft background tinted by category. Inside, show a category icon or the item's first letter or two (Hindi first letter works too) in large type. Never leave the box empty or show a broken image. Use an `onError` fallback to the same placeholder.

### Gaps
- How each brand handles missing images was not documented publicly.

## What UX research says on density, image size and readability

### Takeaway
Baymard says list items must carry enough info to avoid needless product-page visits, and that limited mobile screens make comparison hard. Detailed numeric guidance is paywalled.

### Cited Findings
- Product lists need enough product information to avoid needless visits to product pages, which costs more on mobile. Limited screen size makes comparing items challenging — [Baymard mobile product list](https://baymard.com/mcommerce-usability/benchmark/mobile-page-types/product-list)
- 82% of sites give sufficiently large mobile images. 52% fail to scale product images in landscape. Sizing images relative to viewport width fixes this — [Baymard landscape images](https://baymard.com/blog/scale-mobile-product-images-in-landscape)
- Only about 16% of mobile sites surveyed offered a list/grid toggle — [Retail Dive](https://www.retaildive.com/news/what-retailers-selling-on-mobile-can-learn-from-tinder/415003/)

### Inferences
- For a kirana store, pack size and price matter more than ratings. Always show pack size on the card.

### Gaps
- NN/g and Material 3 card spec pages could not be fetched (M3 page rendered no content).

## Accessibility (alt text, discount green contrast, Hindi clamp)

### Takeaway
Use the legacy `-webkit-line-clamp` (or Tailwind `line-clamp-2`) with overflow hidden. Use a darker green such as green-700 for "% off" text. Alt text should be the item name plus pack size.

### Cited Findings
- `line-clamp` has limited availability. The reliable form is `display:-webkit-box; -webkit-box-orient:vertical; -webkit-line-clamp:2; overflow:hidden`, which is deprecated but will keep being supported. `overflow:hidden` is required to clip — [MDN line-clamp](https://developer.mozilla.org/en-US/docs/Web/CSS/line-clamp)

### Inferences
- WCAG AA needs 4.5:1 for small text. Bright greens like #22c55e on white fail, so use about #15803d (Tailwind green-700) or darker for "% off" text, or white text on a dark green badge. (Contrast values from general WCAG knowledge; verify with a checker.)
- Devanagari has taller ascenders/descenders and matras. Use `leading-snug` (about 1.35) rather than `leading-tight`, set `lang="hi"` on Hindi names, and set min-height in `lh` units so clamped matras aren't cut off.
- Announce the struck MRP for screen readers (e.g. visually hidden "MRP" before it and "Price" before the selling price). The ADD button needs an aria-label like "Add Toor Dal 1 kg".

### Gaps
- No India-specific accessibility study of grocery cards found.
