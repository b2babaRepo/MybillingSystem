# Turn voice notes into shop-ready draft orders

Grocery Khata should build its AI order agent as a **single structured-output Claude call, not a free-roaming agent**. That call should get the shop's whole catalogue and options pre-loaded and cached. Before it run deterministic steps: speech-to-text (Sarvam Saaras for voice), on-device photo resizing and barcode scanning, and Hindi unit normalisation. After it run validation that rejects any item ID that is not in the shop's catalogue. The result is a draft order with Ready / Check / Not understood lines. The customer confirms it, and then it enters the packing, option-swap and changed-line approval flow the app already has. With only 72 common items and a few hundred options per shop, everything fits in one prompt, so retrieval tools and multi-turn loops add latency and failure modes without adding accuracy. The pattern itself is proven: Swiggy Instamart's "Say it / Scan it / Write it" list builder and Flipkart Wholesale's Sarvam-powered WhatsApp voice bot work this way. But nobody visible offers it to an independent kirana using that shop's own catalogue, prices and khata, and that is Grocery Khata's opening. Every vendor accuracy number found was vendor-reported or measured on a different task (printed Hindi, read speech). The most important early deliverable is therefore an in-house evaluation on 100–300 real orders. Model costs are tiny, about ₹0.1–2 per order. DPDP consent and retention design matter more than cost. WhatsApp should come after the in-app PWA, not first.

**How to read this report.** Claims with a citation come from the research notes and their linked sources. Claims marked *(vendor claim)* come from the vendor's own benchmark or marketing page. Claims marked *(inferred)* are design reasoning or estimates without a direct source. Claims marked *(unverified)* came from secondary sources or conflict with each other and should be checked before you rely on them.

## Capture stays on the phone, and heavy reading moves to the server

**Voice.** Record with `MediaRecorder` as WebM/Opus. It is supported on Chrome for Android ([caniuse](https://caniuse.com/mediarecorder)), and most speech-to-text APIs accept it ([lilting.ch](https://lilting.ch/en/articles/ai-voice-chat-input-implementation)). Use a press-and-hold button, mono audio and a cap of about 30 seconds. A 15-second order at 16–32 kbps is roughly 30–60 KB, which uploads fine on 3G *(inferred from codec bitrates)*. Do **not** build on the Web Speech API. In Chrome it sends audio to Google's servers, gives no vocabulary control, and does not work offline. Its on-device mode shipped first on desktop, with Android not named ([lilting.ch](https://lilting.ch/en/articles/ai-voice-chat-input-implementation); [blink-dev](https://groups.google.com/a/chromium.org/g/blink-dev/c/VNOok2dbmHM/m/TQpe9shjCgAJ)). On-device Whisper in WASM is probably too heavy for phones with 2–3 GB of RAM *(inferred)*.

**Photos.** Resize on the device with a canvas to about 1,500 px on the long edge as WebP/JPEG, roughly 150–300 KB. Apply the EXIF rotation before upload, and offer crop and retake. Claude charges ⌈w/28⌉×⌈h/28⌉ tokens, so a 1000×1000 image is 1,296 tokens. Accuracy drops on rotated, heavily compressed or tiny images, and the docs themselves recommend resizing before upload ([Claude vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)).

**Product packs.** For product packs, try `BarcodeDetector` first with `ean_13`. It is experimental, needs HTTPS, and you should use `getSupportedFormats()` to check what the device supports ([MDN](https://developer.mozilla.org/en-US/docs/Web/API/BarcodeDetector)). Look the code up in the shop's own EAN table, which you build as the shop bills. Open Food Facts is only a free fallback, because its coverage outside Western Europe is patchy ([OFF docs](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/tutorials/scanning-cosmetics-pet-food-and-other-products/)). Indian codes usually start with "890" *(inferred)*.

**Typed notes** need no capture work.

**Fitting the JavaScript budget.** All of the capture code must sit **outside the 130 KB first-load JavaScript budget**. Put the recorder, the resizer and any zxing-WASM barcode fallback in a lazily loaded "Order by voice/photo" chunk. Queue offline captures in IndexedDB and upload them when the connection returns, showing a "pending" state until then *(inferred)*.

**Speech-to-text.** Run it on the .NET backend behind an `ISpeechToText` interface.

- **Sarvam Saaras (v3, or v4 now recommended) is the default.** It covers 22 Indian languages plus Indian English, handles Hinglish natively, and has a "Codemix" output mode that keeps "Parle-G" and "packet" in Latin script. Sarvam reports **19.31% WER on IndicVoices** *(vendor claim, with no per-competitor numbers)*. It processes audio in India, does not train on API data, and deletes audio after processing by default ([Sarvam STT](https://www.sarvam.ai/speech-to-text); [Saarika docs](https://docs.sarvam.ai/api-reference-docs/getting-started/models/saarika)).
- **Sarvam's price is unconfirmed.** The docs say **₹30/hour billed per second**, but the marketing page says ₹1.5/minute ([pricing docs](https://docs.sarvam.ai/api-reference-docs/getting-started/pricing)). Either way it is under ₹0.4 for a 15-second order.
- **ElevenLabs' 5.5% Hindi WER is not comparable.** It was measured on read speech (FLEURS or Common Voice, labelled inconsistently) ([ElevenLabs](https://elevenlabs.io/speech-to-text/hindi)), not on noisy kitchen speech.
- **Fallbacks:** gpt-4o-mini-transcribe at about $0.003/min *(unverified aggregator price)* ([CostGoat](https://costgoat.com/pricing/openai-transcription)) and Google Chirp 3 ([docs](https://cloud.google.com/speech-to-text/v2/docs/chirp_3-model?hl=en)). The MIT-licensed AI4Bharat IndicConformer is a later self-hosting option ([GitHub](https://github.com/AI4Bharat/IndicConformerASR)).
- Do not try to fix brand names with speech-to-text keyword boosting. Let the catalogue-aware LLM step correct them *(inferred)*.

**Image reading.** Send the image straight to Claude rather than to a separate OCR step.

- **No public benchmark exists for handwritten Devanagari or Hinglish shopping lists.** The closest evidence is printed Hindi on 300 real scans (chrF++):

| System | Real-scan Hindi score |
|---|---|
| Gemini 2.5 Flash | 86.3 |
| Claude Opus 4.7 | 82.2 |
| Qwen3-VL-8B | 75.2 |
| GPT-5.5 | 58.5 |
| EasyOCR | 58.3 (93.6 on clean synthetic text) |

  Source: [arXiv 2606.29213](https://arxiv.org/abs/2606.29213). Numerals and matras are the main sources of error, and quantity is exactly the field that matters for billing.
- **A grocery list is short text from a small vocabulary.** The model can correct misread characters against the catalogue, so character error rates probably overstate item error rates *(inferred)*.
- **Dedicated OCR still needs an LLM to turn text into items.** Google Vision costs about $1.50 per 1,000 images ([pricing](https://cloud.google.com/vision/pricing)) but performs weakly on Indic text. Sarvam Vision 2.1 leads its own Indic benchmark *(vendor claim, flagged by [Analytics Vidhya](https://www.analyticsvidhya.com/blog/2026/09/sarvam-vision-2-1-review/))*. Keep either one only as a second opinion when confidence is low.
- **WhatsApp screenshots are typed text** and are the easiest input. Tell the model to ignore timestamps and chat UI *(inferred)*.

## One cached Haiku call, escalating to Sonnet, beats a tool-calling agent here

**The call.** Use one request with structured outputs (`output_config.format` with a JSON schema, or a `strict: true` `propose_order` tool), so the response is guaranteed to parse. The deprecated `output_format` parameter now returns a 400 without a beta header, and there is a C# SDK helper for the .NET backend ([Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).

**Schema limits mean the server must validate.** Every object needs `additionalProperties: false`. There are no numeric min/max or string-length constraints, enum casing can drift, and a refusal or a `max_tokens` cutoff can still break the schema. The server must check that quantities are above 0, compare enums case-insensitively, and handle refusals. Changing the schema also invalidates the prompt cache, so keep it fixed and versioned (same source).

**Prompt order for caching.** Arrange the prompt as tools → system → common catalogue → this shop's items and options (put a cache breakpoint here, per shop) → the household's recent "usual" picks → the untrusted order text or image. Use the 1-hour cache for busy shops *(inferred design)*.

**Model choice.**

- **Default to Haiku 5.5.**
- **Escalate to Sonnet 5.5** when the input is a handwritten photo, any line comes back low-confidence, or more than 25% of lines are unmatched *(inferred thresholds; tune them against your evaluation set)*.
- **Keep Opus 5.5 for offline gold labelling.**
- Add a multi-turn tool loop (`search_catalogue`, `get_shop_options`, `propose_order`, capped at about 3 turns) only if a shop grows past roughly 500 SKUs *(inferred)*.

**Current prices** per million tokens ([Pricing](https://platform.claude.com/docs/en/about-claude/pricing)):

| Model | Input | Cache hit | Output | Batch API (in / out) |
|---|---|---|---|---|
| Opus 5.5 | $4 | $0.20 | $20 | $2 / $10 |
| Sonnet 5.5 | $2 | $0.10 | $10 | $1 / $5 |
| Haiku 5.5 (prompts ≤100k tokens) | $0.10 | $0.01 | $0.50 | $0.05 / $0.25 |

Two caveats on these prices. First, the vision docs still cost Haiku-class images at Haiku 4.5's $1/M ([vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)), so check the Haiku 5.5 rate before budgeting. Second, the 286-token minimum cacheable prompt on the pricing page looks unusually low and is unverified. Data residency is not settled either: US-only inference costs 1.1x, and no India region was identified.

**Matching items to the shop's catalogue.** Follow Instacart's production pattern: retrieve candidates, have an LLM re-rank them, then validate that the answer stays inside the taxonomy. Instacart reports +18 points of precision and +70 points of recall on tail queries, and found that separate models can contradict each other, which argues for one joint extraction step ([Instacart](https://tech.instacart.com/building-the-intent-engine-how-instacart-is-revamping-query-understanding-with-llms-3ac8051ae7ac); [AI Engineer talk](https://ai.engineer/talks/how-instacart-transformed-its-search-and-discovery-using-an-llm-driven-approach)). For Grocery Khata the pipeline has four steps, all *(inferred design)*:

1. **Normalise units deterministically in C#.** paav = 250 g, adha kilo = 500 g, dhai sau gram = 250 g, sava kilo = 1.25 kg, dedh = 1.5, darjan = 12, and Devanagari numerals such as २.
2. **Generate candidates.** Run Postgres `pg_trgm` trigram search over English names, Hindi names and a new `item_aliases` table (cheeni/chini/shakkar → sugar, maida, sarson tel). Add pgvector only when aliases stop being enough.
3. **Let the model choose.** It picks only `catalogue_item_id` and `shop_option_id` values that were in the prompt. The server rejects any other ID.
4. **Map the household's preferences.**
   - **"Shop chooses"** leaves the option empty for the packing step.
   - **"Only this"** pins the option.
   - **"Ask me"** creates a clarifying question.
   - A bare "tel" falls back to the household's usual option at that shop.

**What the model returns for each line:** `raw_text`, `source_span`, item ID or null, option ID or null, `{qty, unit}`, preference, a confidence of high, medium or low, an `ambiguity_reason` (unknown_item, brand_unclear, qty_unclear, multiple_matches or not_stocked), and up to 3 alternatives. Model-reported confidence is poorly calibrated, so combine the confidence bucket with deterministic signals: the trigram score margin, whether the household bought the item before, and whether the unit parsed *(inferred)*. **Prices always come from the database and never from the model.**

## The draft never bills itself: customer confirms, shopkeeper finalises

The AI's output is a proposal stored apart from the order. The flow is:

1. **The customer sees the draft first.** It shows item pictures (reuse `ItemCard` and the aisle art), quantities, the shop's prices, and the original phrase under each line ("2 paav tamatar"). The customer can edit or re-speak a correction, and nothing reaches the shop until they tap Confirm.
2. **The shopkeeper gets a pick list grouped by aisle.** Lines are coloured **Ready (green)**, **Check (amber**: a guessed brand or quantity, with the voice clip or photo crop one tap away), or **Not understood (red**: the raw text shown word for word).
3. **The existing packing screen stays the final authority.** Option swaps and out-of-stock substitutions reuse the changed-line approval flow, and "ask me" lines become questions inside that same flow.
4. **Nothing is written to the khata until the shopkeeper finalises and the household approves the changed lines.**

Errors have very different costs. A wrong item or price on a credit ledger turns into an item-level dispute, while "item unclear, please call" is cheap. So **fail visibly, never silently** *(inferred; no shopkeeper user research exists, so a pilot is needed)*. JioMart's 2020 WhatsApp pilot, where the partner kirana generated the bill, shows that kiranas will fulfil orders routed to them ([The Next Web](https://thenextweb.com/facebook/2020/04/27/facebook-backed-reliance-jio-pilots-basic-grocery-ordering-service-on-whatsapp-in-india/)). Kirana owners also favour Hindi/Hinglish and WhatsApp-native tools ([OkCredit](https://okcredit.in/khata-app-for-kirana-store)).

**Audit trail.** Add an `ai_proposals` table holding the model ID, prompt version, input hash, a reference to the raw input, the proposed lines as JSON, tokens and latency. Each final order line links to its proposed line with an action of accepted, edited, replaced, deleted or added. This table is the audit trail for disputes, and it is also the evaluation dataset *(inferred design)*.

## WhatsApp is cheap to receive on but slow to launch; ship in-app first

**WhatsApp costs.** Since 1 July 2025 WhatsApp prices per message, and **messages from users to the business are free**, so receiving voice notes and photos costs nothing. Free-form replies are allowed only inside the 24-hour customer-service window ([Meta pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)). Three changes landed on 1 October 2026 or land soon:

- Service messages above **1,000 per month per number** became chargeable on 1 October 2026.
- Utility templates sent inside the window became chargeable on the same date.
- India accounts must move to INR billing by 31 December 2026, or delivery stops on 1 January 2027 (same source).

Indicative utility rates are about ₹0.13 per message *(unverified third-party figure)* ([MyOperator](https://support.myoperator.com/portal/en/kb/articles/whatsapp-has-shifted-to-per-message-pricing-effective-july-1-2025-based-on-message-categories-and-your-recipient-s-country-in-india-there-are-three-paid-categories)). That puts one shop with a few hundred orders a month at tens to low hundreds of rupees a month *(inferred)*.

**WhatsApp's AI policy.** Meta's ban on general-purpose AI assistants, in force from 15 January 2026, still allows task-specific sales and support bots ([TechCrunch](https://techcrunch.com/2025/10/18/whatssapp-changes-its-terms-to-bar-general-purpose-chatbots-from-its-platform)). A one-shop ordering bot fits that category, provided it is never marketed as a general assistant.

**Why the PWA comes first.** WhatsApp is still the habit: 45% of Indians prefer it for hearing from businesses ([MEF](https://mobileecosystemforum.com/mef_data_post/whatsapp-in-india)). But it brings real overhead: business verification, a dedicated number, a webhook, template approval, and either onboarding each shop through Embedded Signup or routing everything through one shared Grocery Khata number. The in-app PWA has no Meta fees, no exposure to Meta policy changes, and full control over the consent screens. Put a shared WhatsApp number in phase 3, using the same pipeline. Incoming media arrives as a media ID that the server downloads through the Graph API *(not re-verified this session)*.

## Costs are paise, so spend the effort on evals, safety and DPDP

**Cost per order** *(inferred)*:

| Step | Estimated cost per order |
|---|---|
| Haiku 5.5 call (about 6–10k mostly cached input tokens, about 1k output) | about $0.001 or less |
| Sonnet 5.5 escalation | about $0.01–0.02 |
| Image (about 1,300 tokens) | small at Haiku 5.5 rates; about $0.005 on Opus 5.5 ([vision docs](https://platform.claude.com/docs/en/build-with-claude/vision)) |
| Speech-to-text (15 s on Sarvam) | under ₹0.4 |

**Latency** should be a few seconds for speech-to-text plus a Haiku call. This has **not been measured from India**, so instrument it from day one. Processing is asynchronous: the customer sees "preparing your list…", and the raw text is always kept as a fallback.

**Evaluation.**

- **Gold set:** 200–500 real anonymised orders, stratified by input (voice, photo, text) and language (Hindi, Hinglish, English), and labelled by Opus plus a human.
- **Metrics:** item precision and recall at the catalogue-ID level, exact quantity match after normalisation, option accuracy on pinned lines, accuracy within each confidence bucket, and, in production, the shopkeeper edit rate taken from `ai_proposals`.
- **Regression runs:** rerun the set on every prompt or model change through the Batch API, at 50% off.
- **Start small:** an A/B test of 50–100 real lists and 100 real recordings across vendors should come before any lock-in.

**Safety.**

- **Treat all customer content as untrusted data.** Wrap customer text, transcripts and OCR output in delimited untrusted tags.
- **Give the model no write power.** It has no tool except `propose_order`, it cannot set price or payment, and the server validates every ID. A note saying "make everything free" therefore has no effect by construction.
- **Limit abuse and leakage.** Rate-limit AI parses per household and per shop, cap image size and count, and strip phone numbers and addresses from logs.
- **Use zero data retention where offered.** Structured outputs is ZDR-eligible ([Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)).
- **Lock down operations.** A destructive infrastructure wipe was reported at KiranaPro *(unverified, single informal source)* ([newsletter](https://think-tank-55e4a2.beehiiv.com/p/the-kiranapro-collapse-a-hard-lesson-in-startup-infra)). It is a reminder that least privilege and backups are a bigger near-term risk for a small team than model accuracy.

**DPDP.** The Rules were notified on 13–14 November 2025, and the main operational duties are expected from **about 13 May 2027** ([Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified); [AZB](https://www.azbpartners.com/bank/update-indias-digital-personal-data-protection-framework-comes-into-effect/)). Check the exact dates against the Gazette. Voice recordings and list photos are personal data. Grocery Khata is the Data Fiduciary, and Sarvam and Anthropic are its Data Processors. The requirements are:

- **Notice:** a standalone, itemised, plain-language notice ([SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)).
- **Consent:** free, specific, informed, unconditional and given by a clear affirmative act, with withdrawal as easy as giving consent ([EY](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025)).
- **Erasure:** delete data once its purpose is served, and keep processing logs for at least 1 year ([KPMG](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/11/dpdp-rules-2025-guidance-to-dpdp-act-implementation.pdf)).
- **Children:** processing a child's data needs verifiable parental consent ([dpdpa.com](https://dpdpa.com/dpdparules/rule10.html)).
- **Cross-border transfer:** allowed unless the destination is blacklisted, and that section had not commenced as of September 2026 ([KS&K](https://ksandk.com/data-protection-and-data-privacy/cross-border-data-transfers-dpdp-act-india/)).

What the app should do *(inferred)*:

- Show a Hindi and English notice on first use of the mic or camera. It names what is collected, why, which providers process it (Anthropic is outside India), how long it is kept, and how to withdraw.
- Ask for 18+ age confirmation.
- Ask for separate opt-in for any model-improvement use, including adding orders to the eval set.
- Delete raw audio and photos 7–30 days after the order is closed, keeping them only for dispute review.
- Keep the transcript and order permanently as part of the khata records.
- Share only the transcribed list with the shopkeeper unless the customer agrees to share the original clip.

Get a legal review on whether voice counts as biometric data.

## A four-phase build plan

| Phase | Scope | Exit criterion |
|---|---|---|
| **0: Eval first (2 weeks)** | Collect 100 consented voice orders, 100 list photos and 50 typed notes from pilot households. Build the `item_aliases` table and the C# unit normaliser. Label gold orders with Opus. | Baseline item F1 and quantity accuracy measured per input type for Sarvam vs gpt-4o-mini-transcribe, and for Haiku vs Sonnet vision. |
| **1: Typed and screenshot notes in-app** | Add an `/orders/ai-draft` endpoint: normalise, trigram candidates, a cached Haiku structured call, ID validation, and an `ai_proposals` row. Add the customer confirm screen and Ready/Check/Red lines on the shop packing screen. | Shopkeeper edit rate under about 20% on pilot shops *(target, inferred)*. |
| **2: Voice and photo in-app** | Lazy-loaded capture chunk outside the 130 KB budget, IndexedDB offline queue, Sarvam Saaras codemix, Sonnet escalation for handwriting, barcode scan against the shop EAN table, DPDP consent and retention jobs. | Measured p95 latency, consent coverage at 100%, audio deletion job verified. |
| **3: WhatsApp and scale** | Shared Grocery Khata WhatsApp number routing to the chosen shop, with the draft returned as a link to the confirm screen. Nightly Batch API eval regression. Tool-loop retrieval only for shops above about 500 SKUs. | Fees tracked against the INR rate card; INR billing migration done before 31 Dec 2026. |

## Conclusion

The hard problem here is not choosing a model. It is turning messy, code-mixed household speech into **one shop's own options under the household's own preference rules**, and doing that inside a credit ledger where a silent mistake becomes a dispute. Grocery Khata already has the structures that make AI safe: the "usual" defaults, the three preference modes, packing swaps, changed-line approval and item-level disputes. The AI only needs to produce a draft that slots into them. That makes the app's existing transparency features its main defence against AI errors.

The biggest uncertainty is accuracy on real kirana input. No independent benchmark covers handwritten Hinglish lists or noisy Hindi grocery speech, and every headline figure found was either vendor-built or measured on a different task. Treat phase 0's evaluation set as the product's core asset. Once `ai_proposals` captures shopkeeper edits, that data becomes something larger platforms do not have for independent kiranas: an alias dictionary and a per-shop error map that improve with every order billed.
