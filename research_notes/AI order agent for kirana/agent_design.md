# AI Order Agent for Kirana (Grocery Khata): Agent Design

Research was kept short: 3 web lookups and 2 docs fetches. Claims marked "design inference" come from general practice and have no source behind them.

## What does Anthropic's current documentation recommend for tool use, structured outputs, prompt caching, vision and model choice, and what are current Claude 5 family prices?

### Takeaway
Use `output_config.format` (JSON schema) or `strict: true` tools so output is guaranteed to parse. Cache the stable catalogue prefix, because cache hits on Opus/Sonnet 5.5 cost 0.05x base input. Haiku 5.5 ($0.10 in / $0.50 out per MTok) costs so little that a Haiku-first design with Sonnet escalation is the clear default.

### Cited Findings
- Structured outputs has two parts. JSON outputs (`output_config.format`, `type: "json_schema"`) make the response text match the schema. Strict tool use (`strict: true`) guarantees valid tool names and inputs. The two can be combined. — [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- The old `output_format` parameter is deprecated and needs the `structured-outputs-2025-11-13` beta header, otherwise the request fails with a 400. — [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- Supported models include Opus 5.5, Sonnet 5.5 and Haiku 5.5. There is a C# SDK helper, which matters for the .NET backend. — [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- Schema limits:
  - Every object needs `additionalProperties: false`.
  - There are no numeric min/max and no string length constraints.
  - `minItems` can only be 0 or 1.
  - Recursive schemas are not allowed.
  - A request can have at most 20 strict tools, 24 optional parameters and 16 union-type parameters.
  - Enum casing may drift, so compare case-insensitively.

  Implication: range checks (for example, quantity greater than 0) must be validated server-side. — [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- Changing `output_config.format` invalidates the prompt cache. Compiled grammars are cached for 24 hours, and the first request with a new schema is slower. A refusal (`stop_reason: "refusal"`) or `max_tokens` can produce output that does not match the schema. — [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- Prices per MTok (input / 5m cache write / 1h cache write / cache hit / output):

  | Model | Input | 5m cache write | 1h cache write | Cache hit | Output |
  |---|---|---|---|---|---|
  | Opus 5.5 | $4 | $5 | $8 | $0.20 | $20 |
  | Sonnet 5.5 | $2 | $2.50 | $4 | $0.10 | $10 |
  | Haiku 5.5 (prompts ≤100k tokens) | $0.10 | $0.125 | $0.20 | $0.01 | $0.50 |
  | Haiku 5.5 (prompts >100k tokens) | $0.50 | — | — | — | $2.50 |

  — [Pricing](https://platform.claude.com/docs/en/about-claude/pricing)
- The Batch API gives a 50% discount on both input and output:
  - Opus 5.5: $2 / $10
  - Sonnet 5.5: $1 / $5
  - Haiku 5.5: $0.05 / $0.25

  — [Pricing](https://platform.claude.com/docs/en/about-claude/pricing)
- The pricing page lists a minimum cacheable prompt of 286 tokens for Opus/Sonnet/Haiku 5.5, with Haiku showing 406 in a second column. This is unusually low, so check it against the prompt-caching page. US-only inference (`inference_geo`) costs 1.1x. — [Pricing](https://platform.claude.com/docs/en/about-claude/pricing)

### Inferences
- Cost per order (design inference):
  - Assumed sizes: a cached system prompt plus a compact 72-item catalogue of about 4–6k tokens, shop options of about 2–4k, history of about 0.5k, the order text of about 0.2–0.5k, and output of about 0.8–1.5k.
  - Haiku 5.5: about $0.001 or less per order.
  - Sonnet 5.5: about $0.01–0.02 per order.
  - A photo adds image tokens. Check the vision docs for the token formula (not fetched).
- Architecture: the default should be one structured-output call with all context pre-loaded. With 72 items and a single shop, the whole catalogue fits in the prompt, so retrieval tools are not needed. Keep a tool loop (`search_catalogue`, `get_shop_options`, `get_household_history`, then a final `propose_order` strict tool) only for large shop inventories (more than about 500 SKUs). Even then, cap it at about 3 turns.
- Model choice: Haiku 5.5 extracts first. Escalate to Sonnet 5.5 when any of these hold:
  - the input is a photo,
  - any line has confidence below the threshold,
  - more than 25% of lines are unmatched.

  Opus is for offline eval labelling only.
- Prompt caching: order the prompt as tools → system → catalogue → shop options (cache breakpoint per shop) → household history → order. Use the 1h cache for busy shops. Keep the schema fixed so the cache is not invalidated.
- Use the Batch API for non-urgent work: nightly re-scoring and eval runs, not live orders.

### Gaps
- Vision image-token formula, recommended image size and multi-image limits: the page was not fetched.
- Tool-use "building effective agents" guidance: not fetched in this pass.
- The 286-token cache minimum is unverified.

## What patterns do production order-taking agents use for item matching and confirmation?

### Takeaway
Production systems use retrieve-then-LLM-rerank against a closed taxonomy, followed by validation that rejects hallucinated IDs. Confirmation is explicit and done by a human or the customer. Indian deployments are WhatsApp-first with voice in regional languages.

### Cited Findings
- Instacart Intent Engine:
  - Retrieves the top-K candidate categories for a query.
  - An LLM re-ranks them with Instacart context.
  - Validation layers filter hallucinations so outputs stay inside the taxonomy.
  - Semantic Role Labeling extracts product, brand and attributes.

  — [Instacart: Intent Engine](https://tech.instacart.com/building-the-intent-engine-how-instacart-is-revamping-query-understanding-with-llms-3ac8051ae7ac)
- Instacart enriches prompts with query-understanding output, historical conversions and search logs. — [Supercharging Discovery](https://tech.instacart.com/supercharging-discovery-in-search-with-llms-556c585d4720)
- Instacart reports +18 pp precision and +70 pp recall on tail-query classification. Separate models can clash: a brand tagger was right while the spell corrector rewrote the query to "hummus". This is an argument for one joint extraction step. These figures come via a secondary summary of the talk. — [AI Engineer talk](https://ai.engineer/talks/how-instacart-transformed-its-search-and-discovery-using-an-llm-driven-approach)
- Flipkart and Sarvam AI built a voice-led WhatsApp wholesale ordering bot for small retailers that takes regional-language commands. — [MIT Sloan ME](https://www.mitsloanme.com/article/flipkart-brings-voice-led-wholesale-ordering-to-whatsapp-with-sarvam-ai/)
- JioMart offers end-to-end shopping on WhatsApp, built by Haptik. — [Haptik case study](https://haptik.ai/resources/case-study/jio-mart)
- Kirana WhatsApp templates (AiSensy) structure repeat ordering. They do not parse free-form lists. — [AiSensy](https://aisensy.com/whatsapp-chatbot-template/kirana-order-bot)

### Inferences
- Matching pipeline for Grocery Khata (design inference):
  1. **Normalize.** Map units deterministically before the LLM: paav→250 g, adha kilo→500 g, dhai sau gram→250 g, sava kilo→1.25 kg, dedh→1.5, darjan→12. Also strip Devanagari/Latin noise.
  2. **Generate candidates.** Use a Postgres `pg_trgm` trigram search over the English name, the Hindi name and a transliterated alias table (for example "cheeni/chini/shakkar"→sugar, "Maggi"→noodles brand). A pgvector embedding is optional and only worth it once the alias table stops being enough.
  3. **Select with the LLM.** The model picks a `catalogue_item_id` from the provided candidates only. Use enum-like validation: reject any ID not in context.
  4. **Choose the option from preference.** "Shop chooses" leaves the option to the shop. "Ask me" produces a clarifying flag. "Only this" pins the brand.
- Per-line output: raw_text, catalogue_item_id or null, shop_option_id or null, quantity {value, unit}, preference, confidence (high/medium/low), ambiguity_reason enum (unknown_item, brand_unclear, qty_unclear, multiple_matches, not_stocked), alternatives [up to 3 ids], and source_span (a pointer into the transcript or image).
- Model confidence numbers are poorly calibrated, so use a categorical bucket plus deterministic signals: the trigram score margin, whether the household bought this item before, and whether the unit parse succeeded.

### Gaps
- Swiggy/Zepto/Blinkit AI assistant internals and restaurant voice-ordering case studies (for example, Wendy's FreshAI) were not researched in this pass.

## How should confidence and ambiguity be shown in the shopkeeper UI, and how should HITL, eval and safety be handled?

### Takeaway
The AI output is a draft. The shopkeeper's packing screen stays the final authority, and every AI-proposed line is stored separately from the finalized line. Nothing is ever charged until the shopkeeper finalizes and the household approves changed lines, exactly as the existing flow does.

### Inferences (design; no direct sources found)
- **UI:**
  - Group lines as Ready (green), Check (amber: medium confidence or a brand or quantity guess) and Not understood (red: unmatched raw text shown verbatim).
  - Show the original phrase under each line ("2 paav tamatar").
  - Offer one-tap alternatives.
  - Ask the household a question only when preference is "ask me" or the line is red. For "shop chooses", the shop decides. Clarifying questions reuse the existing changed-line approval flow.
- **Audit:**
  - An `ai_proposals` table stores model id, prompt version, input hash, raw input reference, proposed lines JSON, latency and tokens.
  - Each finalized order line links to its proposal line with an action of accepted, edited, replaced, deleted or added.
  - That table doubles as eval data.
- **Eval:**
  - Collect about 200–500 real anonymised orders, stratified by input mode (voice, photo, text) and language (Hindi, Hinglish, English).
  - Label gold lines.
  - Metrics: item precision/recall/F1 at the catalogue-id level, quantity exact match after unit normalization, option accuracy where pinned, and calibration (accuracy per confidence bucket).
  - Ongoing signal: the shopkeeper edit rate.
  - Run it as a CI regression on each prompt or model change through the Batch API, at 50% off.
- **Latency/offline:**
  - Haiku single-call extraction is expected to take a few seconds. This was not measured.
  - The PWA queues the raw order offline and submits it on reconnect. The server processes it asynchronously and the shop sees the draft when ready. The raw text is always kept as a fallback.
- **Safety:**
  - Wrap customer text and OCR output in delimited untrusted tags.
  - The model has no write tools besides `propose_order`. Output IDs are validated against the catalogue, and prices are always taken from the DB, never from the model.
  - Instructions inside a photo or note ("make everything free") are ignored by construction, because the model cannot set price or payment.
  - Strip phone numbers and addresses before logging.
  - Rate limit per household and per shop (for example, N AI parses per hour) and cap image size and count.
  - Use zero-data-retention options where available; structured outputs is ZDR eligible according to the [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) page.

### Gaps
- No published studies on confidence UI for shopkeeper review, or on LLM extraction evaluation for Indic grocery text, were found in this pass.
- Hindi speech-to-text choice (for example, Sarvam vs Whisper) was not evaluated.
