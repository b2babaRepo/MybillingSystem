# AI order agent for kirana: channels, real-world examples, legal/privacy (India, 2025–2026)

Researched 2026-10-28. Limited budget (about 15 searches/fetches). Most claims come from search-result summaries; only Meta's pricing doc was fetched in full. Older data is labelled with its year.

## What concrete products already do voice/photo ordering for kirana in India, and what results or problems are reported?

### Takeaway
Voice and list-photo ordering exist, but mostly inside big platforms (Swiggy Instamart's "Say it / Scan it / Write it" list, Flipkart Wholesale's WhatsApp voice bot built on Sarvam AI, Swiggy plus Sarvam in 11 languages) or as announced and partly shipped features on ONDC buyer apps (Saarthi, Kiko Live, Gupshup, KiranaPro). I found no reported adoption numbers or accuracy figures for any of them. The best-documented kirana-on-WhatsApp precedent, JioMart (2020), sent orders over WhatsApp to a partner kirana for fulfilment, but it was a catalogue link, not free-form voice. Khatabook, OkCredit and Dukaan have no confirmed AI voice-ordering features.

### Cited Findings
- **JioMart on WhatsApp (2020 pilot, old):** the customer messages a JioMart number and gets a link to a catalogue. JioMart passes the order to a nearby partner kirana, which bills it and hands it over for pickup. Payment was cash only. The pilot started in Navi Mumbai, Thane and Kalyan — [The Next Web, Apr 2020](https://thenextweb.com/facebook/2020/04/27/facebook-backed-reliance-jio-pilots-basic-grocery-ordering-service-on-whatsapp-in-india/); [MediaNama 2020](https://www.medianama.com/2020/04/223-jiomart-whatsap-kirana-orders-mumbai-suburbs/); [Business Today 2020](https://www.businesstoday.in/latest/corporate/story/jiomart-whatsapp-grocery-service-to-reinvent-kiranas-256563-2020-04-27)
- The JioMart pitch was to connect about 3 crore kiranas. That was a projection, and I found no reported adoption outcome — [Voice&Data](https://www.voicendata.com/digital-transformation-opportunity-3-crore-kirana-stores-jio-facebook-deal-gives-fillip)
- **Swiggy Instamart:** a shopping-list feature was piloted in October 2024. It offers "Say it", "Scan it" (photo of a handwritten list) and "Write it", and builds the cart automatically. It was prompted by a viral social post — [YourStory, Oct 2024](https://cwv.yourstory.com/2024/10/swiggy-instamart-pilots-new-shopping-list-feature-for-hassle-free-cart-building); [afaqs](https://www.afaqs.com/news/mktg/swiggy-instamart-launches-upload-your-shopping-list-feature-7367790)
- Swiggy partnered with Sarvam AI (reported March 2026) for voice ordering in 11 languages (Hindi, Kannada, Marathi, Tamil, Telugu and others) across Food, Dineout and Instamart — [Verdict Foodservice](https://www.verdictfoodservice.com/news/swiggy-sarvam-ai-to-launch/); [CXO Digital Pulse](https://www.cxodigitalpulse.com/?p=49085)
- Swiggy also lets users order through ChatGPT, Claude and Gemini — [Social Samosa](https://www.socialsamosa.com/industry-updates/swiggy-enables-orders-through-chatgpt-claude-and-gemini-11034493)
- **Flipkart Wholesale (B2B, retailers ordering stock):** a voice AI assistant on WhatsApp, built on Sarvam AI, lets small retailers browse, check stock and discounts, and order in regional languages. The stated motivation is that wholesale ordering has meant phone calls and delays. No launch date was found, and this is single-source — [MIT Sloan ME](https://www.mitsloanme.com/article/flipkart-brings-voice-led-wholesale-ordering-to-whatsapp-with-sarvam-ai/)
- Flipkart Wholesale added Hindi and English voice search in January 2022 (old) because of kiranas' varying English proficiency — [Voicebot.ai 2022](https://voicebot.ai/2022/02/01/flipkart-adds-hindi-and-english-voice-search-to-wholesale-e-commerce/)
- **ONDC Saarthi:** a reference buyer app from ONDC, powered by Bhashini and voice-first. It supports Hindi, English, Marathi, Bengali and Tamil, with a plan to reach 22 languages — [Daily Jagran](https://www.thedailyjagran.com/india/ondc-introduces-saarthi-multilingual-reference-app-powered-by-bhashini-with-voice-first-approach-10187060); [Inc42](https://inc42.com/?p=477840)
- **Gupshup:** a conversational ONDC buyer app on WhatsApp, in beta and limited to food and beverages. Voice was planned, not live (2024/25) — [CXO Digital Pulse](https://www.cxodigitalpulse.com/gupshup-debuts-indias-first-conversational-buyer-app-on-ondc-network/)
- **Kiko Live:** an ONDC buyer app focused on kiranas, with more than 5 lakh kirana orders reported. Voice search was announced as a future feature — [CXO Today](https://cxotoday.com/media-coverage/kiko-live-crosses-half-million-milestone-orders-for-kirana-stores-on-ondc/); [Retail4Growth](https://retail4growth.com/news/kiko-live-launches-ondc-buyer-app-in-india-7100)
- **KiranaPro:** kirana quick commerce on ONDC, with orders placed through a voice agent — [Tech Geography substack](https://techgeography.substack.com/p/kiranapro-challenging-quick-commerce). In 2025 it reportedly suffered a destructive infrastructure wipe attributed to an ex-employee with full privileges. This comes from one informal, unverified source — [Think Tank newsletter](https://think-tank-55e4a2.beehiiv.com/p/the-kiranapro-collapse-a-hard-lesson-in-startup-infra)
- **Khatabook, OkCredit, Dukaan:** no official 2025 AI voice-ordering feature was found. OkCredit offers Hindi/Hinglish ledgers and WhatsApp/SMS payment reminders. Voice "khata" tools exist only as hackathon or open-source projects (Dukaan Sathi, LikhLo AI) — [OkCredit](https://okcredit.in/khata-app-for-kirana-store); [Dukaan Sathi hackathon](https://build-small-hackathon-dukaan-saathi.hf.space/blog)

### Inferences
- The pattern is proven at large-platform scale: speak or photograph a list, the AI builds a cart, the user reviews it. Nobody visible is doing it for one independent kirana with that shop's own catalogue and khata, which is Grocery Khata's gap.
- Sarvam AI (an Indian provider) is the voice engine of choice for Swiggy and Flipkart. It may also reduce cross-border data concerns.
- KiranaPro's reported incident is a reminder that operational security (least privilege, backups) is a bigger near-term risk for a small team than model accuracy.

### Gaps
- No public accuracy, error-rate or adoption metrics for any voice or photo list feature (Instamart, Flipkart, KiranaPro).
- Current (2025–26) status of JioMart's WhatsApp ordering.
- Zepto and Blinkit voice or photo features: none found.
- Meta's own "WhatsApp catalogue / cart" ordering for small businesses was not researched in depth.
- International examples (e.g. Instacart's GPT-4V list-photo preview) were not researched further.

## What does the WhatsApp Business Platform cost and require for receiving customer voice notes and photos in India?

### Takeaway
Receiving voice notes and photos is free, because Meta does not charge for user-to-business messages. Costs come from what the business sends. Since 1 July 2025 pricing is per message. Free-form replies are allowed only inside the 24-hour customer service window. From 1 October 2026, service messages beyond 1,000 a month per number and utility messages inside the window became chargeable. India accounts must move to INR billing by 31 December 2026. From 15 January 2026 general-purpose AI assistants are banned, but a task-specific ordering bot is allowed.

### Cited Findings
- Per-message pricing has applied since 1 July 2025, by category (marketing, utility, authentication, service) and recipient country. Messages from users to the business are not charged — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- The customer service window opens when a user messages and resets to 24 hours with each new user message. Non-template (free-form) messages are allowed only while it is open. Otherwise only approved templates can be sent — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- **Service messages:** free since 1 November 2024, but charging resumes 1 October 2026, with 1,000 free service messages per month per business number. Numbers with no payment method by 30 September 2026 may have service messages stop being delivered — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- **Utility templates:** free inside an open window until 30 September 2026, and charged from 1 October 2026 — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- A free entry point window of up to 7 days applies only to chats started from Click-to-WhatsApp ads — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- **India-specific changes:** the marketing rate rose on 1 January 2026. INR billing localisation started 1 January 2026, accounts must migrate by 31 December 2026, and non-INR accounts stop delivering from 1 January 2027 — [Meta pricing docs](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing.md)
- **Indicative India rates (third party, internally inconsistent):** marketing about ₹0.86, utility about ₹0.13, authentication about ₹0.13 per message. Utility rates fall with volume tiers that reset monthly — [MyOperator KB](https://support.myoperator.com/portal/en/kb/articles/whatsapp-has-shifted-to-per-message-pricing-effective-july-1-2025-based-on-message-categories-and-your-recipient-s-country-in-india-there-are-three-paid-categories); [Zoho community](https://help.zoho.com/portal/en/community/topic/whatsapp-message-pricing-changes-effective-july-1-2025)
- **AI policy:** from 15 January 2026, AI model providers may not distribute general-purpose assistants through the Business API (ChatGPT, Perplexity and others were removed). Bots for support, sales, notifications and updates remain allowed — [TechCrunch Oct 2025](https://techcrunch.com/2025/10/18/whatssapp-changes-its-terms-to-bar-general-purpose-chatbots-from-its-platform); [respond.io](https://respond.io/blog/whatsapp-chatbot-policy-2026); [Business Standard](https://www.business-standard.com/technology/tech-news/meta-bans-ai-chatbots-from-whatsapp-business-api-chatgpt-to-shut-down-jan-2026-125102200335_1.html)
- WhatsApp is the preferred channel for hearing from businesses in India (45%) — [MEF](https://mobileecosystemforum.com/mef_data_post/whatsapp-in-india)

### Inferences
- **Rough cost model:** a customer sends a voice note (free). The bot replies with a draft order and asks for confirmation inside the window: a service message, free within the 1,000 a month per number allowance and charged after that from October 2026. A "your order is ready" notification sent later, outside the window, is a utility template (about ₹0.1–0.2). For a single shop with a few hundred orders a month, Meta fees are likely tens to low hundreds of rupees a month. This is an estimate; verify against Meta's INR rate card.
- **Feasibility for a small app:** a Meta Business account, a verified business, a dedicated phone number, a webhook endpoint (HTTPS) and template approval are all needed. Each shop would either need its own number, which means onboarding every kirana as a WhatsApp Business Account through Embedded Signup or a solution partner, or share one Grocery Khata number that routes orders to shops. One shared number is simpler for a pilot.
- **Media handling (from general knowledge of the Cloud API, not re-verified this session):** an incoming voice note or image arrives as a media ID in the webhook. The app calls the Graph API to get a short-lived URL, then downloads with its access token. Voice notes are typically OGG/Opus. Store minimally, and delete after transcription (see the DPDP section).
- **In-app PWA alternative:** no Meta fees, no dependency on Meta policy, and full control over consent screens. But the household must open the app, and browser MediaRecorder and camera support varies (iOS Safari codecs). A hybrid works: the PWA first, with WhatsApp as a later channel.
- The Grocery Khata bot is task-specific (ordering for one shop), so it should fall within the allowed category of the January 2026 ban. It must not be marketed as a general assistant.

### Gaps
- Exact current INR rate card (Meta's CSV was not fetched).
- Exact October 2026 service-message rate for India.
- Media download URL expiry and size limits, and on-premise vs Cloud API details, were not re-verified.

## How do Indian households prefer to order from kirana shops today, and what do they need from AI?

### Takeaway
Kiranas remain dominant: 86% of Indians prefer local kiranas versus 2% using apps (Axis My India, 2022, old). Ordering by WhatsApp message to the local store became common during COVID. I found no clean current statistic on phone versus WhatsApp-voice versus paper-list ordering, nor on the gender of the person managing the order. Product evidence suggests that voice and photo lists address literacy and English barriers.

### Cited Findings
- 86% prefer shopping at local kiranas and 2% use online apps. The sample was 10,207 respondents, 70% rural (Nov 2022) — [Business Today](https://www.businesstoday.in/latest/economy/story/online-groceries-no-kirana-stores-are-preferred-by-86-indians-finds-survey-351577-2022-11-02)
- During COVID (2020), households across city tiers ordered essentials through apps, local websites or WhatsApp messages to local stores. In the LocalCircles survey, 40,000+ responses from 303 districts, 65% of respondents were male — summarised via search; the original table was not fetched; [search result source list](https://www.thenewsminute.com/money/45-indian-households-likely-spend-wfh-gadgets-next-3-months-survey-150303)
- Voice search was introduced because "varying English proficiency" makes text input hard (Flipkart Wholesale 2022) — [Voicebot.ai](https://voicebot.ai/2022/02/01/flipkart-adds-hindi-and-english-voice-search-to-wholesale-e-commerce/)
- Instamart's list-scan feature was triggered by users wanting to upload handwritten lists — [YourStory 2024](https://cwv.yourstory.com/2024/10/swiggy-instamart-pilots-new-shopping-list-feature-for-hassle-free-cart-building)

### Inferences
- Mirror existing habits. A voice note in Hinglish or a regional language ("do kilo aata, ek Fortune tel, bachchon wala biscuit") or a photo of a paper list is closest to today's WhatsApp or phone ordering.
- Confirmation is essential. Show the parsed list with pictures, quantities and prices, let the user edit or speak corrections, and send nothing to the shop until the user taps "confirm". Brand and size ambiguity ("tel" = which oil, which pack size?) should be resolved from that household's past purchases at that shop.

### Gaps
- No 2024–26 data found on the share of kirana orders placed by phone, WhatsApp voice or text, or paper list.
- No data found on women's role in household grocery ordering, on trust in AI, or on literacy-segment behaviour. Redseer, Bain/Flipkart "How India Shops Online" and Google-KPMG were not reached within budget.

## What do the DPDP Act and 2025 Rules require for this use case?

### Takeaway
The DPDP Rules were notified on 13/14 November 2025 with phased commencement. The heavy operational obligations (notice, consent, security, retention, children) reportedly apply from about 13 May 2027 (18 months). Voice notes and photos of lists are personal data: a voice is identifiable, and photos may show names, addresses or faces. Grocery Khata is the Data Fiduciary, and the AI vendor (STT, LLM) is its Data Processor. Consent must be free, specific, informed, unconditional and given by a clear affirmative act, with a standalone plain-language notice that itemises the data and purposes. Withdrawal must be as easy as giving consent. Data must be erased once the purpose is served. Children's data needs verifiable parental consent. Cross-border transfer is allowed by default unless the government blacklists a country.

### Cited Findings
- Rules notified 13 November 2025, Gazette 14 November 2025. Some provisions (Rules 3, 5–16, 22, 23) have deferred commencement — [Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified); [EY](https://www.ey.com/content/dam/ey-unified-site/ey-com/en-in/pdf/2025/11/dpdp-act-and-rules.pdf)
- Major operational provisions, including the cross-border framework, are expected to commence 13 May 2027 (18-month phase) — [AMS Shardul alert](https://www.amsshardul.com/wp-content/uploads/2025/11/Regulatory-Alert-Enforcement-of-DPDP-Act-and-Notification-of-DPDP-Rules.pdf); [AZB](https://www.azbpartners.com/bank/update-indias-digital-personal-data-protection-framework-comes-into-effect/)
- **Notice (Rule 3):** it must be understandable on its own, in clear and plain language, with an itemised description of the personal data and the purposes — [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/); [Lexplosion](https://lexplosion.in/summary-of-the-digital-personal-data-protection-rules-2025/)
- **Consent:** free, specific, informed, unconditional and by a clear affirmative act. Withdrawal must be as easy as giving consent — [EY insights](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025)
- **Retention and erasure (Rule 8):** erase when the purpose is fulfilled or the user is inactive, unless law requires retention. The Third Schedule sets a 3-year inactivity period for large e-commerce, social media and gaming entities. Users get 48 hours' notice before erasure. Processing logs must be kept at least 1 year — [KPMG](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/11/dpdp-rules-2025-guidance-to-dpdp-act-implementation.pdf); [Bitraser](https://www.bitraser.com/article/dpdp-rules-2025.php)
- **Children (Rule 10):** verifiable parental consent is required before processing a child's (under 18) data. Commentary on the drafts noted that DigiLocker-style verification was suggested without a clear procedure. Exemption categories exist — [Bar & Bench](https://www.barandbench.com/view-point/childs-personal-data-and-privacy-analysing-the-draft-dpdp-rules-2025); [dpdpa.com Rule 10](https://dpdpa.com/dpdparules/rule10.html)
- **Cross-border transfer (Section 16, Rule 15):** transfer is free to any country except those restricted by government notification. Sectoral laws with stricter rules, such as RBI localisation, still apply. Section 16 and Rule 15 had not commenced as of September 2026 — [KS&K](https://ksandk.com/data-protection-and-data-privacy/cross-border-data-transfers-dpdp-act-india/)

### Inferences
- **Consent screens:** before the first recording or photo, show a short, standalone notice in the user's language (Hindi or English at minimum). It should say what is collected (voice recording, list photo, transcription, order history), why (to prepare the order for the chosen shop), who processes it (the named AI provider, possibly outside India), how long it is kept (for example, audio and photo deleted after the order is confirmed or within N days; the text order is kept as part of khata records), and how to withdraw and delete (one tap in settings, plus a contact for grievances).
- Use a separate opt-in for any secondary use, such as using recordings to improve models. Purpose limitation means it must not be bundled.
- Prefer AI vendors with zero data retention or no-training terms, and with Indian or nearby regions where possible. Sarvam and Bhashini are domestic options. Foreign providers are legal for now, but name them in the notice.
- For children, the app is for adult household managers. Add an age declaration ("I am 18+") and avoid processing children's voices. A child sending a voice order would require parental consent.
- Treat the shopkeeper as a recipient of order data, not of raw audio. Share only the transcribed list unless the customer opts in to the shop hearing the voice note.
- Penalties under the Act are up to ₹250 crore for security failures. This is from the Act's schedule as generally reported and was not fetched this session.

### Gaps
- The official Gazette text was not read directly, so the exact commencement dates per rule need verification.
- Whether a small startup falls under the Third Schedule's 3-year class (that class is for large platforms) is unclear.
- The final Rule 10 verification mechanism was not confirmed.

## How would kirana owners want AI-prepared orders presented, and what is their tolerance for errors?

### Takeaway
I found no direct user research. The proxies are Flipkart Wholesale and OkCredit design choices (vernacular, voice, WhatsApp-native) and the fact that JioMart routed orders to kiranas over WhatsApp. Shopkeepers are WhatsApp- and phone-first and need a quick pick list.

### Cited Findings
- JioMart sent orders to the local kirana over WhatsApp and the store generated the bill (2020) — [The Next Web](https://thenextweb.com/facebook/2020/04/27/facebook-backed-reliance-jio-pilots-basic-grocery-ordering-service-on-whatsapp-in-india/)
- Kirana owners favour Hindi/Hinglish ledgers and WhatsApp reminders — [OkCredit](https://okcredit.in/khata-app-for-kirana-store)
- Flipkart built voice ordering for retailers because complex e-commerce interfaces are hard for them — [MIT Sloan ME](https://www.mitsloanme.com/article/flipkart-brings-voice-led-wholesale-ordering-to-whatsapp-with-sarvam-ai/)

### Inferences
- Present a customer-confirmed list grouped by aisle or shelf, matched to the shop's own products and prices. Mark uncertain items as "check" with the original snippet (a voice clip or photo crop) available on tap. Support out-of-stock substitutions with one tap back to the customer.
- Error tolerance is likely low for wrong items or prices on khata (credit) because of disputes, and higher for "item unclear, call customer". Fail visibly, not silently.

### Gaps
- No published interviews or surveys with kirana owners on AI-prepared orders. Primary research or a pilot is recommended.
