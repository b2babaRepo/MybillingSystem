# AI Ops Agents for Grocery Khata Platform Admin (monitoring, subscriptions, support)

## Proven uses of AI agents in SaaS ops (health scores, dunning, support, anomaly detection, briefings)

### Takeaway
The proven pattern is that deterministic data and rules find the problems: health scores from usage, retry schedules for failed payments, fraud heuristics. The LLM writes and triages: support answers, message drafts, weekly briefings. Support deflection is the most proven LLM use, at about 50-67% average resolution for mature vendors.

### Cited Findings
- Health scores usually combine login frequency, feature usage, support tickets, NPS and billing issues. Product usage commonly carries the most weight (35-45% in one framework), while logins alone are about 15% because "a customer could log in every day but not actually use the core features" — [Supportbench](https://www.supportbench.com/building-health-score-predicts-churn/), [Statisfy](https://www.statisfy.com/blog/customer-health-score-predict-churn)
- Track login frequency over a 14-day window for a faster read on disengagement, and check score weights against historical churn — [Supportbench](https://www.supportbench.com/building-health-score-predicts-churn/), [Statisfy](https://www.statisfy.com/resources/customer-health-scores)
- I found no ChartMogul-specific health-score documentation. These sources are vendor blogs, not peer-reviewed research — (search result)
- Stripe Smart Retries automatically retries failed payments to prevent involuntary churn — [Stripe docs](https://docs.stripe.com/billing/revenue-recovery)
- Recovery-rate claims conflict. The reported Stripe benchmark is about 55-57% of failed recurring payments recovered, with Smart Retries adding about 9% over fixed schedules. Independent vendor audits of 200+ B2C accounts found 25-35% — [Churnkey](https://churnkey.co/blog/stripe-smart-retries/), [Redux Payments](https://www.reduxpayments.com/blog/what-stripe-dunning-misses-in-2025-and-how-to-recover-more), [Stuut](https://www.stuut.ai/blog/stripe-smart-retries-vs-dedicated-dunning-tools-which-wins)
- Deliveroo reportedly recovered more than £100M in a year with Smart Retries and other Stripe recovery tools — [Churnkey](https://churnkey.co/blog/stripe-smart-retries/)
- Intercom Fin's AI resolution rates:
  - About 51% out of the box, before tuning — [Claude case study](https://claude.com/customers/intercom)
  - 54% average on the Fin page (undated) — [Intercom](https://intercom.com/fin)
  - 67% rolling 30-day average by Dec 2025 — [createwith](https://createwith.com/tool/intercom/updates/intercom-ships-200-updates-in-2025-fin-3-reaches-67-average-resolution-rate)
  - Top customers above 80% — [Intercom blog](https://www.intercom.com/blog/videos/the-secret-to-sustaining-75-ai-resolution-rates)
  - "50-70% AI resolution is common" — [WorkOS](https://workos.com/blog/intercom-skeptics-to-believers-ai)
- Anthropic separates workflows (predefined code paths) from agents (the LLM directs its own tool use). It advises "the simplest solution possible", often a single optimized LLM call, and adding agent autonomy only when it clearly helps — [Anthropic, Building effective agents](https://www.anthropic.com/engineering/building-effective-agents)

### Inferences
- For Grocery Khata, the health score should be a SQL job, not an LLM. Inputs per shop:
  - Days since last bill or khata entry, and 14-day entry count against the shop's own baseline
  - Number of active staff devices
  - Failed or late payments
  - Open support tickets
  - Plan days remaining

  Weights can start hand-set and be adjusted later against shops that actually churned.
- In India, many renewals are manual UPI or AutoPay payments rather than card retries. So "dunning" is mostly a reminder sequence plus a grace period, not retry logic. Expect recovery nearer the conservative 25-35% range until UPI AutoPay mandates are used.
- Fake-shop and abuse detection should start as rules: duplicate phone or GST numbers, many shops from one device or IP, trials re-created after expiry, zero real transactions. The LLM only summarizes flagged cases for the approver.
- The weekly admin briefing is a good first LLM feature. It is low risk: it reads aggregates only and writes nothing.
- Support target: about 50% of tickets auto-answered in the first months is realistic, based on Fin's out-of-the-box figures.

### Gaps
- I found no India-specific case studies of AI ops agents in small-merchant SaaS (Khatabook, OkCredit and Vyapar do not publish this).
- I found no primary Stripe source for the 55-57% figure.
- I found no peer-reviewed numbers on how well churn prediction works for micro-merchants.

## Autonomy vs human approval; audit, rate limits, prompt injection, privacy

### Takeaway
Use least privilege: read-only tools by default, and every write or outbound message goes through a proposal queue. Only low-risk, template-bound actions run on their own. Customer messages are untrusted input, and no single defence stops prompt injection.

### Cited Findings
- OWASP calls this risk "Excessive Agency": giving an LLM tools, writes or messaging without approval on high-impact steps. It comes from excessive functionality, excessive permissions and too little oversight. OWASP recommends a human in the loop for sensitive operations. The category was renumbered to LLM03 in the 2026 edition (it was LLM06 in 2025) — [Giskard](https://www.giskard.ai/glossary/owasp-llm03-2026-excessive-agency), [CSO Online](https://www.csoonline.com/article/575497/owasp-lists-10-most-critical-large-language-model-vulnerabilities.html)
- Mitigations sources agree on:
  - Least privilege, with narrow or read-only tools
  - Human approval for high-impact or irreversible actions
  - Logging every tool call
  - Treating retrieved content as untrusted
  - Defence in depth, because no single mitigation stops prompt injection

  — [SecureLayer7](https://securelayer7.net/learn/ai-security/excessive-agency), [Reco](https://www.reco.ai/blog/owasp-top-10-for-llm-applications)
- LLMs cannot reliably tell trusted instructions apart from untrusted data. Hidden instructions in an email or message can push the model to misuse its tools — [Reco](https://www.reco.ai/blog/owasp-top-10-for-llm-applications)
- Anthropic recommends sandbox testing, programmatic checks on intermediate steps, human checkpoints and stopping conditions such as a maximum number of iterations. It also says to design tools so mistakes are harder to make — [Anthropic](https://www.anthropic.com/engineering/building-effective-agents)

### Inferences (recommended action matrix)
- **Agent acts alone, within caps:**
  - Classify and tag tickets
  - Answer FAQs from approved help content in Hindi or English
  - Send pre-approved utility templates on schedule: renewal due on a date, payment received, plan expired
  - Create internal alerts and draft the briefing
- **Needs approval from an approver or super admin:**
  - Refunds, free or extended plans, discounts and offers
  - Price or plan changes
  - Blocking or suspending a shop, or flagging it as fraud
  - Any free-text outbound message outside a template
  - Marketing broadcasts
  - Data exports
- **Never allowed:** deleting data, or changing roles or permissions.
- The existing roles fit the approval queue:
  - Support role approves replies and messages.
  - Approver role approves money and plan actions.
  - Super admin approves blocking and pricing.
- Audit log: an append-only Postgres table storing agent run id, model, prompt version, the tool calls and arguments, the proposal, the approver, the decision and timestamps.
- Rate limits and caps:
  - At most N outbound messages per shop per week
  - A daily cap on proposals
  - A per-run token cap
  - A kill switch flag in admin settings
- Prompt-injection controls:
  - Pass customer WhatsApp text only as data inside delimited fields.
  - The support agent has no write tools and only sees the current shop's context, so the shop is scoped server-side and never chosen by the model.
  - Every action is re-validated server-side against policy, however persuasive the model's output.
- Privacy:
  - Tools return aggregates such as entry counts, last activity, plan and payment status.
  - Tools never return khata line items, customer names or phone numbers of the shop's own customers. Expose those only through a separate tool that needs explicit admin consent.
  - Mask personal data in logs.
  - India's DPDP Act 2023 (rules notified 2025) probably applies. It was not verified in this research; see gaps.

### Gaps
- I did not fetch India DPDP Act and Rules obligations (consent, purpose limitation, sending data to a US-hosted LLM). Legal review is needed.
- I found no benchmark for acceptable automated-message frequency for Indian merchants.

## Architecture: rules first, LLM for language, read-only tools plus proposal queue, Claude SDK options, cost

### Takeaway
Run .NET scheduled jobs (e.g. Hangfire or Quartz) for health scores, expiry, reminders and anomaly rules. Call Claude through the plain Messages API with tool use for language tasks and the read-only investigation agent. All writes go into a `agent_proposals` table that humans approve in the admin UI. Managed Agents or the Agent SDK are optional. Both support approval gates natively.

### Cited Findings
- Anthropic recommends using LLM APIs directly first, because frameworks hide prompts and responses and make debugging harder — [Anthropic](https://www.anthropic.com/engineering/building-effective-agents)
- Claude Managed Agents permission policies are set per toolset. With `always_ask`, the session pauses until the client sends an allow or deny confirmation event. A deny cannot be overridden, and every tool-use event carries an `evaluated_permission` field — [Claude docs: permission policies](https://platform.claude.com/docs/en/managed-agents/permission-policies)
- The Claude Agent SDK checks permissions in a fixed order (hooks first, then deny rules). A `canUseTool` callback handles runtime approval. Because `canUseTool` only fires for calls that would otherwise prompt, a PreToolUse hook is suggested to gate every call — [Agent SDK permissions](https://platform.claude.com/docs/en/agent-sdk/permissions), [dev.to](https://dev.to/sekeraradim/human-approval-gates-in-the-claude-agent-sdk-1epe)

### Inferences (recommended architecture)
1. **Signals layer, in SQL and .NET only:**
   - A nightly `shop_health` materialized view
   - A subscription state machine: trial → active → due (T-7, T-3, T-0) → grace (+7 days) → expired/read-only → win-back
   - Anomaly rules
   - All of it is testable and costs nothing per run.
2. **Messaging layer:** a template registry mapping each state to an approved WhatsApp template, plus opt-in and quiet-hours checks. No LLM is involved in sending.
3. **LLM tasks:**
   - (a) Support triage and answering, using the help corpus with Hindi and English (Hinglish) prompts
   - (b) Weekly briefing for the platform admin, built from aggregates
   - (c) An "investigate shop X" agent with read-only tools: get_shop_summary, get_subscription, get_payment_history, get_ticket_history, get_activity_aggregates
   - (d) Drafting proposals such as "offer 1 month at 50% to these 12 at-risk shops", written to `agent_proposals` with a rationale
4. **Approval UI:** an admin screen listing proposals, gated by role. The approver clicks approve, and then normal application code performs the action. The LLM never holds write credentials.
5. **Where to host Claude:** C# can call the Messages API over HTTP with a manual tool loop, which is simplest for a .NET stack. Managed Agents is an option if hosted sessions are wanted. The Agent SDK is Python/TypeScript, so it would mean a sidecar service.
6. **Cost controls:**
   - Use a small model (Haiku-class) for triage and classification, and a larger model only for briefings and investigations.
   - Use prompt caching for the system prompt and help corpus.
   - Batch nightly jobs.
   - Set per-day token budgets and log cost per run.
   - Current prices were not verified here; check the claude-api docs.

### Gaps
- I did not verify current Claude model prices or Batch API discounts in this research.
- I did not verify whether an official .NET Anthropic SDK exists; check before choosing.

## WhatsApp Business messaging rules and India costs (2025-2026)

### Takeaway
Since July 2025, Meta charges per delivered template message.

- **Renewal-due and expiry notices:** must be strictly factual utility templates, about ₹0.115 each. Any "Renew today!", offer or upsell turns the template into a marketing one, about ₹0.86 each.
- **Opt-in:** required.
- **From 1 Oct 2026:** utility messages inside the 24-hour window and service messages beyond 1,000/month per number are no longer free.

### Cited Findings
- Billing changed from conversation-based to per delivered message on July 1, 2025. Template categories are marketing, utility and authentication. Free-form "service" messages can only be sent inside the 24-hour customer service window, which opens or resets each time the user messages you — [Meta pricing docs](https://developers.facebook.com/docs/whatsapp/pricing)
- "You can only send messages to WhatsApp users who have opted in to receiving messages from you" — [Meta](https://developers.facebook.com/docs/whatsapp/pricing)
- Utility messages inside an open window were free from 1 Jul 2025 to 30 Sep 2026 and have been charged since 1 Oct 2026. From 1 Oct 2026, each number gets 1,000 free service messages per month and pays beyond that. A Meta Business Agent category was added 1 Jul 2026 at $2 per million tokens. Replies to Click-to-WhatsApp ad leads open a free 7-day window — [Meta](https://developers.facebook.com/docs/whatsapp/pricing)
- India changes:
  - Marketing rate went up 1 Jan 2026.
  - India has utility volume tiers.
  - INR billing launched in 2026. Accounts must move to INR by 31 Dec 2026, or delivery stops from 1 Jan 2027.

  — [Meta](https://developers.facebook.com/docs/whatsapp/pricing)
- India rates, as reported by vendors (check Meta's rate-card CSV):
  - Marketing about ₹0.8631 per message (up from ₹0.7846)
  - Utility and authentication about ₹0.115

  Other vendors quote marketing at ₹1.09 and utility at ₹0.14-0.15, probably including BSP markup. Total cost = Meta fee + BSP fee (about ₹999-16,999/month) + 18% GST — [Robylon](https://www.robylon.ai/blog/whatsapp-business-api-pricing-india), [ecorpit](https://ecorpit.com/whatsapp-business-api-cost-india-d2c-2026/), [AiSensy](https://m.aisensy.com/blog/whatsapp-business-pricing/)
- Template category rules (tightened July 2025):
  - A template that mixes utility and marketing content is treated as marketing.
  - Utility templates must not promote, upsell, include offers or "attempt to secure renewals".
  - "Your subscription expires on {{date}}. Renew today!" is classified as marketing.
  - Factual reminders such as "Your payment for {{service}} is due on {{date}}" pass as utility.
  - A reclassification can be appealed within 60 days.

  — [YCloud](https://www.ycloud.com/blog/whatsapp-api-message-template-category-guidelines-update), [Wati](https://support.wati.io/en/articles/15932488-best-practices-to-reduce-whatsapp-utility-to-marketing-template-reclassification), [WANotifier](https://help.wanotifier.com/en/article/whatsapp-utility-templates-getting-recategorized-to-marketing-heres-the-fix-xjz06j/). These are vendor sources; Meta's official guideline text was not fetched.

### Inferences
- Use separate templates:
  - Utility: `plan_due_reminder` (factual: plan, expiry date, amount, payment link), `payment_received`, `plan_expired_readonly`
  - Marketing: `winback_offer` and `upgrade_offer`, sent only to shops that opted in to marketing, and only after approval
- Cost example: 1,000 shops × 4 utility reminders per month ≈ ₹460 plus GST, before BSP fees. Reminder cost is negligible next to plan revenue.
- Collect explicit WhatsApp opt-in at shop signup, with separate tick-boxes for utility and promotional messages. Store opt-in in Postgres with timestamp and source.
- AI support answers on WhatsApp are service messages inside the 24-hour window. Since 1 Oct 2026 they are free only up to 1,000 per month per number.

### Gaps
- I did not read Meta's official India rate-card CSV directly, so the exact INR figures and utility tier thresholds are unverified.
- Exact per-message service pricing beyond the free tier was not found.

## Metrics to track

### Takeaway
Track:
- MRR and net new MRR (new, expansion, churned)
- Logo churn and revenue churn, split into voluntary and involuntary (failed or late payment)
- Trial→paid conversion
- Dunning recovery rate
- Health-score distribution
- Support metrics: AI resolution rate, first response time and time to resolution, escalation rate, CSAT/NPS
- Agent-specific metrics: proposals per week, approval rate, overrides, cost per run

### Cited Findings
- AI resolution rate is the industry headline metric for support agents. Intercom reports a rolling 30-day rate (67% by Dec 2025) — [createwith](https://createwith.com/tool/intercom/updates/intercom-ships-200-updates-in-2025-fin-3-reaches-67-average-resolution-rate)
- Health-score inputs to monitor include NPS, ticket volume and billing issues — [Statisfy](https://www.statisfy.com/blog/customer-health-score-predict-churn)
- Failed-payment recovery rate is the main dunning KPI, with published ranges of 25-57% — [Redux Payments](https://www.reduxpayments.com/blog/what-stripe-dunning-misses-in-2025-and-how-to-recover-more), [Churnkey](https://churnkey.co/blog/stripe-smart-retries/)

### Inferences
- Agent safety KPIs:
  - Proposal approval rate (low means a noisy agent)
  - Wrong-answer rate on sampled support conversations, reviewed weekly
  - Prompt-injection or policy-block events
  - WhatsApp template reclassification and block or quality rating
  - LLM cost per shop per month
- Add WhatsApp cost per retained shop, and messages per shop per month.

### Gaps
- I found no India micro-SaaS benchmarks for churn or trial conversion. ChartMogul and other benchmark reports were not fetched.
