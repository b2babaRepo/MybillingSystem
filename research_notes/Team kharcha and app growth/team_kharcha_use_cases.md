# Team Kharcha (Shared Expense Book): Users, Needs, DPDP, Pricing (India, as of Oct 2026)

Scope note: this is a short research pass (7 searches, no page fetches). Several figures come from secondary sources or blogs and are marked as such. I found no India survey that measures shared-expense tracking by user group, so the ranking of groups below is inference.

## Use cases and their needs: which groups are largest and most frequent?

### Takeaway
SHGs and housing societies are huge, organised groups with formal bookkeeping, but each already has its own channel (NABARD EShakti, MyGate/ADDA). For a kirana/household app, the best groups are (1) shop partners and staff and (2) family/joint households, because they enter expenses daily and already fit the app's "khata" habit. Roommates and trips are well served by Splitwise, and their use is episodic.

### Cited Findings
- SHGs: as of FY2022, SHGs held cumulative savings above Rs 47,240 crore and supported livelihoods of 14.2 crore households. Loans disbursed to SHGs in FY2022 were Rs 99,729 crore, and the number of credit-linked groups rose 6% over FY2021 — [NABARD, Fostering Grassroots Institutions](https://www.nabard.org/pdf/fostering-grassroots-institutions-eng.pdf)
- NABARD's EShakti project is building a national digital database of SHG books for banks and promoting institutions. NABARD says digitisation improves record keeping and bank confidence, which in turn improves credit flow. Dates: launch about 2015 to 2017; PIB described it as a pilot in 2017 — [TERI news](https://www.teriin.org/news/nabard-commences-digitisation-shgs); [c4scourses (secondary)](https://c4scourses.in/blog/e-shakti-initiative-of-nabard/)
- Housing societies and RWAs already pay for society apps. Blog estimates put MyGate at about Rs 3–12 per flat per month and ADDA at about Rs 4–10, but these figures are unverified. ApnaComplex publishes a starting price of Rs 499 per month. One comparison calls MyGate's accounting "basic," not audit-ready — [codingclave 2026](https://codingclave.com/blog/best-society-management-app-india-2026); [codingclave alternatives](https://codingclave.com/blog/mygate-alternatives-india-2026); [MyGate blog on society accounting challenges](https://mygate.com/blog/housing-society/challenges-in-society-accounting/)
- Shared living: co-living operators recommend Splitwise to roommates. I found no India survey on roommate or PG expense apps — [Colive blog](https://www.colive.com/blog/?p=2213)
- Splitwise now caps free users in India at about 3–4 expenses a day, with a cooldown between entries. This pushes heavy daily users, such as households and shops, to look for alternatives — [ITVoice](https://www.itvoice.in/splitwise-has-introduced-restrictions-on-the-number-of-free-expenses-users-can-add); [Tricount blog, 2025 (competitor)](https://tricount.com/blog/top-splitwise-alternatives-in-india-2025-which-app-should-you-switch-to)

### Inferences
- **Shop partners and staff** (daily, high value): they need petty-cash advances to a helper, the running balance per person, receipt photos, owner approval of entries, categories (transport, chai, packing, repairs), and a month report. These users are the most likely to pay. The app already has owner and helper logins.
- **Family / joint household** (daily, the largest number of users): they need monthly ghar kharcha by person and category, Hindi voice entry, and a budget per category. Most want plain tracking, not "who owes whom." Privacy matters inside families too, so a member may want some entries kept private.
- **Roommates / PG / hostel** (weekly): they need who-owes-whom, settle-up through a UPI link, and a fixed monthly split for rent and groceries. Splitwise is the incumbent here, and its daily cap is the opening.
- **Trips and weddings/festivals** (short bursts): they need a temporary team, many invited users, a contribution pool, and a final summary with a PDF or WhatsApp share. These are good for virality (many invitees) and poor for revenue.
- **SHG / RWA / temple committees** (monthly, formal): they need receipts, an audit trail, approval by a treasurer, and member contributions versus spends. They have specialised tools, so serve them only with simple exports.
- **Contractors, site teams and field teams**: they need advance-and-settle, photo proof, offline entry and per-site tags. This is close to the shop petty-cash model.
- Features that matter to all groups: offline-first sync, Hindi voice entry, an edit/delete history visible to members (trust), and WhatsApp share of daily and month summaries.

### Gaps
- No India data on how many households or shops track shared expenses, or on usage frequency by group. KPMG and RedSeer reports were not found in this pass.
- No current (2025–26) count of SHGs or of EShakti digitisation.
- No MSME petty-cash survey was found.

## Expected features per group (approval, receipts, budgets, owes vs tracking, petty cash, voice, offline)

### Takeaway
The market splits on one design choice: tracking (households, shops, committees) versus settling (roommates, trips). Paid tiers in this category charge for receipts/OCR, history search, budgets/charts and exports, not for basic entry.

### Cited Findings
- Splitwise Pro features: receipt storage and OCR scanning, JSON backups, currency conversion, budgeting charts and full history search — [ITVoice](https://www.itvoice.in/splitwise-has-introduced-restrictions-on-the-number-of-free-expenses-users-can-add); [getfinny 2026 pricing guide](https://getfinny.app/blog/splitwise-pricing-2026)
- Society accounting pain points include tracking collections against spends and audit readiness — [MyGate blog](https://mygate.com/blog/housing-society/challenges-in-society-accounting/)

### Inferences
| Group | Approval | Receipts | Budgets | Owes vs track | Petty cash | Voice/offline |
|---|---|---|---|---|---|---|
| Shop | Owner approves | Yes | Monthly limit per helper | Track | Core | High |
| Family | Optional | Low | Per category | Track | Pocket money | Hindi voice is key |
| Roommates | No | Low | Low | Owes | No | Medium |
| Trip/Event | No | Medium | Event budget | Owes or pool | Pool | Offline (travel) |
| SHG/RWA/Temple | Treasurer | Yes | Yes | Track (contributions) | Yes | Medium |
| Site/Field | Supervisor | Photo proof | Per site | Track | Core | Offline is key |

### Gaps
- No Indian app-review analysis of which features users ask for most. Play Store reviews were not fetched.

## DPDP Act 2023 and DPDP Rules 2025: what applies to sharing expense data with team members

### Takeaway
The DPDP Rules were notified on 14 Nov 2025. Most duties for app operators (notice, consent, security, erasure, children's data) take effect 18 months after that, around **mid-May 2027**, so build for them now. Showing a member's entries to the team needs that member's clear consent when they join. Leaving a team must stop sharing, and erasure must work. Users under 18 need verifiable parental consent.

### Cited Findings
- Rules notified 14 Nov 2025. Data Protection Board provisions apply immediately. Consent Manager registration (Rule 4) applies at 12 months. Rules 3 and 5–16 (notice, security, breach, retention/erasure, children) and Rules 22–23 apply at 18 months. Sources partly disagree on the Rule 4 timing — [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/); [KPMG guidance](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/11/dpdp-rules-2025-guidance-to-dpdp-act-implementation.pdf); [EY](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025); [dpdpa.com timeline](https://dpdpa.com/dpdpa_enforcement_timeline.html)
- Children (under 18): verifiable parental consent is required before processing, and the fiduciary must check that the parent is an identifiable adult using reliable identity or age details. Tracking, behavioural monitoring and targeted ads aimed at children are barred. The penalty is up to Rs 200 crore (one dpdpa.com FAQ says Rs 150 crore; the Schedule says 200) — [dpdpa.com Section 9](https://www.dpdpa.com/dpdpa2023/chapter-2/section9.html); [dpdpa.com Schedule](https://dpdpa.com/theschedule.html); [SFLC.in](https://ddn.sflc.in/blog/digital-rights-protection-of-children-the-law-and-the-reality-a-comparative-analysis/)
- Section 12 gives rights to correction and erasure. Section 8(7): on withdrawal of consent, or once the purpose is served, the fiduciary must erase the data, including at its processors, unless the law requires keeping it — [dpdpa.com chapter 3](https://dpdpa.com/dpdpa2023/chapter-3/chapter3.html); [dpdpa.com Section 8](https://www.dpdpa.com/dpdpa2023/chapter-2/section8.html)

### Inferences (design implications, not legal advice)
- Joining a team: show a short Hindi/English notice before the user joins, for example: "Your entries, name and amounts will be visible to the members of [team]." Record the consent with a timestamp. An invite link alone is not consent.
- Leaving a team: stop sync at once. Let the leaver choose either to delete their entries or to keep them in the team's books as anonymised "Former member" entries. Shop books may have a lawful reason to keep records (accounts and tax), which should be stated in the notice.
- Withdrawing consent must be as easy as giving it (one tap in Settings).
- Minors: block users under 18 from joining by self-declaration, or let a parent add a child as a non-login "member name" whose entries the parent makes. This avoids building an age-verification flow.
- Receipt photos may contain other people's data, so keep them visible to the team only and delete them along with the entry.
- Do not use team expense data for ad targeting without separate consent.

### Gaps
- The official gazette text was not fetched, so check the exact rule numbers and dates on MeitY.
- It is unclear whether a family team is "personal or domestic purpose" (Section 3(c)(ii)) for members. The exemption covers individuals, not the app operator.

## Pricing: freemium limits, team plans, what Indian users pay for

### Takeaway
Indian users pay roughly Rs 100–150 a month or Rs 600–1,000 a year for consumer expense tools, and Rs 500–4,000 a year for shop billing. Charge the team owner (the shop owner, head of family or treasurer) and keep invited members free.

### Cited Findings
- Splitwise Pro in India is reported at Rs 149 a month or Rs 999 a year (US: $5 a month or $40 a year). The free tier is capped at about 3–4 expenses a day. These figures are not on Splitwise's site, so check the store listing — [ITVoice](https://www.itvoice.in/splitwise-has-introduced-restrictions-on-the-number-of-free-expenses-users-can-add); [getfinny](https://getfinny.app/blog/splitwise-pricing-2026)
- Vyapar: the mobile app has a free tier. Older mobile plans were about Rs 599–942 a year, and Desktop Gold is Rs 3,399 a year (2026 Capterra listing), with 18% GST extra. Sources conflict — [Capterra](https://www.capterra.com/p/180579/Vyapar/pricing/); [Techjockey (older)](https://techjockey.com/compare/khatabook-vs-vyapar); [itforsme](https://www.itforsme.in/pricing/vyapar-india)
- Khatabook's core ledger is free. No current subscription price was found — [Techjockey](https://techjockey.com/compare/khatabook-vs-vyapar)
- Society apps charge per flat (unverified estimate of Rs 3–15 per flat per month), with ApnaComplex starting at Rs 499 a month — [codingclave](https://codingclave.com/blog/best-society-management-app-india-2026)

### Inferences (ways to make money without charging invited members)
- Owner-pays team plan: free for 1 team with up to about 3 members and the last 2 months of history. Pro (about Rs 49–99 a month or Rs 499–999 a year) unlocks more members, unlimited history, PDF/Excel exports, receipt storage, approvals and petty-cash advances.
- Do not cap daily entries. Splitwise's cap is a top complaint and the opening for a competitor.
- Paid add-ons: a festival or wedding event pass (one-time fee of about Rs 99) and a committee/SHG audit report.
- Non-subscription revenue: distributor or brand offers to the shop, UPI settle-up links, and lending or credit partnerships based on cash-flow history. Any of these needs separate DPDP consent.
- Invitees are the growth channel. Each team brings several new installs, so free invitees pay off as acquisition.

### Gaps
- No Indian willingness-to-pay data for team or family plans.
- Khatabook's and OkCredit's current paid tiers were not checked.
