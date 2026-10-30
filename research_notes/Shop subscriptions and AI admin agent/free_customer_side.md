# Free customer side: two-sided models, lapse handling, costs, abuse, monetisation (India, 2026)

## How Indian two-sided apps charge the merchant and keep consumers free

### Takeaway
The khata apps' pattern fits Grocery Khata best. The shop pays (or uses a free tier with ads). The customer never installs anything: they get SMS or WhatsApp entries, reminders and a UPI pay link. Food platforms show the opposite risk: they take merchant commissions and also add consumer fees over time.

### Cited Findings
- OkCredit: customers do not need the app. They receive each entry and payment reminders over SMS and can pay through any UPI app. — [OkCredit udhar khata page](https://okcredit.in/udhar-khata-app)
- OkCredit plans: Basic is free, has ads, and sends transaction SMS from the shop's own phone SIM. Unlimited Transactions is ₹30 for 30 days. Ads Free++ is ₹75 for 30 days (multi-device, GST bills, defaulters, desktop, stock). Premium is ₹99 for 30 days and adds unlimited transaction SMS sent by OkCredit. — [OkCredit pricing](https://okcredit.in/pricing)
- OkCredit says its Basic plan covers unlimited customers, entries, reminders and backup, and that reminders carry a UPI link. — [OkCredit FAQ](https://okcredit.in/faq); [OkCredit reminders](https://okcredit.in/payment-reminder-app)
- Khatabook keeps core bookkeeping free. Its revenue comes from premium features (reports, GST and inventory tools), lending (working-capital loans with partner NBFCs) and payments. An executive is quoted saying basic bookkeeping will stay free. — [StartupTalky](https://startuptalky.com/khatabook-business-model/); [Entrepreneur India](https://india.entrepreneur.com/entrepreneurs/msme-empowerer/449646)
- Khatabook was reported at about INR 70 Cr annualised gross revenue, with profitability expected in 18–24 months (date of the interview not confirmed). — [Entrepreneur India](https://india.entrepreneur.com/entrepreneurs/msme-empowerer/449646)
- Dukaan is a merchant-paid store builder. Listings show a free tier, paid plans from about Rs 499/month or US$14.99, an "Infinity" premium tier, and zero commission claims. The sources conflict and the official pricing page was not verified. — [SoftwareSuggest](https://www.softwaresuggest.com/dukaan/pricing); [Capterra](https://www.capterra.in/software/1012555/dukaan); [TechCrunch 2021](https://techcrunch.com/2021/09/05/dukaan-raises-11-million-to-help-merchants-in-india-set-up-online-stores)
- Zomato charges restaurants commission: roughly 27.8% of order value per a CCI probe report, and up to 37% for cloud kitchens. It also added a consumer "platform fee" in August 2023 at Rs 2 per order and later raised it in steps to about ₹14.9. It added a 2% pay-on-delivery fee capped at Rs 30, and the fee hikes drew customer backlash. — [Moneylife](https://moneylife.in/article/competition-commission-orders-probe-into-conduct-of-zomato-swiggy/66816.html); [Outlook Business](https://www.outlookbusiness.com/corporate/zomato-hikes-platform-fee-to-149-per-order-amid-cost-pressures); [Restaurant India](https://www.restaurantindia.in/news/zomato-adds-2-percent-pay-on-delivery-fee-raising-costs-for-customers.n17253); [Restaurant India backlash](https://www.restaurantindia.in/news/restaurant-india-news-zomato-fee-hike-sparks-customer-backlash-and-industry-debate.n15600)

### Inferences
- Copy the OkCredit split. A free shop tier sends messages on the shop's own channel, and paid tiers send through the platform. This keeps message cost on the shop's plan and never on the customer.
- Zomato shows the main pitfall: once consumer fees exist, they tend to creep upward. A public written "customers are free forever" commitment sets Grocery Khata apart, but it limits future options, so state it narrowly (no fee to view the khata, send lists or order).
- Network effect: one household can buy from several shops (kirana, dairy, medical). A "my shops" view where the customer invites another shop is the natural growth loop. It works because the customer side costs nothing.

### Gaps
- No sourced data was retrieved for Practo, Swiggy restaurant commission, Shopify, or measured customer-invites-shop referral rates. These need a follow-up search.
- OkCredit and Khatabook help pages do not say what customers see after a shop downgrades or deletes its account.

## What happens to customers when a shop lapses, pauses or leaves; DPDP roles

### Takeaway
Under DPDP, the shop is most likely the Data Fiduciary for its customers' khata data, and Grocery Khata is its Data Processor. The household's own account data (login, lists, multi-shop view) is a different case, where Grocery Khata is likely the fiduciary. Most substantive duties apply from about May 2027. A lapsed subscription should never cut a customer off from their balance.

### Cited Findings
- The DPDP Rules 2025 were notified on 13 Nov 2025 and published in the Gazette on 14 Nov 2025. — [JSA](https://www.jsalaw.com/wp-content/uploads/2025/11/JSA-Prism-InfoTech-November-2025-DPDP-Rules.Final_.pdf); [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)
- Commencement is phased. Rules 1, 2 and 17–21 took effect on publication, and Rule 4 (consent managers) after one year. Rules 3, 5–16, 22 and 23 (notice, security, breach, erasure, data-principal rights) start 18 months after publication, about May 2027 by calculation. — [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/); [EY](https://www.ey.com/content/dam/ey-unified-site/ey-com/en-in/alerts-hub/2025/11/digital-data-protection-act-rules-notified-by-meity.pdf)
- A Data Processor processes personal data on behalf of a Data Fiduciary. The fiduciary stays responsible for safeguards, including data the processor holds. Rule 6 safeguards must be written into processor contracts, and processors must follow the fiduciary's deletion instructions. — [Mondaq](https://www.mondaq.com/india/data-protection/1708164/digital-personal-data-protection-rules-2025-notified); [AMLegals](https://amlegals.com/digital-personal-data-protection-rules-2025/); [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)
- Rule 14 covers the rights to access, correction and erasure, plus grievance redressal "within a reasonable period". Rule 8 requires erasure once the purpose ends or the retention period passes, at least 48 hours' notice before erasure, and keeping logs for at least one year. — [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/); [Alvarez & Marsal](https://www.alvarezandmarsal.com/sites/default/files/2026-01/DPDPA%20POV_1911_PB_3.pdf)
- Rule 6 safeguards include encryption, access controls, logging and backups. Breaches must be reported to the Board and to affected principals without delay. Rule 10 requires verifiable parental consent for children. — [SCC Online](https://www.scconline.com/blog/post/2025/12/26/digital-personal-data-protection-rules-2025-key-highlights/)

### Inferences (design rules, not legal advice)
- **Lapsed or on hold:** keep the customer's read-only khata access (balance, statement, download) working, and keep accepting UPI payments. Pause only premium shop features and platform-sent messages. Locking customers out of a debt record they are part of is a trust and consumer-fairness risk.
- **Shop leaves or deletes:** the shop's instruction drives deletion, because we are its processor. Before erasure, send the customer a notice of at least 48 hours with a statement PDF (this mirrors Rule 8). Keep the minimum logs for one year. Accounting and GST retention laws may require longer retention; check this with counsel.
- **Customer-initiated erasure:** delete the household's own profile. For the shop's ledger entries, route the request to the shop as fiduciary, because the shop has a legitimate interest in an outstanding udhar record. Get counsel to confirm.
- Put fiduciary/processor clauses in the shop terms of service now, ahead of May 2027.

### Gaps
- No official guidance was found on fiduciary roles in multi-tenant khata SaaS, or on the exact Third Schedule retention classes. The PIB PDF could not be parsed.

## Costs of free customer features and how vendors cap them

### Takeaway
Messaging is the main variable cost. On WhatsApp, a utility message (entry or reminder) costs about ₹0.115 and a marketing message about ₹0.86, both before BSP fees and 18% GST. Vendors tie platform-sent messages to paid shop tiers.

### Cited Findings
- Meta has billed per message since 1 Jul 2025. India rates from 1 Jan 2026: marketing about ₹0.8631, utility and authentication about ₹0.115. Utility messages are free inside an open 24-hour service window. — [Meta pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing); [Authkey](https://authkey.io/blogs/?p=1840); [ecorpit](https://ecorpit.com/whatsapp-business-api-cost-india-d2c-2026/)
- BSP platform fees run about ₹999–16,999/month, plus 18% GST. AiSensy quotes about ₹1.09 per marketing message, which likely includes its markup (unverified). — [MyOperator](https://myoperator.com/blog/whatsapp-business-api-pricing-india-2026); [AiSensy](https://m.aisensy.com/blog/whatsapp-business-pricing/)
- OkCredit's free tier offloads SMS cost to the shop's own SIM. Only the ₹99 Premium tier gets unlimited platform SMS. — [OkCredit pricing](https://okcredit.in/pricing)

### Inferences
- Rough cost at 200 customers × 8 utility messages a month = 1,600 messages × ₹0.115 ≈ ₹184 + GST per shop per month. That is significant against a ₹99–299 plan. Give each plan a monthly message allowance, use free service-window replies (the customer messages first), and fall back to share links or deep links that open the shop's own WhatsApp.
- AI bill or list reading and storage: give each shop plan a quota for customer-submitted lists and photos (for example N reads a month). Compress images and set a retention period on images, while keeping the structured data.

### Gaps
- No sourced per-image AI costs or Indian SMS DLT rates were gathered in this pass.

## Fair-use limits and abuse prevention for free consumer accounts

### Takeaway
No vendor-published limits were found. Use controls standard for OTP-based apps.

### Cited Findings
- OkCredit signup needs only a mobile number. — [OkCredit](https://okcredit.in/khata-book-app-download)

### Inferences
- Customers can see a khata only after the shop links them (shop-initiated invite or OTP match). This blocks someone from enumerating other people's balances.
- Rate-limit OTPs, orders and lists per number per day. Add a per-shop daily cap on customer-triggered AI reads, charged to the shop's quota. Let the shop block a customer. Flag patterns like many shops being invited from one number.
- Do not let customers trigger paid marketing-category messages. Use only utility templates and service-window replies.

### Gaps
- No public fair-use policies were found from Khatabook, OkCredit or Dukaan for their customer side.

## Monetising without charging customers; pitfalls

### Takeaway
Proven routes are premium shop tiers (OkCredit and Khatabook), lending and payment partnerships (Khatabook), and higher store tiers that bundle marketing and search ranking (Dukaan Infinity). The pitfalls are fee creep onto consumers (Zomato), competition scrutiny of commissions, and lending regulation.

### Cited Findings
- Khatabook monetises through premium subscriptions, NBFC lending partnerships, payments and insurance cross-sell. A 1–3% loan commission is cited by a vendor blog and is unverified. — [Miracuves](https://miracuves.com/blog/khatabook-revenue-model/); [StartupTalky](https://startuptalky.com/khatabook-business-model/)
- Dukaan's premium tiers bundle marketing and search ranking. — [TechCrunch](https://techcrunch.com/2021/09/05/dukaan-raises-11-million-to-help-merchants-in-india-set-up-online-stores)
- The CCI ordered a probe into Zomato and Swiggy conduct over commissions and practices. — [Moneylife](https://moneylife.in/article/competition-commission-orders-probe-into-conduct-of-zomato-swiggy/66816.html)

### Inferences
- Options, ranked by fit: (1) premium shop tiers (AI agent, more messages, GST, multi-staff); (2) featured or "nearby shop" listing for new customers, which never ranks a customer's existing shops; (3) payments or lending via partners, which needs RBI-compliant partners (digital lending rules) and must never charge the payer; (4) supplier and brand ads to shops, never ads in the customer khata view.
- Pitfalls: ads or upsells inside the customer khata view erode trust. Selling or sharing customer data to lenders without consent breaches DPDP. Commission per order pushes shops to move customers off-platform.

### Gaps
- No primary sources were found for Shopify-style app-store or payments-commission comparisons in India, or for featured-listing pricing on hyperlocal apps.
