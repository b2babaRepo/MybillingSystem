# Visual design for a bold, light, trustworthy shopkeeper PWA (Hindi + English, low-end Android)

Note: research was kept short (about 12 tool calls). Primary sources were used where they turned up (web.dev, W3C/WAI, Material docs, Android haptics docs). Many India app design sources are agency blogs, and those are flagged.

## Typography for Devanagari + Latin

### Takeaway
Keep Mukta. It is a 7-weight (200–800) static, OFL family from Ek Type covering Devanagari and Latin. Load only 2–3 weights, subset by unicode-range (Devanagari + Latin blocks), and use font-display: swap. Devanagari needs a looser line-height (about 1.5–1.6 for body text) or matras and conjuncts clip.

### Cited Findings
- Mukta comes in seven weights, 200 to 800. Fontsource lists [200..800]. It is static, not variable. — [Fontsource Mukta](https://fontsource.org/fonts/mukta/about); [RightFont](https://rightfontapp.com/family/mukta)
- The base Mukta family is the Devanagari one. Sibling families (Vaani, Mahee, Malar) cover Gujarati, Gurmukhi and Tamil. License is SIL OFL 1.1. — [Fontsource](https://fontsource.org/fonts/mukta/about); [GitHub Ek-Mukta](https://github.com/girish-dalvi/Ek-Mukta)
- Tests on tall scripts, Devanagari included, showed clipping at line-height 1.1. It was fixed at 1.6 or more. Noto Sans also clipped Latin descenders at 1.1. — [SuttaCentral forum](https://discourse.suttacentral.net/t/testing-for-line-height-with-tall-scripts/6196)
- Hindi matras get clipped in fixed-height UI containers no matter which font is used. Noto Sans Devanagari, Nirmala UI, Kohinoor and Mangal all clipped. The cause was fixed spacing. — [Articulate forum](https://community.articulate.com/discussions/discuss/hindi-text-clipping-in-player-ui---looking-for-a-devanagari-safe-font/1242062)
- Google Fonts design discussion gives a rule of thumb: keep glyph bounding boxes within about 125% of the em, or they clip. — [googlefonts-discuss](https://groups.google.com/d/msg/googlefonts-discuss/qIPdk9Y7YUY/Eu21xtm0YrsJ)
- Google Fonts CSS splits each family into per-script @font-face blocks with unicode-range, e.g. Devanagari U+0900-097F. The browser downloads only the blocks the page uses. Each block sets font-display: swap. — [unicodefyi chapter 6](https://unicodefyi.com/series/modern-web/fonts-and-rendering) (secondary)
- swap fits body text. optional fits decorative or heading fonts when performance matters more than consistency. — [unicodefyi guide](https://unicodefyi.com/guide/unicode-fonts-guide/) (secondary)
- The CSS size-adjust, ascent-override and descent-override descriptors on a fallback @font-face reduce layout shift during a font swap. Values must be calibrated per font pair. Set an explicit line-height first. — [web.dev size-adjust](https://web.dev/articles/css-size-adjust); [Shopify CLS font swap](https://shopify.dev/docs/storefronts/themes/best-practices/performance/reduce-cls-font-swapping)

### Inferences
- Suggested scale for a list screen at 360px width: row item name 16–17px weight 600, line-height 1.45–1.5. Secondary line (Hindi/English alias, unit) 13–14px weight 400, line-height 1.4, colour slate ~#5b6b67. Section headings 13px weight 700, letter-spacing 0.02em (Latin only, never letter-space Devanagari). Screen title 20–22px weight 700–800. Never set row heights in fixed px. Use min-height plus padding so matras are never clipped.
- Self-host Mukta 400/600/800 via @fontsource/mukta (Devanagari and Latin subsets only, woff2). Preload only the 600 Devanagari file, which is the one rows use. Fallback stack: `Mukta, "Noto Sans Devanagari", "Nirmala UI", system-ui, sans-serif`. Android ships Noto Devanagari, so the fallback looks acceptable.
- Leave out Baloo 2 (playful, heavy files) and Tiro (serif, editorial). An optional "bold display" moment could use Mukta 800. A second family is not needed.

### Gaps
- No primary source measured Mukta's own vertical metrics or exact woff2 file sizes per subset. Check with a quick build.
- Google's official Devanagari knowledge pages were not retrieved.

## Colour: bold, accessible palette around teal

### Takeaway
#0f6b5c passes AA for text on white (6.41:1) and on the #eef3f2 background (5.72:1). It works as the primary and as a text/selected colour. White cards on #eef3f2 are almost invisible (1.12:1), so card separation needs a shadow or border rather than relying on fill. Pale grey disabled buttons fail contrast and look broken.

### Cited Findings
- WCAG 1.4.11 Non-text Contrast (AA) requires 3:1 for UI component boundaries, icons and states needed to operate content. 1.4.3 requires 4.5:1 for normal text and 3:1 for large text (18pt or 24 CSS px, or 14pt bold). — [getwcag 2.2 checklist](https://getwcag.com/blog/wcag-2-2-checklist); [Stark non-text contrast](https://getstark.co/wcag-explained/perceivable/distinguishable/non-text-contrast)
- WCAG 2.2 2.5.8 Target Size Minimum (AA) requires 24×24 CSS px, with spacing exceptions. — [WAI What's new in 2.2](https://deploy-preview-343--wai-website.netlify.app/standards-guidelines/wcag/new-in-22); [DAISY KB](https://kb.daisy.org/publishing/docs/conformance/wcag/target-size-minimum.html)
- Material shows disabled states at 38% opacity of the enabled state. This is from the M2 docs. An M3 equivalent is reported secondhand. — [Material states (M2)](https://m2.material.io/go/design-states); [Proxmox patch citing M3](https://lore.proxmox.com/all/20260305112158.220557-1-s.sterz@proxmox.com/t)
- UX Movement argues against greying out disabled buttons, because users cannot tell why the button is dead. — [UX Movement](https://uxmovement.com/buttons/why-you-shouldnt-gray-out-disabled-buttons/)
- Contrast ratios computed here with the WCAG relative-luminance formula (no external source): teal/white 6.41, teal/#eef3f2 5.72, white/#eef3f2 1.12, #d1d5db/white 1.47 (a typical pale disabled grey, which fails), #64748b slate/white 4.76, amber #f59e0b/white 2.15 (fails as text), amber-700 #b45309/white 5.02, green-700 #15803d/white 5.02.

### Inferences
- Palette tokens: primary #0f6b5c, primary-strong #0a5247 (pressed), primary-tint #e3f1ee (selected row fill), with the selected row also getting a 2px left bar or checkmark in primary so it does not rely on colour alone. Accent for counts and CTAs: a warm saffron/amber. Use it as a fill (#f59e0b with dark text #3a2600), never as text on white. Success #15803d, warning text #b45309, danger #b91c1c.
- Disabled CTA: keep it full colour but explain why, e.g. "Pick at least 1 item", or keep it enabled and show a validation toast. Avoid 38% teal on white, which reads as broken.
- Card edges: use `box-shadow: 0 1px 2px rgb(15 40 35 / .06), 0 0 0 1px rgb(15 40 35 / .05)` instead of relying on white-on-#eef3f2.

### Gaps
- No primary M3 page was retrieved to confirm the 38% value or the M3 tonal palette tooling (Material Theme Builder could generate the teal tonal ramp).

## Hierarchy and density for a long list

### Takeaway
No strong external source was found. The inferences below follow general practice, constrained by WCAG target sizes.

### Cited Findings
- Target size of 24px minimum (AA) — [WAI](https://deploy-preview-343--wai-website.netlify.app/standards-guidelines/wcag/new-in-22)
- Zepto uses bottom sheets for one-handed use and progressive disclosure. — [UX Collective bootcamp case study](https://bootcamp.uxdesign.cc/case-study-improving-the-experience-of-the-zepto-app-4f09ee43e16e) (student case study, not company-authored)

### Inferences
- For hundreds of items, use a flat list inside one white card per category group rather than a card per row. Separate rows with 1px hairlines inset past the thumbnail, not full borders. Group headers should be sticky. Rows should be 56–64px min-height with the whole row tappable (well above 24px) and a 28–32px check/stepper control on the right.
- Density: 12px vertical padding, 16px gutters, 8px between groups. Too sparse (>72px rows) hurts scanning in long picking lists.

### Gaps
- No primary research found on list density for Indian low-literacy users.

## Icons and imagery without weight

### Takeaway
Use a tree-shaken SVG icon set (Lucide ~0.5–1 KB per icon, gzipped/minified estimates) at one consistent stroke weight. Use emoji only as an optional category glyph, because emoji rendering differs across Android versions. Image placeholders should be fixed-size boxes to avoid CLS.

### Cited Findings
- lucide-react ships ESM with sideEffects:false, so unused icons tree-shake. Per-icon cost is estimated at ~0.5–1 KB (third-party estimates). — [PkgPulse 2026 comparison](https://www.pkgpulse.com/guides/lucide-vs-heroicons-vs-phosphor-react-icon-libraries-2026); [allsvgicons](https://www.allsvgicons.com/compare/react-icons-vs-lucide-react/)
- Icons that convey meaning need 3:1 contrast (1.4.11). — [Stark](https://getstark.co/wcag-explained/perceivable/distinguishable/non-text-contrast)
- For semi-literate users, combining visuals and icons with text helps them confirm actions. — [BillCut blog](https://www.billcut.com/blogs/fintech-in-bharat-designing-for-digital-literacy) (agency blog, low rigour)

### Inferences
- Give image thumbnails explicit width/height (e.g. 44×44, radius 12), `loading="lazy"`, `decoding="async"`, and serve them as WebP/AVIF at 2x (88px). The placeholder should be a tinted square (primary-tint) with the item's first letter in Mukta 700. It costs zero bytes and looks intentional.
- Category illustrations: monochrome or two-tone inline SVG under 2 KB each, sprite or inline. No raster hero images on the picker.

### Gaps
- Phosphor and Tabler sizes were not measured directly. Check with bundle analyzer.

## Micro-interactions that feel premium but cheap

### Takeaway
Use transform/opacity-only CSS transitions (120–200ms), a short check scale-in, and a "bump" on the count badge. Add an optional `navigator.vibrate(10–15)` gated by feature detection, a user toggle, and possibly reduced motion. Honour `prefers-reduced-motion`.

### Cited Findings
- Feature-detect `'vibrate' in navigator`. Vibrate only on explicit user actions and offer a persisted toggle. The blog suggests gating on prefers-reduced-motion (a suggestion, not a standard). — [OpenReplay blog](https://blog.openreplay.com/haptic-feedback-for-web-apps-with-the-vibration-api/)
- The W3C Vibration spec lets user agents block vibration per user preference. — [W3C Vibration REC](https://w3.org/TR/2015/REC-vibration-20150210)
- Android haptics principles: apply the same effect consistently to similar interactions. — [Android haptics principles](https://developer.android.com/develop/ui/views/haptics/haptics-principles)

### Inferences
- Tap feedback: `:active { transform: scale(.98); background: var(--primary-tint) }` plus `-webkit-tap-highlight-color: transparent`. The check icon uses an SVG stroke-dashoffset draw in 180ms. The cart/selected counter pops with scale 1→1.15→1 in 200ms. Wrap all of it in `@media (prefers-reduced-motion: no-preference)`. Skip Lottie and animation libraries such as Framer Motion on this screen.

### Gaps
- No data found on haptics' effect on perceived quality for web apps. iOS Safari lacks navigator.vibrate (known platform fact, not verified in this session).

## Performance budgets

### Takeaway
Target the global P75 device. Alex Russell's budget is about 150 KiB HTML/CSS/fonts and 300–350 KiB JS gzipped total, so a single screen should use much less. Use `content-visibility: auto` with `contain-intrinsic-size` on category groups, or virtualize very long lists. Reserve space for images and fonts to keep CLS near 0.

### Cited Findings
- P75 budget: ~150 KiB HTML/CSS/fonts and ~300–350 KiB JS (gzipped). Baseline is a budget Android on a slow network. A quarter of users are slower still. — [Infrequently Noted via conffab summary](https://conffab.com/elsewhere/the-performance-inequality-gap-2024-infrequently-noted/); [Jim Nielsen notes](https://notes.jim-nielsen.com/n/2025-07-11-1311/) (secondary summaries; original post not fetched)
- `content-visibility: auto` skips layout and paint for off-screen content. In the demo, rendering went from 232ms to 30ms. Use `contain-intrinsic-size: auto <est>` to avoid scrollbar jumps. Supported in Chrome 85+, Firefox 125+, Safari 18+. Content stays in the accessibility tree. — [web.dev content-visibility](https://web.dev/articles/content-visibility)
- Fallback font metric overrides reduce CLS from font swaps. — [web.dev size-adjust](https://web.dev/articles/css-size-adjust)

### Inferences
- Budgets for the picker: CSS ≤ 25 KB gz (Tailwind purged), fonts ≤ 60 KB (2–3 woff2 Devanagari+Latin weights), route JS ≤ 100 KB gz, thumbnails ≤ 6 KB each. Lighthouse mobile targets: Performance ≥ 90, LCP < 2.5s, CLS < 0.1 (aim for 0), INP < 200ms (standard Core Web Vitals thresholds, not re-verified here).
- With up to ~1000 master items (e.g. the 952-item beauty list), use content-visibility per category group. Switch to windowing (e.g. @tanstack/react-virtual) if INP suffers on filter/search.

### Gaps
- The exact 2024/2026 Russell figures were not read from the primary post.

## Lessons from Indian apps praised for design

### Takeaway
Sources are thin and mostly agency blogs. The recurring principles are local-language-first onboarding, speed and clarity, forgiving search (Hinglish, typos), bottom sheets for one-handed use, and icon+text pairing for semi-literate users.

### Cited Findings
- Zepto rebuilt search to understand typos and Hindi phrasing, matching how Indian users naturally phrase queries (co-founder Kaivalya Vohra, Apr 2026). — [Storyboard18](https://www.storyboard18.com/brand-marketing/from-typos-to-hindi-phrases-zepto-says-its-search-now-understands-everyday-language-inputs-94148.htm)
- Khatabook and PhonePe greet users in local language and offer voice-based flows. — [BillCut multilingual onboarding](https://www.billcut.com/blogs/multilingual-onboarding-drop-offs-reduced) (agency blog). A claimed "34% retention" figure from Khatabook's redesign is unverified.
- PhonePe is described as mobile-optimised and focused on speed and clarity. — [ProCreator](https://procreator.design/blog/indian-startups-get-right-about-product-ux/) (agency, thin)
- A claimed Deloitte stat says 68% of Indian users equate ease of use with trustworthiness. This is unverified secondhand. — [BillCut](https://www.billcut.com/blogs/fintech-in-bharat-designing-for-digital-literacy)
- Google's localisation guide for Indian users exists. — [Think with Google PDF](https://thinkwithgoogle.com/_qs/documents/13326/App_and_website_localization_for_Indian_users.pdf) (not read in detail)

### Inferences
- What to borrow: CRED-style confidence comes from a dark/deep brand surface, generous type and few colours. Teal header plus one warm accent achieves this cheaply. From Blinkit/Zepto: big tappable rows, a persistent count/CTA bar at the bottom, and instant feedback. From Khatabook: bilingual labels side by side and a trust feel from solid colour and no broken-looking states.

### Gaps
- No primary design-team writeups from CRED, Meesho, Blinkit or Khatabook were found in this pass.
