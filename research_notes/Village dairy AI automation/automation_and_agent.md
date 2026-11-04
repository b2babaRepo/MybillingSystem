# Village dairy collection centre: cutting human time with automation and an AI agent (2026)

Note: this research used only 5 web searches. No pages were fetched in full, so all citations come from search-result summaries. Check them before quoting figures.

## Time per operation today

### Takeaway
The only sourced throughput figure is about 200 members per hour at a Gujarat society using electronic collection. That works out to about 18 s per farmer (my own arithmetic). Payment processing reportedly dropped from about a week to a few minutes. I found no rigorous time-motion study for manual centres.

### Cited Findings
- In Gujarat cooperatives using electronic weighing and fat testing, about 200 members can deposit milk in one hour. Amul's electronic system cut payment processing from about a week to a couple of minutes across 691 centres — [Down To Earth](https://www.downtoearth.org.in/coverage/milk-and-money-15750)
- AMCS (Automatic Milk Collection System) software prints a slip for each farmer per shift and generates reports such as the Payment Report automatically — [Prompt AMCS blog](https://www.promptdairytech.com/blog/automatic-milk-collection-system-amcs/)
- NABARD has a model project for automated milk collection centres, which is useful for cost and throughput assumptions — [NABARD/TNAU model project](https://agritech.tnau.ac.in/banking/nabard_pdf/Animal%20husbandry/6.Model_project_on_Automated_Milk_Collection_Centers.pdf)
- NDDB has a publication on technology interventions in the DCS (dairy cooperative society) network — [NDDB dairyknowledge PDF](https://www.dairyknowledge.in/sites/default/files/technologicalinterventionsinstrengtheningdcsnetwork-webversion.pdf)

### Inferences
- The brief's figure of 25–30 s per sample with an AMCU (automatic milk collection unit) fits the about 18 s per member implied above, since that society likely ran parallel or pipelined testing. A manual centre (lactometer or Gerber test, handwritten slip, rate-chart lookup) is plausibly 1–3 minutes per farmer, but this is unsourced.
- These per-cycle estimates are unsourced. Manual 10-day billing for about 100 farmers is likely several hours. Reconciliation with the dairy is likely 1–2 hours per cycle. Farmer queries are a steady daily interruption.

### Gaps
- No sourced timings for manual slips, bill preparation, deduction handling or reconciliation. A field time-motion study at 2–3 centres is recommended.

## Automation without AI

### Takeaway
Off-the-shelf AMCU stacks already automate weighing, testing, rate lookup, slips, bills and farmer apps. The main integration work is reading analyser output (RS232/USB serial) and producing payout files.

### Cited Findings
- Ekomilk analysers (made by Bultech 2000) have an RS232 port and can connect to a serial printer. Data goes to a PC running the "Milk Data" program over a null-modem cable. The byte-level frame format is not published in the user guide — [Ekomilk user guide (Scribd)](https://www.scribd.com/document/398766956/user-guide-ekomilk-pdf)
- Lactoscan analysers have DB9 RS232 output. The printout shows date, time, serial number, calibration mode and results such as fat % — [Lactoscan manual (Scribd)](https://www.scribd.com/document/468422753/LactoscanS-Eng)
- Prompt's Farmers App shows AMCS data to farmers. The Amul Farmers app (package com.prompt.ma...) is an Amul and Prompt initiative showing quantity, fat, collection time and amount paid — [APKCombo listing](https://apkcombo.com/amul-farmers-app/com.prompt.ma.maapplicationfinalAmul/)
- NDDB's AMCS ran in about 26,000 DCSs as of October 2025, with over 2.43 lakh farmers on the mobile app — [PIB document, Jan 2026](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2026/jan/doc202619754001.pdf)
- NDDB's Dugdh Sankalan Sathi app offers digital payment to farmers — [IASGyan](https://www.iasgyan.in/daily-current-affairs/dugdh-sankalan-sathi-mobile-app)
- Stellapps mooPay pays farmers directly into their bank accounts, removing the cost and risk of handling cash. It was launched in Bhilwara, Rajasthan, in October 2020 and with Paras Dairy — [IIT Madras ACR](https://acr.iitm.ac.in/iitm_news_repository/iit-madras-startup-launches-moopay-for-dairy-farmers-in-rajasthan); [Agriculture Post](https://agriculturepost.com/paras-dairy-partners-with-moopay-for-making-real-time-digital-payments-to-farmers/)
- Amul offers Micro ATM cash withdrawal at the society — [Agriculture Post](https://agriculturepost.com/amul-launches-digital-payment-through-micro-atm-at-dairy-farmers-doorstep/)
- WhatsApp utility templates in India cost about ₹0.115 per delivered message at Meta's list price (pre-GST), or ₹0.145 through the provider AiSensy. From October 2026, utility templates sent inside the 24-hour window are billed instead of free — [Hyperleap](https://hyperleap.ai/whatsapp-pricing-calculator-india); [AiSensy](https://m.aisensy.com/blog/whatsapp-pricing-update-october-2026/); [Meta docs](https://developers.facebook.com/docs/whatsapp/pricing)

### Inferences
- High-value non-AI steps:
  1. A serial/USB listener that parses analyser lines and scale weight into a collection row.
  2. A farmer code or RFID/QR card, so no name is typed.
  3. A versioned rate chart (fat/SNF matrix) applied automatically.
  4. An auto WhatsApp slip, costing about ₹0.12–0.15 each. Two shifts × 100 farmers is about ₹25–30 per day, so offer a daily summary or an SMS/print option instead.
  5. Auto 10-day bills with deductions such as feed and advances.
  6. A bank bulk-payout or UPI payout file that the owner approves.
- Analyser formats vary by firmware. Plan per-model parsers plus a "capture raw line" fallback.

### Gaps
- No public protocol spec for Prompt, Ekomilk or Lactosure output. "Lactosure" was not found and may be a different brand. Ask vendors or distributors for it, or capture sample output from a real device.
- No sourced UPI bulk-payout costs for small operators.

## AI options

### Takeaway
Vision OCR of slips and dairy statements, Hindi voice entry, a WhatsApp balance bot, anomaly flags and reconciliation are all feasible. I found no published dairy-specific accuracy or savings figures, so pilot each one under a strict draft-then-confirm rule.

### Cited Findings
- Stellapps reports about 30% better milk quality (MBRT, a bacterial-quality test) with its cold-chain monitoring. It also reports loans made over the mooPay rails, and says 70% of the farmers in one deployment are women. These are company-sourced claims — [Index Insurance Forum](https://indexinsuranceforum.org/node/858); [Vivriti Stellapps factsheet](https://www.vivriticapital.com/assets/files/sustainability/Stella-Apps.pdf)
- In the same Index Insurance Forum interview, Stellapps says payments ran on time through COVID because the process was fully digital — [Index Insurance Forum](https://indexinsuranceforum.org/node/858)

### Inferences
- **Photo to draft rows:** Read the analyser's printed slip or the dairy's 10-day statement into draft rows. This reuses the same pattern the app already uses for distributor bill photos (commit 3d94a3a).
- **Voice entry:** Parse a phrase such as "Ramesh 5 litre bhains 6.5 fat" into {farmer, qty, animal, fat}. Always show a readback before saving. Bhojpuri and Maithili accuracy is unverified.
- **WhatsApp bot:** Answer "kitna paisa bana?" from read-only records, using templates. An LLM is only needed for free-form questions.
- **Rule-based anomaly flags:** fat or SNF more than X SD from the farmer's own baseline; SNF low while fat is normal (possible water adulteration); identical readings repeated; farmers with no recent activity or a shared bank account (possible dummy farmers); shortage between local litres/fat and the dairy's accepted figures.
- **Reconciliation:** Match the dairy's statement to local records line by line or per shift, and list the differences.
- **Forecasting:** Use simple seasonal averages. An LLM is not needed.

### Gaps
- No sourced accuracy benchmarks for Indic speech recognition or vision OCR on dairy slips.
- No independent time or cost savings data from Akashganga, Prompt or Stellapps. Only vendor claims were found.

## Guardrails and the "dairy agent" design

### Takeaway
The agent should read and draft only. Anything touching money, test values, rate charts or messages to farmers needs explicit owner approval, with an audit log.

### Cited Findings
- No direct sources were found on DPDP Act guidance for dairy cooperatives. The only related context is that WhatsApp pricing is per delivered template, which affects bot cost — [Meta docs](https://developers.facebook.com/docs/whatsapp/pricing)

### Inferences
- **Tools the agent can use freely:** read_collections, read_rate_chart, draft_slip, draft_10day_bill, draft_reconciliation, answer_farmer_query (read-only, farmer's own data only), flag_anomaly, forecast_volume.
- **Tools that need owner approval:** send_bulk_messages, finalise_bill, create_payout_file or trigger payout, edit any collection or test value, change rate chart, add or remove a farmer, record an advance or feed deduction.
- **Never allowed:** silently altering fat, SNF or quantity; paying without confirmation.
- **Offline:** capture locally and sync later; keep a printed slip as fallback.
- **DPDP:** record farmer consent at registration, collect only what is needed, give the bot access to the requester's own records only, and log every agent action.
- **Cost:** with templated replies, LLM calls are only for photos, voice and free-text questions. This is likely a few rupees per day per centre (unsourced estimate).

### Gaps
- DPDP Rules specifics for cooperatives, and the actual LLM cost for Indic voice, were not researched within the search budget.
