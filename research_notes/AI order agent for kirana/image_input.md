# Reading grocery orders from camera photos (handwritten lists, product packs, WhatsApp screenshots)

## How well do multimodal LLMs read handwritten Devanagari / Hinglish lists? Benchmarks?

### Takeaway
I found no public benchmark of frontier LLMs on handwritten Devanagari or Hinglish shopping lists. The closest evidence is printed Hindi on real scans, where Gemini 2.5 Flash and Claude Opus 4.7 lead clearly and GPT-5.5 lags by a wide margin. Nine of ten systems drop sharply on real scans compared with synthetic text. So an in-house evaluation on real kirana lists is required before choosing.

### Cited Findings
- "Can OCR-VLMs Read Devanagari?" (arXiv, June 2026) tested 10 systems on Hindi: 4 synthetic degradation conditions plus 300 real printed scans. All scored 91–98 chrF++ on clean synthetic text, but 9 of 10 collapsed on real scans. Results on real scans: Gemini 2.5 Flash 86.3, Claude Opus 4.7 82.2, Qwen3-VL-8B 75.2, GPT-5.5 58.5, EasyOCR 58.3 (down from 93.6), olmOCR-7B 40.5. DeepSeek-OCR occasionally produced runaway repetition (up to 71x the reference length). — [arXiv 2606.29213](https://arxiv.org/abs/2606.29213)
- The same paper: strong English OCR does not predict Indic performance. Errors fall into two groups: surface errors (numerals, punctuation) and structural errors (conjuncts, matras, nukta). Specialised OCR-VLMs were the most fragile when images were degraded. — [arXiv 2606.29213](https://arxiv.org/abs/2606.29213)
- A separate arXiv study tests Gemini, GPT and Claude on transcribing handwritten Sanskrit manuscripts (historical script, not modern lists). — [arXiv 2608.18696](https://arxiv.org/pdf/2608.18696)
- An earlier benchmark covers LLM OCR on low-resource scripts. — [arXiv 2412.16119](https://arxiv.org/pdf/2412.16119)
- Academic handwriting data: IIIT-INDIC-HW-WORDS (IIIT Hyderabad CVIT) has 872K word images from 135 writers in 8 scripts. Devanagari comes from IIIT-HW-Dev, and the Hindi test split is about 12.9K words. — [CVIT](https://cvit.iiit.ac.in/research/projects/cvit-projects/iiit-indic-hw-words); [PLATTER paper](https://arxiv.org/pdf/2502.06172)
- CVIT also hosts a camera-captured, unconstrained Indic handwriting dataset, which is closer to phone photos of paper scraps. — [CVIT UCCIOHD](https://cvit.iiit.ac.in/usodi/ucciohd.php)
- An olmOCR-2-7B Devanagari LoRA adapter, trained partly on IIIT handwritten crops, notes that accuracy varies by writer style. — [Hugging Face](https://huggingface.co/enclavelabs/enclave-scribe-devanagari)

### Inferences
- From this evidence, Gemini and Claude are the most credible first choices for Devanagari. GPT-family models should be tested before being relied on for Hindi script.
- A grocery list is short, low-vocabulary text, and an LLM can fix misread characters using context and the shop's catalogue. That makes the job easier than general OCR, so character-level error rates overstate how many items will actually be wrong.
- Numerals are a known weak spot (surface errors), and quantity is the field that matters most for billing. Quantities need the most careful confirmation in the UI.

### Gaps
- No published accuracy figures for Claude 5.5 models, GPT-4o/4.1 or Gemini on handwritten Hindi or Hinglish lists. The Hindi baseline numbers in the IIIT-HW papers were not retrieved.
- Romanised Hinglish handwriting ("2 kg cheeni") is unbenchmarked as far as I found.

## Dedicated OCR options: accuracy on handwriting, cost per image

### Takeaway
Dedicated OCR gives you raw text only. You would still need an LLM to turn it into items. Its main advantage is cost, for example Google Vision at about $1.50 per 1,000 images. Sarvam Vision is the strongest India-specific option on its own Indic benchmark, but that benchmark is vendor-built. For short lists, a single vision-LLM call that reads and structures the list together is simpler, and given the evidence above probably at least as accurate.

### Cited Findings
- Google Cloud Vision: DOCUMENT_TEXT_DETECTION is the feature for dense text and handwriting. It costs $1.50 per 1,000 units for 1,001 to 5M units a month, $0.60 per 1,000 above 5M, and the first 1,000 units a month are free. Billing is per feature per image. Language hints are supported, and language does not change the price. — [Cloud Vision pricing](https://cloud.google.com/vision/pricing); [handwriting docs](https://docs.cloud.google.cn/vision/docs/handwriting?hl=ja)
- Sarvam Vision 2.1 reports 87.39 overall on Sarvam's Indic OCR Bench (6,909 samples, 22 languages plus English) and 87.3 on olmOCR-Bench. The original Sarvam Vision scored 95.91 on Hindi against Gemini 3 Pro's 95.12. Sarvam's own tables show Google Cloud Vision as weak on Indic text. — [Sarvam Vision 2.1 blog](https://www.sarvam.ai/blogs/sarvam-vision-2-1); [Sarvam Vision](https://www.sarvam.ai/blogs/sarvam-vision)
- An independent review cautions that this benchmark is built by the vendor that wins it. — [Analytics Vidhya review](https://www.analyticsvidhya.com/blog/2026/09/sarvam-vision-2-1-review/)
- EasyOCR, a classical open-source engine, fell from 93.6 to 58.3 chrF++ on real printed Hindi scans. Classical open-source engines are therefore a poor fit even before handwriting. — [arXiv 2606.29213](https://arxiv.org/abs/2606.29213)
- Qwen3-VL-8B (open weights, runs on a single 24 GB GPU) scored 75.2 on real Hindi scans, making it the best self-hostable option tested. — [arXiv 2606.29213](https://arxiv.org/abs/2606.29213)

### Inferences
- Tesseract Hindi and PaddleOCR are not covered by any 2025–26 evaluation I found. By analogy with EasyOCR they are likely weak on handwriting, so treat them as unverified.
- A dedicated OCR step only pays off at high volume, where cost dominates. It could also serve as a second opinion when the LLM reports low confidence.

### Gaps
- Not verified: Azure Document Intelligence's handwriting support for Hindi and its current per-page price; Google Document AI pricing; Sarvam's and Bhashini's per-image pricing; AI4Bharat IndicOCR accuracy. None of these were retrieved in this pass.
- I found no Sarvam figures broken out for handwriting specifically.

## Product-pack recognition and barcode lookup

### Takeaway
Scan the barcode in the browser first, then match it against the shop's own catalogue, where shops record their own EAN codes. Open Food Facts is a free, keyless fallback, but its India coverage is patchy. When there is no barcode, which is common for crushed empty packets or loose items, a vision LLM reading brand, variant and pack size from the photo is the fallback.

### Cited Findings
- BarcodeDetector API: marked experimental and not Baseline. It requires HTTPS, works in Web Workers, and supports format strings such as "ean_13". `getSupportedFormats()` reports what the device actually supports. — [MDN BarcodeDetector](https://developer.mozilla.org/en-US/docs/Web/API/BarcodeDetector)
- Open Food Facts: a keyless, crowd-sourced API that looks products up by GTIN-8/12/13/14 (EAN/UPC) and asks callers to send a descriptive User-Agent. Coverage is strongest in France and Western Europe and patchier elsewhere. I found no official India coverage figure. — [OFF API docs](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/tutorials/scanning-cosmetics-pet-food-and-other-products/); [Firecrawl summary](https://www.firecrawl.dev/alexandria/world-openfoodfacts-org)

### Inferences
- Barcodes on Indian packs usually start with GS1 India's "890" prefix. Because Open Food Facts coverage is uncertain, a lookup table the shop builds itself as it bills will probably outperform any external database.
- Plan a zxing-js (or similar WASM) fallback for devices where BarcodeDetector is missing. Check support at runtime with `getSupportedFormats()`.

### Gaps
- I did not retrieve MDN's compatibility table, so the minimum Chrome for Android version for BarcodeDetector is unverified. My unverified understanding is that it has been supported on Android Chrome for years.
- GS1 India's DataKart/Verified API access and terms were not researched.
- No accuracy data found for LLMs reading Indian pack sizes or variants.

## Capture tips, image token cost, latency

### Takeaway
Resize photos on the phone to about 1000–1600 px on the long edge, with moderate JPEG/WebP compression. That costs roughly 1,300–1,600 Claude tokens, or about $1.30 per 1,000 images on Haiku-class pricing and about $5 per 1,000 on Opus 5.5. It also keeps uploads small on weak networks.

### Cited Findings
- Claude cost formula: ⌈width/28⌉ × ⌈height/28⌉ visual tokens. Claude 4.7 and later models accept up to a 2,576 px long edge and 4,784 tokens; older models up to 1,568 px and 1,568 tokens. Larger images are downscaled automatically. — [Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)
- Examples from those docs: a 1000×1000 image is 1,296 tokens, about $1.30 per 1,000 images at Haiku 4.5's $1/M input price and about $5.18 per 1,000 at Opus 5.5's $4/M. A 4K image on Opus 5.5 costs about $19.14 per 1,000. High-resolution images can use about 3x the tokens. — [Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)
- Limits: 10 MB per image (base64) and a maximum of 8000×8000 px. Supported formats are JPEG, PNG, GIF and WebP. Accuracy drops on low-quality or rotated images and those under 200 px. Heavy JPEG compression can make text hard to read. The docs recommend resizing before upload to cut latency, and the Files API to avoid resending images on every turn. — [Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)

### Inferences
- Shrinking a typical 12 MP phone photo on the device (canvas/WebP at about 1,500 px, around 150–300 KB) cuts upload time on 3G/4G far more than it costs in accuracy.
- Correct the orientation from EXIF before upload, since the model does not read metadata. Let the user crop to the list, and allow a retake.

### Gaps
- Current per-image costs for GPT-4.1 and Gemini 2.x were not retrieved.
- No measured end-to-end latency for vision calls from India was found.

## Failure modes and confirming uncertain items

### Takeaway
Have the model return, for each line: the raw text as written, a normalised name, brand, quantity, unit, a confidence level and a flag (struck-through, unclear quantity, ambiguous name). Then map every item to the shop's catalogue and show low-confidence items for a one-tap fix. Never auto-bill an item that has not been confirmed.

### Cited Findings
- Numerals and diacritics (matras, conjuncts, nukta) are the main sources of error in Devanagari OCR. — [arXiv 2606.29213](https://arxiv.org/abs/2606.29213)
- Claude's documentation advises human verification for tasks that need precision, and says model counts and outputs can be approximate. — [Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)

### Inferences
- Common failures to expect:
  - quantities with no unit ("2" with no kg/packet)
  - Hindi numerals (२)
  - fractions ("½ kg", "pav", "dhai sau gram")
  - crossed-out lines
  - ditto marks
  - a brand without a size ("Fortune tel")
  - local and synonym names: sarson tel = mustard oil, maida = refined flour, cheeni = sugar, chawal = rice, dal names
- A synonym dictionary per shop, combined with the shop's catalogue, should resolve most of these cases.
- WhatsApp screenshots are typed text, which is much easier to read than handwriting. Ask the model to ignore timestamps and UI elements.
- In the UI, group results into three buckets: confirmed, needs check (amber, with the original crop or raw text shown) and unreadable.

### Gaps
- I found no published studies on error rates for this exact "grocery list to structured items" task in India.

**Overall recommendation (inference):** Use one vision-LLM call per image that returns structured JSON, preceded by on-device resizing and barcode scanning, followed by catalogue matching and a confirmation screen. Start with Claude Sonnet- or Haiku-class models or Gemini Flash. Escalate to Opus, or a second OCR pass with Sarvam, only when confidence is low. Run an eval on 50–100 real local lists before committing.
