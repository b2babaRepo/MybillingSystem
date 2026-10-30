# Charge shops lightly, free customers, gate agents

Grocery Khata should sign shops up with **phone OTP, owner name, shop name, shop type and state**. It should ask for GSTIN, legal name and billing address only when a shop wants a B2B invoice. Card, UPI and bank details should stay with the payment gateway. Subscriptions should be modelled as **plans that map to entitlements (feature flags and limits) plus dated per-shop overrides**. Under that model, a monthly, yearly or multi-year term, a new-member offer, a complimentary grant or an admin hold is just a different record, not different code. The Indian market buys **1-year and 3-year prepaid terms**, with 3-year priced near **2.25x annual**. Monthly plans only make sense on UPI AutoPay. I found no precedent for 5-year plans, and they carry GST-upfront and refund liabilities, so 5-year should be deferred or sold as an annual-billed price lock. Customers of the shops are never charged. They keep read-only access to their balance and can pay by UPI even when their shop lapses or is on hold. Message costs are charged to the shop's plan allowance. AI agents should be a thin language layer over deterministic rules. SQL and .NET jobs compute health scores, renewal states and fraud flags. The LLM triages support, drafts briefings and writes proposals into an approval queue. Under the existing super, approver and support roles, only humans can move money, change plans or block a shop, and nothing can delete data.

## Five fields at signup; GSTIN only when a shop asks for a tax invoice

The leading khata apps keep the front door almost empty. OkCredit signs users up with a mobile number and OTP and promises **"no forms, no documents"** ([OkCredit FAQ](https://okcredit.in/faq)). myBillBook and Vyapar treat GST details and address as later profile steps ([myBillBook setup guide](https://knowledge.mybillbook.in/articles/8812717-quick-setup-guide); [Vyapar GST guide](https://vyaparapp.in/guides/how-to-manage-gst-compliance-and-taxation-in-vyapar-app)). No source showed any of these apps asking for PAN, bank details or a UPI ID at signup.

One field is not optional once a shop pays: **state**. CGST Rule 46(f) requires the recipient's state on every invoice to an unregistered buyer, whatever the invoice value. That state is treated as the address on record and decides CGST+SGST versus IGST ([CBIC Rule 46](https://taxinformation.cbic.gov.in/content/html/tax_repository/gst/rules/cgst_rules/active/chapter6/rule46_v1.00.html)). Circular 242/36/2024 confirms this for online services ([GST Council circular](https://www.gstcouncil.gov.in/sites/default/files/2025-01/circular-no-242-2024.pdf)). SaaS is charged at **18% GST** ([India Briefing](https://india-briefing.com/news/gst-compliance-for-saas-and-cloud-computing-in-india-explained-39021.html/)). The SAC code is unsettled. The sources point to 998313, 998314 or 998315, and 998431 appears to be news and streaming content ([Kanakkupillai](https://www.kanakkupillai.com/learn/gst-for-saas-companies-in-india/)). A CA has to pick it.

If a shop gives a GSTIN, check its checksum, check that the first two digits match the chosen state, and verify it through an API. Cashfree's API, for example, returns legal name, address and status, and offers a sandbox ([Cashfree GST verification](https://www.cashfree.com/gst-verification/)). Use the response to prefill the B2B invoice and to reject cancelled GSTINs. With UPI AutoPay, the KYC burden falls on **Grocery Khata as merchant**. The shop only approves the mandate with its UPI PIN, and Razorpay's default mandate ceiling of **₹15,000 per debit** sits far above any kirana plan ([Razorpay UPI recurring](https://razorpay.com/docs/payments/payment-gateway/s2s-integration/recurring-payments/upi)). Store only the gateway's customer, mandate and subscription IDs.

DPDP points the same way. The Rules were notified in November 2025, and most substantive duties start around **May 2027** ([SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)). They require purpose-bound notice and consent and favour collecting less, so leave out Aadhaar, shop photos and PAN unless there is a stated purpose. Collect WhatsApp opt-in at signup with **separate tick-boxes for utility and promotional messages**, because Meta permits messages only to users who opted in ([Meta pricing](https://developers.facebook.com/docs/whatsapp/pricing)). Store the timestamp and source of each opt-in. The main fraud risk is trial abuse, not lost money, so allow **one trial per phone, GSTIN or UPI mandate**. Ask for photos or geo-location only when a shop is flagged for review.

## Kirana buyers prepay annually or for three years; monthly needs AutoPay

Vyapar has no monthly plan. Its Basic Desktop+Mobile plan is ₹2,799 a year, its 3-year Saver is ₹6,299, and mobile-only costs ₹599 for one year or ₹1,349 for three ([Vyapar pricing](https://vyaparapp.in/index.php/pricing)). myBillBook Silver is **₹399/year for 1 device**, with a 14-day trial and up to 35% off multi-year through sales ([myBillBook](https://freetrial.mybillbook.in/silver-plan-for-android.html)). Petpooja **dropped monthly billing** in mid-2026 ([PricingSaaS](https://pricingsaas.com/companies/petpooja/diffs/2026W29)). BUSY sells 360-day terms and carries remaining validity into an upgraded plan ([BUSY pricing](https://busy.in/pricing.md)). Khatabook and OkCredit keep the core ledger free. OkCredit charges ₹30 to ₹99 per 30 days for unlimited entries, ad removal, and platform-sent SMS ([OkCredit pricing](https://okcredit.in/pricing)). That gives the price anchors: about ₹400-600 a year for an entry tier, about ₹2,500-3,000 for a full tier, and about 2.2x annual for a 3-year term. Strike-through MRP prices are the local convention.

The proposed 5-year term is outside market practice and costly to carry. For services, GST falls due at the **earlier of invoice or payment** (CGST s.13), so a 5-year prepayment means paying the full GST on day one ([ClearTax](https://cleartax.in/s/advance-received-under-gst)). Revenue is then recognised over 60 months, and an early refund needs credit notes and GST reversal. Cap v1 at 3 years. If 5 years is wanted for marketing, sell it as a price lock billed annually.

Payments need three collection paths that all end in one payment record and one invoice. The first is gateway subscriptions for monthly and yearly plans. Razorpay supports UPI AutoPay, cards and eMandate, with trials, add-ons, pause, upgrade and retries ([Razorpay Subscriptions](https://razorpay.com/docs/payments/subscriptions/)). The second is a one-time UPI link for prepaid multi-year plans. The third is admin-recorded cash or UPI with a receipt number and a proof photo, which follows the dealer channel Marg and BUSY use ([ERPResearch](https://www.erpresearch.com/pricing/marg-erp)). The RBI's 2026 E-Mandate Framework keeps the **₹15,000 no-AFA ceiling**, requires a **pre-debit notice at least 24 hours ahead** and lets customers cancel at any time ([IANS](https://ianslive.in/rbi-tightens-e-mandate-rules-makes-extra-authentication-mandatory-for-recurring-payments-above-rs-15000--20260421192243)). Do not count on retries. Published recovery rates for failed recurring payments range from **25-35%** in independent audits to about **55-57%** in Stripe-reported figures ([Redux Payments](https://www.reduxpayments.com/blog/what-stripe-dunning-misses-in-2025-and-how-to-recover-more); [Churnkey](https://churnkey.co/blog/stripe-smart-retries/)). In India, renewal recovery is mostly reminders plus a grace period.

Offers, complimentary access and holds should be records, not special-case code. A new-member offer is a coupon or price override with a validity window and a redemption cap. A referral offer gives credit days to both shops. A complimentary grant is a **₹0 entitlement grant with an end date, a reason and the granting admin**, never a fake payment. A hold is its own state, distinct from "expired". Accounting software usually turns read-only after lapse while keeping the data: TaxCalc and Reckon go read-only, and IRIS goes read-only after 45 days ([TaxCalc](https://kb.taxcalc.com/2699); [IRIS](https://www.iris.co.uk/support/knowledgebase/kb/pt-bt-p11d-vat-trust-tax-read-only-mode/)). Handle upgrades the way BUSY does, by **converting remaining validity into days** rather than cash proration. Gate value-add features such as multi-device, GST reports, inventory, voice/OCR credits and bulk reminders. Never gate a shop's access to its own records. I found no Indian vendor documentation of entitlement models, so this structure is my synthesis rather than cited practice.

## Customers stay free forever, and a shop's lapse never locks their balance

The OkCredit model fits best. Customers install nothing. They receive entries and reminders by SMS with a UPI link and can pay from any UPI app ([OkCredit udhar khata](https://okcredit.in/udhar-khata-app)). Its free shop tier sends SMS from the shop's own SIM, and only the ₹99 tier pays for platform SMS ([OkCredit pricing](https://okcredit.in/pricing)). Zomato shows the opposite path. It added a ₹2 consumer platform fee in 2023, raised it in steps to about ₹14.9, and drew a backlash ([Outlook Business](https://www.outlookbusiness.com/corporate/zomato-hikes-platform-fee-to-149-per-order-amid-cost-pressures)). A narrow public promise, such as "no fee to view your khata, send lists or order", sets Grocery Khata apart without blocking later payments partnerships.

Free customers still cost money, almost all of it in messaging. A WhatsApp utility message costs about **₹0.115** and a marketing message about **₹0.86**, before BSP fees of roughly ₹999-16,999/month and 18% GST ([ecorpit](https://ecorpit.com/whatsapp-business-api-cost-india-d2c-2026/); [MyOperator](https://myoperator.com/blog/whatsapp-business-api-pricing-india-2026)). A shop with 200 customers receiving 8 messages each a month costs about ₹184 a month, which is large next to a ₹99-299 plan. So each plan gets a monthly message allowance. Customers can trigger only utility templates and replies inside the 24-hour service window. When the allowance runs out, the app falls back to deep links that open the shop's own WhatsApp. AI list and photo reads are charged to the shop's quota, and stored images expire.

A customer can see a khata only after the shop links them, which blocks anyone from enumerating balances. Rate-limit OTPs and orders per number. Under DPDP the shop is most likely the **Data Fiduciary** for its customers' ledger, and Grocery Khata is its **Processor**, bound to follow the shop's deletion instructions under Rule 6 contracts ([Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified)). Rule 8 requires **48 hours' notice before erasure** and logs kept for at least a year ([SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)). In practice:

- When a shop is lapsed or on hold, its customers keep read-only access and can still pay.
- When a shop deletes its account, its customers get a statement PDF and at least 48 hours' notice first.
- When a household asks for erasure, its own profile is deleted, and requests about ledger entries go to the shop.

The shop's terms should state these fiduciary and processor clauses before May 2027. Counsel needs to confirm the role split.

## Rules find problems, the LLM writes, and humans approve every write

Anthropic advises starting with "the simplest solution possible" and adding agent autonomy only when it clearly helps ([Anthropic](https://www.anthropic.com/engineering/building-effective-agents)). OWASP lists **Excessive Agency** (LLM03 in 2026) as a top risk, meaning an agent given writes or messaging without approval on high-impact steps. Its defences are least privilege, human approval, logging every tool call, and treating retrieved text as untrusted, because no single control stops prompt injection ([Giskard](https://www.giskard.ai/glossary/owasp-llm03-2026-excessive-agency); [Reco](https://www.reco.ai/blog/owasp-top-10-for-llm-applications)).

Monitoring should therefore be SQL. Health scores weight real usage above logins: one framework gives product usage 35-45% of the weight and logins about 15%, and uses 14-day windows ([Supportbench](https://www.supportbench.com/building-health-score-predicts-churn/)). For a shop, that means:

- days since the last entry, and 14-day entries against the shop's own baseline
- active devices
- late payments
- open tickets
- days left on the plan

Fake-shop detection also starts as rules: duplicate phone or GSTIN, many shops from one device, trials re-created after expiry, and shops with zero real transactions.

Support deflection is the most proven LLM use. Intercom Fin resolves about **51% out of the box** and **67% on average by December 2025** ([Claude case study](https://claude.com/customers/intercom); [createwith](https://createwith.com/tool/intercom/updates/intercom-ships-200-updates-in-2025-fin-3-reaches-67-average-resolution-rate)). A target of about 50% in the first months is realistic.

On approval mechanics, Claude Managed Agents' `always_ask` setting pauses a session until a human allows or denies the step ([Claude docs](https://platform.claude.com/docs/en/managed-agents/permission-policies)). In a .NET stack, though, the cleaner design is the plain Messages API with **read-only tools** that write proposals into a table. Approval happens in the admin UI, and ordinary application code then carries out the action, so the model never holds write credentials.

WhatsApp category rules matter here. "Your subscription expires on {{date}}. Renew today!" is classified as **marketing**, and a factual due notice counts as **utility** ([YCloud](https://www.ycloud.com/blog/whatsapp-api-message-template-category-guidelines-update)). Automated reminders must stay factual, and offers go out only through approved marketing templates. Since **1 October 2026**, service messages beyond 1,000 a month per number are charged ([Meta pricing](https://developers.facebook.com/docs/whatsapp/pricing)).

## Recommended design for Grocery Khata

**Data model (Postgres).**

| Table | Key columns |
|---|---|
| `shops` | id, owner_name, phone (verified), shop_name, shop_type (kirana/cement/paint), state_code, pincode?, gstin?, legal_name?, billing_address?, gstin_verified_at, status, wa_utility_optin_at, wa_marketing_optin_at |
| `plans` | id, code, name, shop_types[], is_active, is_public, version |
| `plan_prices` | plan_id, term (P1M/P1Y/P3Y; P5Y disabled), mrp, price, currency, gst_inclusive=false, valid_from/to |
| `features` | key (multi_device, gst_reports, inventory, voice_ocr, bulk_reminders, multi_shop, ai_agent), type (bool/limit/credit) |
| `plan_entitlements` | plan_id, feature_key, value (true / limit / monthly credits) |
| `shop_entitlement_overrides` | shop_id, feature_key, value, starts_at, ends_at, reason, granted_by, approval_id |
| `subscriptions` | shop_id, plan_id, price_id, state, current_period_start/end, grace_ends_at, source (trial/gateway/prepaid/manual/complimentary), gateway_sub_id, mandate_id, offer_id |
| `offers` | code, type (percent/fixed/extra_days/trial_extension), eligibility (new_shops, shop_type, referral), valid_from/to, max_redemptions, per_shop_limit=1 |
| `offer_redemptions` | offer_id, shop_id, subscription_id |
| `payments` | subscription_id, method (upi_autopay/card/upi_link/cash/manual_upi), amount, gst_split, gateway_ref / receipt_no, proof_url, recorded_by |
| `invoices` | serial, payment_id, b2b/b2c, place_of_supply, SAC, taxable, cgst/sgst/igst |
| `holds` | shop_id, reason (fraud/dispute/owner_request/compliance), mode (read_only/locked), placed_by, approved_by, lifted_at |
| `usage_counters` | shop_id, month, messages_sent, ai_reads |
| `shop_health` (nightly view) | shop_id, health score and its input signals |
| `agent_runs` | run_id, model, prompt_version, tool_calls (args), tokens, cost |
| `agent_proposals` | run_id, action, args, rationale, required_role, status, decided_by, decided_at |
| `audit_log` | append-only record of every admin and agent action |

**Entitlement rules.** Effective entitlements = plan entitlements + active overrides (the override wins; time-bound). They are computed on the server and cached in the PWA with an expiry timestamp so the app works offline. A set of features is **always on, in every state, for every shop**:

- the khata/udhaar ledger and its entries (the free core, as the market expects)
- viewing past bills
- the customer list
- data export
- customer read-only access and UPI payment

Usage features such as AI voice/OCR and platform messages are monthly credits, not hard gates. Removing a feature from a plan creates a new plan version. Existing subscribers keep the old version until renewal, and shops already paid up on multi-year terms are grandfathered.

**Lifecycle states.**

| State | Shop experience | Moves to |
|---|---|---|
| `trial` (14 days, one per phone/GSTIN/mandate) | full paid features | active / grace |
| `active` | plan entitlements | due |
| `due` (reminders at T-7, T-3, T-0) | full access, banner | active / grace |
| `grace` (+7 days) | full access, red banner | active / read_only |
| `read_only` | free core only; no new paid-feature entries | active / win_back |
| `complimentary` | grant entitlements until end date | active / read_only |
| `on_hold` (admin) | read_only, or locked for fraud only | previous state |
| `cancelled` → `closure_pending` | export window, then customer notice at least 48h before erasure; invoices kept for tax retention | deleted (data minimised) |

**Admin screens and roles.**

| Screen | What it does |
|---|---|
| Shop directory and shop 360 | Shows profile, subscription, payments, health and tickets |
| Plans and features editor | Versioned edits; publish requires super |
| Offers and coupons | Create and edit offers |
| Manual payment entry | Records cash or manual UPI with receipt and proof |
| Complimentary and override grants | Dated grants with a reason |
| Holds | Place and lift holds |
| Proposal approval queue | Agent proposals awaiting a decision |
| Support inbox | Tickets with AI drafts |
| Weekly briefing and metrics | MRR, churn split voluntary/involuntary, trial conversion, recovery rate, AI resolution rate, proposal approval rate, LLM and WhatsApp cost per shop |
| Agent settings | Kill switch, caps |

| Role | Can do |
|---|---|
| **Support** | Answer tickets; approve AI replies and template sends; record manual payments (verified later by an approver); extend a trial by up to 7 days |
| **Approver** | Comps, discounts and offers; refunds; extensions; read_only holds |
| **Super** | Plans and prices; fraud locks; account closures; data exports; role changes; the kill switch |

**AI agent scope.**

| Action | Rule |
|---|---|
| Classify and tag tickets | Runs automatically |
| Answer FAQs from approved Hindi/English help content | Runs automatically |
| Send factual utility templates per state (`plan_due_reminder`, `payment_received`, `plan_expired_readonly`) | Runs automatically, after opt-in and quiet-hours checks, within a per-shop weekly cap |
| Create alerts | Runs automatically |
| Draft the weekly briefing | Runs automatically |
| Free-text message outside a template | Proposal; support approves |
| Marketing or win-back template sends | Proposal; approver approves |
| Comps, discounts, extensions, refunds, plan changes | Proposal; approver approves |
| Holds and fraud flags | Proposal; approver approves; a lock needs super |
| Pricing changes, data exports, closures | Proposal; super approves |
| Deleting data, changing roles or permissions, holding write credentials | Never allowed |

The agent's read tools are `get_shop_summary`, `get_subscription`, `get_payment_history`, `get_ticket_history` and `get_activity_aggregates`. They return aggregates only and never customers' names or ledger lines. The server scopes the shop for each request, and the model never chooses it. Customer and WhatsApp text is passed in as delimited data. Every approved action is re-validated against policy in code. Each run has caps on tokens, iterations and proposals per day. Use a Haiku-class model for triage and a larger model for briefings and investigations, with prompt caching. Current prices and the state of the .NET SDK still need checking in the claude-api docs.

**Decisions the owner must make.**

1. **Price points per shop type**, for example an entry tier near ₹499/yr and a full tier near ₹2,499/yr, with 3-year at about 2.2x.
2. **Whether to offer monthly at all**, and whether only on AutoPay.
3. **Whether to drop 5-year** or sell it as an annual-billed price lock.
4. **Trial length** and how long grace lasts.
5. **The refund policy:** none, or pro-rata.
6. **The exact free-core list** and a public "customers are free" pledge.
7. **Message allowances per plan**, and whether the free tier uses the shop's own WhatsApp.
8. **Who may grant comps, and their maximum length.**
9. **When to register for GST, the SAC code and invoice numbering**, all with a CA.
10. **Which gateway** (Razorpay or Cashfree).
11. **The BSP** for WhatsApp.
12. **The DPDP processor terms and retention periods**, with counsel.
13. **Whether shop data may be sent to a US-hosted LLM**, and whether agent v1 should be limited to briefing and support only.

## Conclusion

The central design choice is to treat a subscription as a **time-bounded set of entitlements that never covers a shop's own records**. That one idea handles offers, comps, holds, plan edits and customer protection without special cases, and it makes AI safe by default. An agent that can only read aggregates and write proposals cannot give away money or lock out a household, however it is prompted. Several gaps remain open:

- India-specific benchmarks for churn and trial conversion
- The exact WhatsApp INR rate card
- Primary texts of the RBI 2026 direction and the DPDP Rules
- The SaaS SAC code

The pricing table and the DPDP role split should be treated as provisional until a CA and counsel sign off.
