# Product image size, format and memory for a mobile card grid

## What pixel size is enough for a 150–180 CSS px card on 2–3x DPR phones? One size or two?

### Takeaway
The pixel size you need is CSS width × DPR. That is 300–360 px at 2x and 450–540 px at 3x. A 320–360 px square thumbnail for the grid plus the existing 512 px image for detail and zoom covers low-end phones, which are mostly 2x to 2.75x.

### Cited Findings
- `srcset`/`sizes` exist so the browser can choose a candidate that matches the slot width × DPR. Set `width`/`height` on every `<img>` so the browser reserves space. Without them, dimensions default to 0×0 and a lazy gallery may load every image at once. — [web.dev lazy loading](https://web.dev/articles/browser-level-image-lazy-loading)

### Inferences
- The arithmetic: 170 CSS px × 2 = 340 px; × 2.75 = 468 px; × 3 = 510 px. A 320 px thumbnail looks slightly soft on 3x screens. On a white pack shot this is rarely visible at card size, because people do not inspect cards closely.
- Recommendation: produce two renditions per photo, `thumb` (320 or 360 px) and `full` (512 px, what is stored today). Serve them with `srcset="…thumb 360w, …full 512w" sizes="(max-width: 480px) 45vw, 180px"`. On 2x phones the browser then picks the thumbnail; on 3x it may pick 512.
- Going above 512 px for cards adds no value. Even the detail view at around 360 CSS px wide would only need about 1080 px at 3x. 512 px is acceptable on low-end phones; add a 1024 px rendition later only if the detail view looks soft.

### Gaps
- I found no authoritative 2026 data on the DPR distribution of Indian low-end Android phones.

## WebP vs AVIF vs JPEG: sizes, browser support and canvas encoding

### Takeaway
Keep WebP. Almost every browser decodes AVIF (about 96% of global usage), and AVIF is about 13–30% smaller. But browser canvas encoding of AVIF is not dependable. WebP canvas encoding works in Chrome/Android, which already covers the client-side pipeline. AVIF only makes sense as a server-generated extra rendition.

### Cited Findings
- `canvas.toBlob`: browsers must support PNG; many also support `image/jpeg` and `image/webp`. If the requested type is unsupported, the output is silently PNG. `quality` (0–1) applies to lossy formats. MDN does not list AVIF. — [MDN toBlob](https://developer.mozilla.org/en-US/docs/Web/API/HTMLCanvasElement/toBlob)
- Firefox's bug requesting AVIF canvas encoding (1821907) was still open, and it notes Blink also lacked the encoder. — [Bugzilla 1821907](https://bugzilla.mozilla.org/show_bug.cgi?id=1821907); runtime check of `blob.type` recommended — [dev.to](https://dev.to/dailytechid/canvastoblob-can-silently-hand-you-back-a-png-when-it-cant-encode-what-you-asked-for-4blo)
- AVIF decode support: Chrome 85+, Samsung Internet 14+, current Chrome for Android. Global support is about 96.35%. caniuse does not list Android WebView separately. — [caniuse AVIF](https://caniuse.com/avif)
- AVIF vs WebP at matched quality: estimates range from about 13% smaller to 20–35% smaller. AVIF encoding is reported to be about 47x slower than WebP at default speed. These are vendor or blog benchmarks, so treat them as directional. — [dev.to benchmarks 2026](https://dev.to/_544bf5cbd223c35a49756/avif-vs-webp-vs-jpeg-real-benchmarks-2026-44ne), [pixotter](https://dev.to/pixotter/webp-vs-avif-which-next-gen-image-format-wins-in-2026-3m1d)
- An 800×800 product photo is typically 50–90 KB as WebP q80 versus 80–150 KB as JPEG q80. WebP q75–85 is described as imperceptible for product shots. These are vendor claims. — [rewarx](https://www.rewarx.com/blogs/optimize-ecommerce-product-images-core-web-vitals-speed-2026)

### Inferences
- In the browser upload code, check `blob.type === 'image/webp'` after `toBlob`. If the device falls back to PNG, retry as `image/jpeg` at 0.8, so you never upload a large PNG.
- Pack shots on white compress very well: large flat areas cost almost nothing. Scaling the 800 px figure by pixel area suggests about 15–30 KB at 512 px q80 and about 6–15 KB at 320 px. These are estimates to verify on real catalogue photos.

### Gaps
- No primary 2026 source confirms whether Chrome for Android's `toBlob('image/avif')` now works. Feature-detect at runtime instead of assuming.

## Byte budgets and quality settings

### Takeaway
Budgets: thumbnail (320–360 px) ≤ 15–20 KB, full (512 px) ≤ 40–60 KB, at WebP quality around 0.75–0.8. The current 150 KB ceiling is about 3–5x more than a 512 px white-background pack shot needs.

### Cited Findings
- WebP q75–85 is the commonly recommended range for product photos. — [rewarx](https://www.rewarx.com/blogs/optimize-ecommerce-product-images-core-web-vitals-speed-2026)
- Quality numbers do not carry across formats ("WebP 80" ≠ "AVIF 80"); compare by SSIM or by eye. — [dev.to benchmarks](https://dev.to/_544bf5cbd223c35a49756/avif-vs-webp-vs-jpeg-real-benchmarks-2026-44ne)

### Inferences
- Encode at 0.8 first. If the result is over budget, step down to 0.7 and then 0.6 rather than shrinking the pixel size.
- Data cost: a grid of 40 cards at about 15 KB each is about 600 KB. At 150 KB each it would be about 6 MB, which matters on prepaid Indian data plans.

### Gaps
- I could not find a primary study of byte sizes for white-background pack shots specifically.

## Front-end loading: srcset, lazy, decoding, fetchpriority, layout shift, virtualisation

### Takeaway
Use `loading="lazy"`, `decoding="async"`, explicit `width`/`height` (or CSS `aspect-ratio:1`), and `fetchpriority="high"` only on the first 2–4 above-the-fold cards. For hundreds of items, virtualise the list or use `content-visibility:auto`.

### Cited Findings
- On connections slower than 4G, Chrome starts lazy-loading images 2500 px from the viewport (4000 px on faster connections), and these values are hardcoded. Combining `lazy` with `fetchpriority="high"` is pointless. `loading="eager"` does not make an image load faster. Missing dimensions cause layout shift and can trigger loading the whole gallery. — [web.dev](https://web.dev/articles/browser-level-image-lazy-loading)

### Inferences
- 2500 px is roughly 3–4 screens of cards, about 15–25 images, so native lazy loading still preloads a fair amount. Virtualisation (e.g. react-window or TanStack Virtual) also limits how many decoded bitmaps exist at once, which matters for memory (next section).
- Do not mark the first row `lazy`, because they are likely the LCP element.

### Gaps
- I did not fetch a primary source on `content-visibility` performance numbers.

## Memory: decoded bitmap cost on low-end phones

### Takeaway
Memory depends on the decoded pixels, not on file bytes. Each decoded image takes width × height × 4 bytes: 512² ≈ 1.0 MB, 360² ≈ 0.52 MB, 320² ≈ 0.41 MB. Serving a 320–360 px thumbnail in the grid roughly halves image RAM compared with 512 px.

### Cited Findings
- I found no fetched primary source in this session. The width × height × 4 (RGBA) formula is standard knowledge and should be confirmed against a web.dev or Chromium source before the report cites it.

### Inferences
- 200 cards at 512 px ≈ 200 MB decoded if all stay in memory. That can get the tab killed on 2–3 GB phones. At 320 px it is about 80 MB, and with virtualisation (about 30 live cards) it is about 12–30 MB.
- Ways to cap memory: serve the smallest adequate rendition, virtualise, avoid large CSS background images, and do not keep blob/object URLs alive (call `URL.revokeObjectURL`). In the upload flow, release canvases by setting width and height to 0 after encoding, and use `createImageBitmap` with `resizeWidth` to avoid decoding the full 12 MP camera image at full size where supported.

### Gaps
- I have no measured data on Chrome's per-tab memory limits on low-end Android in 2026.

## Server-side resizing in .NET, storage in Postgres vs object storage/CDN, service worker caching

### Takeaway
To create the thumbnail, the cheapest path is to generate it on the phone, next to the 512 px image, and upload both. That needs no server library. If the server must resize, ImageSharp is free under $1M annual revenue. NetVips is fastest; SkiaSharp is a reasonable choice. Postgres bytea is fine at this scale as long as the immutable cache headers keep reads off the database. Move to R2 plus Cloudflare CDN when traffic grows: there is no egress fee, storage is $0.015/GB-month, and the first 10 GB are free.

### Cited Findings
- Under the Six Labors Split License, use is free (Apache 2.0) for a direct dependency by a for-profit company with less than $1M annual gross revenue. Other cases need a commercial licence. — [ImageSharp licence on NuGet](https://packages.nuget.org/packages/SixLabors.ImageSharp/3.1.3/License), [license text](https://scancode-licensedb.aboutcode.org/six-labors-split-1.0.html). A licence-key file reportedly applies only to ImageSharp 4.0+. — [ABP support (secondary)](https://abp.io/support/questions/10663/Dependency---SixLabors-License-Change)
- Benchmark: NetVips was fastest; ImageSharp and SkiaSharp had similar speed. SkiaSharp allocates the least managed memory but uses significant native memory. — [Anthony Simmon benchmark](https://anthonysimmon.pages.dev/benchmarking-dotnet-libraries-for-image-resizing); NetVips binding — [net-vips GitHub](https://github.com/kleisauke/net-vips)
- R2: $0.015/GB-month, a free tier of 10 GB, 1M Class A and 10M Class B operations, and zero egress fees. — [Cloudflare R2](https://cloudflare.com/en-ca/products/r2), [Filebase overview](https://filebase.com/blog/cloudflare-r2-pricing-costs-savings-and-alternatives-in-2026/)

### Inferences
- Scale check: 10,000 products × (60 KB + 20 KB) ≈ 800 MB. That is trivial for Postgres. The real costs are larger backups and API/DB load when images are served without a CDN. With `immutable` plus `?v=` caching, each phone fetches each image once. Putting Cloudflare (free plan) in front of the existing URL gives CDN caching without changing storage.
- Service worker: cache image URLs with a cache-first strategy. Because URLs are versioned, entries never go stale. Cap the cache, e.g. Workbox ExpirationPlugin with maxEntries of about 300–500, so a full catalogue does not fill phone storage.

### Gaps
- I did not confirm the libvips/NetVips licence (believed to be LGPL-2.1 for libvips and MIT for NetVips; verify) or Bunny CDN pricing.

## Background removal / white-background trimming

### Takeaway
I did not research this in this session. What follows is a suggested approach only.

### Cited Findings
- No sources were gathered.

### Inferences
- A cheap uniform look comes from auto-trimming near-white borders and then padding to a square on pure #FFFFFF with a fixed margin of about 6–8%. This can be done in the canvas on the phone at no cost. Full background removal (ML models such as in-browser segmentation, or paid APIs) costs more CPU or money. It is likely unnecessary for pack shots photographed on white.

### Gaps
- Costs and quality of background-removal APIs and of in-browser models on low-end phones were not researched.
