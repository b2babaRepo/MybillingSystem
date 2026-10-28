# Voice input (speech-to-text) for Grocery Khata's AI ordering agent

Scope: turning a spoken household order ("do kilo aata, ek litre sarson ka tel, Parle-G ke paanch packet") into text, using a React PWA and a .NET backend. This was a short research pass (about 9 searches and fetches), so many figures are vendor-reported. Treat them as leads to verify, not settled facts.

## Which services/models handle Hindi and Hinglish grocery speech best (2025–2026), with published WER

### Takeaway
Sarvam's Saaras v3 (and the newer Saaras v4) is the strongest India-specific candidate. It is built for 22 Indian languages, handles code-mixed speech natively, and keeps data in India. The only published comparisons against GPT-4o-transcribe, Gemini, Deepgram and ElevenLabs are Sarvam's own. I found no independent IndicVoices benchmark that covers all of these vendors on the same test.

### Cited Findings
- Saaras v3: trained on more than 1M hours of Indian speech, with one model for 22 scheduled languages plus Indian English and automatic language detection. Handles Hinglish, Tanglish and Benglish natively. Its "Codemix" output mode keeps English words in English inside native script. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text)
- Sarvam reports 19.31% WER on IndicVoices across 10 languages (22% for v2). It claims to beat GPT-4o Transcribe, Gemini 3 Pro, Deepgram Nova-3 and ElevenLabs Scribe v2 on Indian languages, but gives no per-competitor numbers. This is a vendor claim. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text)
- Saarika (the older STT model) is listed at 11.81% Hindi WER on the Vistaar benchmark. Sarvam's docs say Saarika v2.5 is being deprecated and recommend Saaras v4 with mode="transcribe". — [Sarvam Saarika docs](https://docs.sarvam.ai/api-reference-docs/getting-started/models/saarika)
- Sarvam latency: under 150 ms time-to-first-token in Fast mode, median under 250 ms. Streaming runs over WebSocket. REST takes files under 30 s; batch takes files up to 1 hour, with diarization and timestamps. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text)
- ElevenLabs reports 5.5% Hindi WER for Scribe v1, against 19.0% for Deepgram Nova 2. The benchmark is labelled FLEURS on some locale pages and Common Voice on others. These are vendor numbers on read speech, not IndicVoices. — [ElevenLabs Hindi STT](https://elevenlabs.io/speech-to-text/hindi)
- AI4Bharat IndicConformer: open source under MIT, covers the 22 official languages, has a 600M multilingual checkpoint on Hugging Face, and was trained on IndicVoices (7,348 h, 16,237 speakers). A third-party wrapper says the model supports only 14 languages, which conflicts with the 22 claim. — [GitHub](https://github.com/AI4Bharat/IndicConformerASR); [AIKosh](https://aikosh.indiaai.gov.in/home/models/details/aibharat_indicconformer_600m_multi.html); [VEXYL-STT](https://medium.com/@anilmathewm/vexyl-stt-free-self-hosted-indian-language-speech-to-text-server-f2909003aaf6)
- Google Chirp 3 lists Hindi (India) as supported. — [Google Chirp 3 docs](https://cloud.google.com/speech-to-text/v2/docs/chirp_3-model?hl=en)

### Inferences
- Read-speech benchmarks such as FLEURS and Common Voice understate errors in noisy, conversational kitchen audio. IndicVoices, which includes extempore and conversational speech, is closer to real voice orders. That means Scribe's 5.5% and Sarvam's 19% cannot be compared directly.
- For Hinglish grocery orders, the deciding factor is likely how a model handles code-mixing and brand names. Sarvam's Codemix mode and LLM-based models such as gpt-4o-transcribe and Gemini audio input are the closest fits. Whichever model is chosen should be tested on 50–100 real orders recorded in the app.

### Gaps
- No independent head-to-head WER on IndicVoices or Vistaar for Hindi or Hinglish across Sarvam, Google, Azure, OpenAI, Deepgram and ElevenLabs.
- Azure Speech hi-IN accuracy and Deepgram Nova-3 Hindi figures were not researched in this pass.
- Gemini and other LLMs that take audio input directly were not benchmarked here. Sarvam names Gemini 3 Pro only as a competitor.

## Pricing, free tiers and data residency in India

### Takeaway
Sarvam costs roughly ₹0.5–1.5 per minute and processes all audio in India. OpenAI costs about $0.003–0.006 per minute (₹0.25–0.5) but processes audio outside India. Google V2 lists from $0.016 per minute. Self-hosting IndicConformer has no licence cost but needs a GPU.

### Cited Findings
- Sarvam docs: STT ₹30 per hour, billed per second; ₹45 per hour with diarization; ₹1,000 in free credits on every plan. — [Sarvam pricing docs](https://docs.sarvam.ai/api-reference-docs/getting-started/pricing). The marketing page instead says "Rs. 1.5 per minute" (₹90 per hour) on the standard plan. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text). The two figures conflict, so check them before costing.
- Sarvam says all processing happens in India with no cross-border transfers. VPC and on-prem deployment are available, it does not train on API data, and data is deleted after processing by default. It claims DPDP compliance and ISO 27001. The page shows SOC 2 Type II in one place and Type III in another. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text)
- OpenAI gpt-4o-transcribe costs about $0.006 per minute and gpt-4o-mini-transcribe about $0.003 per minute. These come from aggregators, not OpenAI's own page. — [CostGoat](https://costgoat.com/pricing/openai-transcription)
- Google Speech-to-Text V2 / Chirp 3 starts from $0.016 per minute according to aggregators. Another guide lists $0.003 per minute for dynamic batch. — [OpenRouter Chirp 3](https://openrouter.ai/google/chirp-3); [ToolWorthy](https://www.toolworthy.ai/tool/google-cloud-speech-to-text)
- Bhashini reportedly offers free ASR APIs for 22 languages after registration at bhashini.gov.in. This comes from an aggregator page and is unverified. — [schemesinindia](https://schemesinindia.in/central/bhashini-digital-india-language-ai-platform)

### Inferences
- At around 15 s per order and 1,000 orders a day, usage is about 250 minutes a day. That is roughly ₹125–375 a day on Sarvam and less on gpt-4o-mini-transcribe. Speech-to-text cost will be small next to the LLM parsing step.

### Gaps
- Azure, Deepgram and ElevenLabs per-minute prices were not fetched this pass. Google asia-south1 regional availability and pricing were not confirmed. Bhashini rate limits and SLA are unknown.

## Browser recording in a PWA (MediaRecorder, Web Speech API)

### Takeaway
MediaRecorder with webm/opus works on Android Chrome, and most speech-to-text APIs accept that format. The Web Speech API in Chrome sends audio to Google's servers, offers no customisation or vocabulary control, and its on-device mode was announced first for desktop. It is not a reliable base for this feature.

### Cited Findings
- caniuse reports MediaRecorder as supported on Chrome for Android. — [caniuse](https://caniuse.com/mediarecorder)
- WebM/Opus is the usual Chrome recording format, most APIs accept it, and `MediaRecorder.isTypeSupported` checks support. — [lilting.ch guide](https://lilting.ch/en/articles/ai-voice-chat-input-implementation)
- Chrome's Web Speech recognition uses Google servers and does not work offline. — [lilting.ch guide](https://lilting.ch/en/articles/ai-voice-chat-input-implementation)
- On-device Web Speech launched first on Windows, Mac and Linux, with ChromeOS to follow. Android was not named, and language packs are about 60 MB each. — [blink-dev Intent to Ship](https://groups.google.com/a/chromium.org/g/blink-dev/c/VNOok2dbmHM/m/TQpe9shjCgAJ)
- Web Speech API reference. — [MDN](https://developer.mozilla.org/docs/Web/API/Web_Speech_API)

### Inferences
- Opus at about 16–32 kbps works out to roughly 30–60 KB for a 15 s order, which is fine on 2G/3G. This is estimated from codec bitrates, not taken from a source.
- For offline use, keep the recorded blob in IndexedDB and upload it when the connection returns, for example from an online event or Background Sync. Show the customer a "pending" state until it is transcribed.
- On-device ASR for low-end phones, such as Whisper in WASM, is likely too heavy for 2–3 GB RAM devices. — related article: [Fora Soft on browser ASR](https://www.forasoft.com/learn/ai-for-video-engineering/articles-ai/client-side-asr-faster-whisper-wasm-browser) (not read in detail)

### Gaps
- Not confirmed: whether hi-IN Web Speech quality is good, or whether on-device recognition works on Android Chrome. Safari's MediaRecorder output (mp4/aac) was not researched, though this matters less for an Android-first app.

## Errors with numbers, units and brand names, and how to correct them

### Takeaway
Expect errors in spoken numbers ("dhai sau" = 250, "paav" = 250 g, "sawa" = 1.25, "dedh" = 1.5), in Hindi versus Devanagari versus digit output, and in brand names. The most robust fix is an LLM post-pass that maps the transcript onto the shop's own catalogue, rather than relying on keyword boosting in the speech-to-text step.

### Cited Findings
- Sarvam offers output modes that include codemix and translate. Choosing the mode controls whether "Parle-G" or "packet" stays in Latin script. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text); [Saarika docs](https://docs.sarvam.ai/api-reference-docs/getting-started/models/saarika)

### Inferences
- Pass the shop's product names and aliases to the LLM post-pass. It should output structured lines (product id, quantity, unit), normalise Hindi number words and fractions, and mark low-confidence matches for the customer to confirm on screen.
- Where available, also use prompt or keyword biasing in the speech-to-text step. gpt-4o-transcribe accepts a text prompt and Google offers adaptation/phrase sets. This is from my training knowledge and was not verified this pass.
- Always show the parsed cart for confirmation before placing the order. The khata or bill must never be written straight from a raw transcript.

### Gaps
- I found no published error analysis specific to Indian grocery or number speech. Sarvam's inverse text normalisation for numbers (whether "do kilo" becomes "2 kilo") was not documented in the sources read.

## Privacy: storing recordings, consent, retention (DPDP Act 2023 / Rules 2025)

### Takeaway
The DPDP Rules were notified on 13–14 Nov 2025. Consent must be free, specific, informed and given by clear affirmative action, and withdrawing it must be as easy as giving it. Data must be erased once its purpose is served, with 48 hours' notice before erasure in the cases the Rules cover. The simplest compliant design is to delete audio after transcription.

### Cited Findings
- Rules notified 13 Nov 2025 and published in the Gazette on 14 Nov 2025. — [SCC Online](https://www.scconline.com/blog/post/2025/11/14/meity-notified-digital-personal-data-protection-rules-2025/)
- Consent must be free, specific, informed, unconditional and given by clear affirmative action. Withdrawal must be as easy as giving consent. — [KPMG](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/11/dpdp-rules-2025-guidance-to-dpdp-act-implementation.pdf); [Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified)
- Retention periods must be set per data category, and data deleted once its purpose is served. The Third Schedule gives 3 years for certain large e-commerce, social media and gaming entities, with 48 hours' notice before erasure. — [Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified); [EY](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025)
- Sarvam deletes audio after processing by default and keeps it in India. — [Sarvam STT page](https://www.sarvam.ai/speech-to-text)

### Inferences
- Show a one-time consent notice in Hindi and English ("your voice is converted to text; the recording is deleted after X") before the first use of the mic.
- Keep the transcript and parsed order as the record of what was ordered, and delete raw audio within a short window, for example 7–30 days, kept only for dispute review. This fits the app's existing dispute feature.
- Choosing an India-processing vendor such as Sarvam, or Google or Azure India regions, reduces cross-border transfer questions.

### Gaps
- I found nothing on whether voice counts as biometric or receives special treatment under DPDP. A legal review is advisable.

---

**Recommended approach for Grocery Khata (synthesis):**
1. Record in the PWA with MediaRecorder as webm/opus, using a press-and-hold button, about 30 s max, mono. Queue recordings in IndexedDB when offline and upload when back online.
2. On the .NET backend, transcribe with Sarvam Saaras (v3 or v4, codemix mode, REST since orders are under 30 s). Data stays in India, the price is low, and it is the strongest on Indic code-mixed speech by its own claims. Keep gpt-4o-mini-transcribe or Google Chirp 3 as a fallback behind an interface, and A/B test them on about 100 real recorded orders.
3. Run an LLM post-pass with the shop's catalogue, aliases and units as context. It should output structured cart lines and normalise Hindi numbers and units (paav, dedh, dhai sau gram).
4. Customer confirms the cart on screen. Low-confidence items are highlighted.
5. Delete audio after a short retention window, show a DPDP consent notice on first mic use, and keep the transcript with the order.
6. Do not rely on the Web Speech API: it sends audio to Google's servers, offers no vocabulary control, and on-device support on Android is unconfirmed. Self-hosting IndicConformer is an option later if costs grow.
