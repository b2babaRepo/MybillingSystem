# AI and AI agents for a small Indian retail medical shop (2026): use cases, evidence, limits

Note on method: 8 web searches (search snippets, not full-page fetches). Many sources are vendor blogs or preprints and are flagged as such. Nothing below is legal advice.

## Prescription reading (handwritten Rx OCR/LLM): accuracy, risks, legal position

### Takeaway
The best evidence on Indian handwritten prescriptions puts LLM extraction at about 82% when name and dosage both have to be right. Older systems reached 64–74%. That is far too low to dispense from without checks. AI should draft the Rx and a registered pharmacist should confirm every line. Indian law ties Schedule H/H1/X sales to an RMP prescription and a licensed premises, and it bars substituting another preparation for a prescribed scheduled drug.

### Cited Findings
- MIRAGE: fine-tuned multimodal LLMs on handwritten Indian prescriptions, with annotations from 1,133 doctors. It reached 82% on medication name plus dosage, and a medication counted as wrong if either was wrong. Earlier systems reached about 64–70%, which the paper calls impractical. — [arXiv 2410.09729](https://arxiv.org/html/2410.09729v2)
- A lexicon-driven approach reached 74.13%, limited by a 520-word lexicon. — [arXiv 2410.09729](https://arxiv.org/html/2410.09729v1)
- Other deep-learning work on extracting medicine from handwritten Rx: [arXiv 2412.18199](https://arxiv.org/html/2412.18199v1); weakly supervised extraction from "inscrutable" handwritten documents: [arXiv 2306.06823](https://arxiv.org/pdf/2306.06823)
- Vendor claim: Veryfi says its prescription API is 99%+ accurate. No methodology is published and the claim likely covers printed formats. — [Veryfi](https://www.veryfi.com/technology/turn-unstructured-medical-prescriptions-into-structured-data/)
- An informal test on one simulated prescription: GPT-4o read it perfectly. The sample is too small to mean much. — [CodeSOTA](https://codesota.com/ocr/best-for-handwriting)
- A ChatGPT study on Thai prescription text (not handwriting) found it tended to leave unknown abbreviations unexpanded rather than invent them. — [PromptLayer summary](https://www.promptlayer.com/research-papers/zero-and-few-shot-named-entity-recognition-and-text-expansion-in-medication-prescriptions-using-chatgpt)
- Schedule H, H1 and X drugs may be sold only on an RMP prescription and from licensed premises. The 2018 draft e-pharmacy rules require a registered pharmacist to verify every prescription. — [Wikipedia: Online pharmacy laws in India](https://en.wikipedia.org/wiki/Online_pharmacy_laws_in_India); [Mondaq](https://mondaq.com/india/food-and-drugs-law/747792/centre-proposes-draft-e-pharmacy-rules-to-regulate-online-sale-of-drugs)
- The draft e-pharmacy rules (Aug 2018, Part VI-B) were never notified. — [Lakshmikumaran & Sridharan](https://cn.lakshmisri.com/insights/articles/diagnosing-the-issues-of-e-pharmacies); [LiveLaw](https://www.livelaw.in/draft-rules-on-e-pharmacies-not-the-right-prescription/)
- Rule 65(11A) bars a dispenser from supplying a different preparation in place of a prescribed scheduled drug. DTAB's recommendation on generic substitution was to be applied only in Jan Aushadhi Kendras at first. — [Medical Dialogues](https://business.medicaldialogues.in/coming-soon-pharmacists-may-be-allowed-to-substitute-branded-drugs-with-generics)

### Inferences
- About 1 in 5 medications misread is a patient-safety risk. Look-alike drug names (sound-alike brands) are the main danger. Product rule: the AI output is a draft. Show the Rx image beside each line, put a confidence flag on each line, and require the pharmacist to tick every line, especially for H/H1 items.
- Matching candidates against the shop's own stock and a brand dictionary cuts the search space and likely improves accuracy. It can also produce a confident wrong match, so keep the image visible.
- No Indian statute I found addresses AI dispensing advice directly. Existing law puts the responsibility on the registered pharmacist and the license holder, so the AI must never be the decision-maker.

### Gaps
- No 2025–26 Indian regulation or CDSCO guidance specific to AI found. Current status of e-pharmacy rules in 2026 not confirmed.
- No independent benchmark of frontier models (Claude, GPT, Gemini, 2025–26) on Indian handwritten Rx found.

## Purchase bill / distributor invoice photo to stock entry

### Takeaway
This is the highest-value and lowest-risk AI use. Invoices are printed and structured, errors can be reviewed before posting, and they do not reach patients directly. Generic OCR tends to drop pharma columns such as batch, expiry and pack. Incumbents like Marg already advertise invoice OCR.

### Cited Findings
- Generic invoice OCR dropped exactly the columns that matter. Batch, mfg/expiry dates and pack size must be extracted explicitly. — [invoicedataextraction.com](https://invoicedataextraction.com/blog/india-pharma-distributor-invoice-batch-expiry-extraction)
- Marg ERP and MedLens read invoices and extract item, batch, expiry, MRP and GST through OCR. Marg is desktop-first and licence-priced. — [invoicedataextraction.com](https://invoicedataextraction.com/blog/extract-indian-pharma-stockist-invoices-to-excel)
- Batch, expiry, HSN, qty, rate and tax split appear on pharma GST invoices. The source cites Rule 46, which I have not verified. — [invoicedataextraction.com](https://invoicedataextraction.com/blog/india-pharma-distributor-invoice-batch-expiry-extraction)
- Commmerce, Pharmixa and Vyapar advertise supplier-invoice scanning. Commmerce also advertises FEFO and expiry alerts. — [Commmerce](https://commmerce.com/pharmacy); [Vyapar](https://vyaparapp.in/free/billing-software-for-retail-shop/pharmacy)
- Best practice is to capture batch and expiry at purchase entry. — [myBillBook](https://mybillbook.in/blog/medical-invoicing-software-with-batch-tracking/)

### Inferences
- Build it as a vision LLM with tool use that outputs a strict JSON schema: item, pack, batch, expiry, MRP, PTR, qty, free/scheme qty, discount, GST%, HSN. Then run validation tools. Check that line totals sum to the invoice total, that MRP ≥ PTR, that expiry is in the future, and that GST% matches HSN. Match each item to the item master with fuzzy matching. Show a review screen before posting.
- Learn mappings for each distributor, for example "this distributor's item name maps to this SKU", so repeat bills get faster.

### Gaps
- No independent accuracy benchmarks for pharma-invoice extraction found.

## Demand forecasting, auto reorder drafts (WhatsApp), expiry risk, dead stock

### Takeaway
There is credible evidence that AI inventory tools raise fill rate (Plazza). For a single small shop, simple statistical reorder points with an LLM to draft orders are enough. WhatsApp order drafting to distributors is useful, but a human should approve each send.

### Cited Findings
- Plazza (Bengaluru) raised a $15M Series A led by Accel, Elevation and Nexus. It runs neighbourhood pharmacies with AI inventory and reports fill rates above 95% against an industry norm of 50–60%. This is the company's own claim. — [AI Market Watch](https://www.ai-market-watch.com/news/ai-pharmacy-startup-plazza-raises-15m-to-solve-indias-medicine-fill-rate-crisis-p1vb1z)
- eVitalRx lists reorder management, PO management, WhatsApp integration, WhatsApp invoices and automatic refill reminders. I found no evidence of AI forecasting. A reviewer mentions distributor bots in its marketplace. — [Capterra India](https://www.capterra.in/software/1019543/evitalrx); [Software Advice](https://www.softwareadvice.com/medical/evitalrx-profile)
- Vendor material describes forecasting from historical sales, seasonality and local demand. Profiter (Italy) offers a predictive replenishment order. — [bestpractice.ai](https://bestpractice.ai/ai-use-cases/healthcare/supply-chain/deploy-ai-for-predictive-inventory-management-in-pharmaceutical-retail); [SMAU/Profiter](https://www.smau.it/san-francisco/2023/partners/profiter-srl)
- HCLTech case study on AI-driven pharmacy supply chains (enterprise scale). — [HCLTech](https://www.hcltech.com/ja-jp/case-study/ai-driven-supply-chains-keep-pharmacies-stocked)

### Inferences
- Use deterministic code for reorder points (moving average × lead time + safety stock, adjusted for season such as monsoon or flu). Use the LLM only to explain the numbers and draft the WhatsApp message to each distributor in their format.
- Expiry risk: flag a batch when days to expiry is less than the days needed to sell current stock at its sales velocity. Suggest a return to the distributor before the return-window cutoff. Rules work here; ML is not needed.

### Gaps
- No public data on Pharmarack's AI or WhatsApp ordering features. No independent study of forecasting ROI for small Indian chemists.

## Substitutes by salt, drug-interaction warnings, data sources, liability

### Takeaway
General LLMs are unreliable at checking interactions. In one study ChatGPT scored 30% on DDI questions, and models missed common pairs. Use a licensed, curated interaction database and have the LLM only present its results. Same-salt substitute lists are feasible from composition data, but substituting a prescribed scheduled drug is legally restricted (Rule 65(11A)). India has no official open drug API.

### Cited Findings
- ChatGPT scored 30% on drug-drug interaction questions from community pharmacists, its lowest category. — [Pharmacia 2024 via DOAJ](https://doaj.org/article/40281e027505426fa2a54f73a5fd78a2)
- All the LLMs tested caught warfarin interactions but missed metoprolol–verapamil. — [Exploratory Research in Clinical and Social Pharmacy 2024](https://doaj.org/article/e4f6c91c40cc4cddb8cf5736ea04cab5)
- ChatGPT, Gemini and Claude gave inconsistent answers on detecting DDIs. — [Ther Adv Drug Saf 2025](https://journals.sagepub.com/doi/reader/10.1177/20420986251339358)
- DDI benchmark against LexiDrug labels: accuracy was higher for severe interactions, and results varied by model and task format. — [medRxiv / PMC12772686](https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12772686/)
- A vendor-linked preprint reports a healthcare-specific model at 93.6% against 85.9% for pharmacist review, with ChatGPT lower. It needs replication. — [JMIR preprint](https://preprints.jmir.org/preprint/104063)
- Prescripsy (Coimbatore, 2025, a student founder) builds DDI checks before dispensing. — [YourStory](https://yourstory.com/companies/prescripsy)
- Community datasets: Kaggle "Indian Medicine Data" (about 195k rows, with salt_composition) and the "A-Z Medicine Dataset of India" (250k+ entries, with composition and price). Licenses are unconfirmed. The AIKosh Indian Drug MCQA dataset is MIT-licensed. — [Baselight/Kaggle](https://baselight.app/u/kaggle/dataset/mohneesh7_indian_medicine_data); [Baselight/Kaggle A-Z](https://baselight.app/u/kaggle/dataset/shudhanshusingh_az_medicine_dataset_of_india); [AIKosh](https://aikosh.indiaai.gov.in/home/datasets/details/indian_drug_mcqa.html)
- The Health Ministry once planned a comprehensive national drug database. It is undated and no API is mentioned. — [RAPS](https://connect.raps.org:443/discussion/india-plans-comprehensive-drug-database?hlmlt=VT)

### Inferences
- Substitutes: offer them only for OTC items or when the doctor or patient asks. Match on exact salt, strength and dosage form. Never allow substitution silently. Label suggestions "for pharmacist decision" and log who accepted each one.
- Interactions: use a licensed source as the engine, such as a commercial Indian drug DB or a Western DB mapped to Indian salts through composition. Indian brands do not map directly to RxNorm, so a salt-level mapping layer is needed. The LLM explains results but does not decide them.
- Liability for a missed interaction sits with the pharmacist or license holder. The app's terms should present the feature as decision support.

### Gaps
- No official CDSCO API or open license found. Commercial DB pricing (e.g. CIMS India, Medikabazaar data) not researched.

## Refill reminders, WhatsApp bots, Hindi voice assistant, GST prep, H1 register

### Takeaway
These are low-risk, high-adoption features that run on structured shop data: reminders, an order intake bot, voice Q&A over the shop's own numbers, GST reconciliation, and H1 register drafting. A WhatsApp bot must hand over to a human before confirming any prescription drug. The H1 register can be filled automatically from sales records, but the pharmacist must verify it.

### Cited Findings
- eVitalRx already offers automatic refill reminders and WhatsApp invoices. — [Software Advice](https://www.softwareadvice.com/medical/evitalrx-profile)
- Digi-Prex (a YC-backed online pharmacy for chronic patients, $5.5M seed, round date unconfirmed) takes prescriptions over WhatsApp and delivers. — [KrASIA](https://www.kr-asia.com/indian-online-pharmacy-digi-prex-secures-usd-5-5-million-from-y-combinator-and-others)
- Hackathon prototype Aushadhi Vault: Rx scanning for Indian medical stores using Llama 3.2 Vision. It is not a product. — [lablab.ai](https://lablab.ai/ai-hackathons/amd-developer/wtf-ninja/aushadhi-vault)

### Inferences
- Voice ("aaj ka profit kitna"): use speech-to-text, then an LLM with read-only tools such as get_sales(date) and get_profit(range). Answers must come from tool results, never from model guesses. This is the same pattern as the existing Grocery Khata app.
- GST: deterministic GSTR-1 and GSTR-3B summaries plus anomaly flags, such as an HSN/rate mismatch, a purchase GSTIN missing from 2B, or a sudden drop in margin. The LLM only explains the flags.
- H1 register: generate it from H1-flagged sale lines (patient name, doctor, drug, qty). Store a link to the prescription image and require a daily sign-off.
- Refill reminders send health-related data over WhatsApp, so they need DPDP consent (see next section).

### Gaps
- No specific Indian Hindi voice-assistant product for chemists found. No evidence found on WhatsApp Business API policy for pharmacy or medicine messages (Meta's commerce policy restricts selling prescription drugs; needs checking).

## Companies doing AI for pharmacies (India and global)

### Takeaway
In India, Plazza is the most credible funded example (AI inventory, claimed fill rate above 95%). Software incumbents (Marg, eVitalRx) are adding OCR and WhatsApp features. Rx-safety startups are early-stage.

### Cited Findings
- Plazza: $15M Series A, fill rate claimed above 95%. — [AI Market Watch](https://www.ai-market-watch.com/news/ai-pharmacy-startup-plazza-raises-15m-to-solve-indias-medicine-fill-rate-crisis-p1vb1z)
- Marg and MedLens invoice OCR. — [invoicedataextraction.com](https://invoicedataextraction.com/blog/extract-indian-pharma-stockist-invoices-to-excel)
- Prescripsy (DDI checks), Digi-Prex (WhatsApp Rx). — [YourStory](https://yourstory.com/companies/prescripsy); [KrASIA](https://www.kr-asia.com/indian-online-pharmacy-digi-prex-secures-usd-5-5-million-from-y-combinator-and-others)
- Google is working on reading doctors' handwritten prescriptions (2022 announcement). — [Fox News](https://noticias.foxnews.com/tech/google-using-tech-read-your-doctors-handwritten-prescriptions.amp)
- Netmeds digitises handwritten prescriptions and routes them to partner pharmacies. — [GrowthJockey](https://growthjockey.com/blogs/healthcare-startups-in-india)

### Gaps
- Independently verified outcome numbers are scarce. Global vendors (e.g. US pharmacy AI) were not researched in depth.

## Guardrails: DPDP, human-in-the-loop, no auto-dispense, audit logs, cost

### Takeaway
The DPDP Rules were notified on 13 Nov 2025 with phased enforcement. Prescriptions and medical histories count as personal data that needs free, specific, informed consent that can be withdrawn easily. The architecture should be: AI drafts, a human confirms, every action is logged, and nothing reaches patients without pharmacist approval.

### Cited Findings
- DPDP Rules notified by MeitY on 13 Nov 2025. — [TheLeaflet](https://theleaflet.in/amp/story/explainer/indias-new-digital-personal-data-protection-rules-2025-a-detailed-reading); [enforcement timeline](https://dpdpa.com/dpdpa_enforcement_timeline.html)
- No separate health-data category. Prescriptions, diagnoses and lab results fall under the general rules. Consent must be explicit and informed; blanket or implied consent is invalid. — [AMLegals](https://amlegals.com/health-data-and-the-dpdp-act-a-practical-guide/); [KPMG Dec 2025](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/12/the-privacy-prescription-impact-of-dpdp-act-and-rules-in-healthcare-and-life-sciences-sector.pdf.coredownload.pdf)
- Withdrawing consent must be as easy as giving it. Commentary cites exemptions for medical emergencies. — [Legal500/Saga Legal](https://www.legal500.com/developments/thought-leadership/impact-of-dpdp-act-and-rules-on-pharmaceutical-companies/)

### Inferences
- Design rules:
  1. Never auto-dispense or auto-confirm an Rx order. Every Rx-derived line needs a pharmacist tick.
  2. The LLM gets narrow tools such as read stock and create draft entries. It gets no tools that send messages or post bills without a UI confirmation.
  3. Keep an audit log of the model output, the human edits, the approver, and timestamps, with the image hash. This also helps for drug inspector visits.
  4. Redact or minimise PII sent to the LLM. Get a DPA from the provider and use zero-retention options. Record per-purpose consent (billing vs reminders vs marketing).
  5. Use deterministic engines for money, tax, interactions and expiry. The LLM handles extraction, explanation and drafting.
- Cost: one invoice or Rx image extraction is roughly one vision call per page. Exact per-call costs were not researched (check current Claude pricing through the claude-api reference). Batch processing, prompt caching of the item-master and system prompt, and routing to a smaller model for simple tasks (voice Q&A, message drafts) all cut cost. Reserve the larger model for handwritten Rx.

### Ranked use cases (synthesis)
1. Distributor invoice to stock entry (high value, low risk)
2. Expiry-risk alerts, return planning and dead stock (rules plus LLM explanation)
3. Reorder drafts sent to distributors on WhatsApp after human approval
4. Hindi voice and chat Q&A over shop data (read-only tools)
5. GST prep and anomaly flags; H1 register auto-draft
6. Refill reminders and a WhatsApp order-intake bot (needs consent and human handover)
7. Substitute suggestions by salt (OTC, pharmacist decides)
8. Handwritten Rx reading (draft only, mandatory verification); DDI warnings only from a licensed DB

### Gaps
- Final DPDP enforcement dates for each obligation not verified from the official gazette. No case law on AI in pharmacy found. Per-call LLM pricing not gathered.
