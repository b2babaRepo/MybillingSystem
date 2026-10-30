# Shop onboarding, KYC and legal fields for a paid shop subscription (India, 2026)

Research date: 2026-10-30. 7 web searches; no full page fetches, so all citations are from search snippets. Mostly secondary sources (law-firm notes, vendor docs). Check each claim against the primary text before building on it.

## 1. What do comparable apps (Vyapar, myBillBook, Khatabook, OkCredit, Zoho, Marg) ask at sign-up and at first payment?

### Takeaway
The pattern is a phone number plus OTP, then the app is usable at once. GSTIN, state, pincode and address are optional profile fields you add later in "Business settings", or when you first create a GST invoice. I found no evidence that any of these apps ask for PAN, bank details or a UPI ID at sign-up.

### Cited Findings
- OkCredit: you download the app and sign up with your mobile number via OTP. Its FAQ says "no forms, no documents" are needed. Logging in with the same number on a new phone restores your data. — [OkCredit FAQ](https://okcredit.in/faq)
- OkCredit names its target segments (kirana/grocery, medical stores, wholesalers and distributors), but I found no documented step where you pick a category during sign-up. — [OkCredit FAQ](https://okcredit.in/faq)
- myBillBook's quick setup guide puts contact details first, then "Configure GST details (if applicable)", then the business address. GST number is added later under Settings > Manage Business. — [myBillBook Quick setup guide](https://knowledge.mybillbook.in/articles/8812717-quick-setup-guide); [myBillBook GST bill help](https://knowledge.mybillbook.in/articles/5367766-how-to-create-a-gst-bill-using-mybillbook)
- Vyapar: you click the Business Name in the bottom-left corner and add Business GSTIN, State, Pin Code and Address. This is a profile step after sign-up. — [Vyapar GST guide](https://vyaparapp.in/guides/how-to-manage-gst-compliance-and-taxation-in-vyapar-app)

### Inferences
- A good minimum for Grocery Khata at sign-up: phone (OTP-verified), owner name, shop name, shop type (kirana/cement/paint) and state. State drives GST place of supply (section 2). Pincode is optional but cheap to ask and can fill in the state.
- Ask for GSTIN, legal name and billing address only when the shop starts a paid plan, and only if it wants a B2B invoice that lets it claim input tax credit.
- If you use hosted checkout, the UPI ID and bank details are collected by the payment gateway, not by us (section 3).

### Gaps
- I could not confirm the current sign-up screens for Khatabook, Zoho Books/Zoho Billing or Marg, or the exact required and optional fields in Vyapar and myBillBook. The searches returned nothing usable. Installing the apps would settle this.
- I did not find out what any of these apps collect at their own subscription checkout (for example Vyapar Premium).

## 2. GST invoice to the shop for the subscription: B2B vs B2C, place of supply, rate and SAC

### Takeaway
We charge 18% GST. For a shop without GSTIN (B2C), the invoice must show the recipient's **state name at any invoice value**. That state counts as the address on record for place of supply, which decides CGST+SGST vs IGST. For a B2B invoice we need the shop's GSTIN plus legal name and address. The SAC code is **not** settled: 998431 looks wrong for SaaS.

### Cited Findings
- CGST Rule 46(f) proviso: for an unregistered recipient, irrespective of the value of the supply, the tax invoice must contain the name of the recipient's state. That state is deemed to be the address on record. — [CBIC Rule 46 text](https://taxinformation.cbic.gov.in/content/html/tax_repository/gst/rules/cgst_rules/active/chapter6/rule46_v1.00.html); [GSTZen Rule 46](https://gstzen.in/a/tax-invoice-cgst-rule-46.html)
- OLDER, superseded: the earlier wording asked for name/address details only below ₹50,000 and only if the recipient requested it. Do not rely on it. — [Teachoo Rule 46](https://www.teachoo.com/6456/2155/Rule-46-GST---Tax-invoice/category/Chapter-6-Tax-Invoice--Credit-and-Debit-Notes-Rules-(Rule-46-to-56)/)
- Circular 242/36/2024-GST (31 Dec 2024) says suppliers of online services to unregistered recipients must record the recipient's state on the invoice. That state is then used to decide place of supply. — [GST Council circular PDF](https://www.gstcouncil.gov.in/sites/default/files/2025-01/circular-no-242-2024.pdf); [TaxGuru summary](https://taxguru.in/goods-and-service-tax/clarification-place-supply-online-services-unregistered-recipients.html)
- Rate: blog sources consistently put SaaS at 18% GST (CGST 9% + SGST 9% within a state, or IGST 18% across states). — [India Briefing](https://india-briefing.com/news/gst-compliance-for-saas-and-cloud-computing-in-india-explained-39021.html/); [IncorpX](https://www.incorpx.io/blog/saas-company-gst-billing-place-of-supply-export)
- SAC conflict: sources point SaaS to group 9983, with mentions of 998315 (hosting/IT infrastructure) and 998314 (IT design/development). 998431 shows up as online news/content and streaming, not SaaS. — [India Briefing](https://india-briefing.com/news/gst-compliance-for-saas-and-cloud-computing-in-india-explained-39021.html/); [Kanakkupillai](https://www.kanakkupillai.com/learn/gst-for-saas-companies-in-india/)

### Inferences
- Field rules: **state is mandatory for every paying shop**. GSTIN is optional. If the shop gives a GSTIN, issue a B2B invoice with the GSTIN, legal name and the address from the GSTIN record; the state comes from the first 2 digits of the GSTIN. Invoices also need a serial number, date, SAC, taxable value and the tax split.
- Once we are GST-registered, B2B invoices must go into GSTR-1 so the shop can claim input tax credit. That is a reason to validate the GSTIN (section 5).
- Have a CA confirm the SAC code. 998313/998314/998315 are the candidates; I could not verify any of them against the rate notification.

### Gaps
- I did not read Notification 11/2017-CT(Rate) itself.
- Not covered: when we must register for GST ourselves (the usual services threshold is ₹20 lakh turnover, ₹10 lakh in special-category states; unverified here), and whether we have e-invoicing duties (these depend on our turnover).

## 3. KYC when using UPI AutoPay / e-mandate via Razorpay or Cashfree

### Takeaway
KYC sits mainly on **our** side: we, the merchant, complete gateway KYC (company docs, PAN, bank account) and get an MCC. The shop paying us only approves the mandate in its UPI app with the UPI PIN or bank authentication. The gateway and bank handle that, so we do not need to collect the shop's bank or UPI details ourselves.

### Cited Findings
- Razorpay's default UPI AutoPay maximum mandate is ₹15,000 per transaction, or ₹1,00,000 for certain MCCs. The MCC assigned at merchant onboarding sets both the maximum mandate and the per-debit threshold below which no extra authentication (AFA) is needed. Debits above that need the customer's UPI PIN. — [Razorpay UPI recurring docs](https://razorpay.com/docs/payments/payment-gateway/s2s-integration/recurring-payments/upi)
- OLDER figure: a Chargebee doc gives ₹5,000 as the default UPI mandate maximum. This looks outdated. — [Chargebee Razorpay e-mandate](https://chargebee.com/docs/razorpay-rbi-e-mandate.html)
- Razorpay tells merchants to confirm with their account manager that UPI AutoPay, cards, eMandate and NACH are enabled on the account. — [Razorpay recurring overview](https://razorpay.com/docs/payments/recurring-payments/overview/integrate)
- Cashfree offers UPI AutoPay for recurring collection. — [Cashfree UPI AutoPay](https://www.cashfree.com/en/upi-autopay/)

### Inferences
- A kirana subscription will be far below ₹15,000/month, so renewals should debit silently after the first approval.
- The gateway's subscription/customer object typically needs only name, phone and email. Email may be optional; check per gateway. We should store only the gateway's customer, mandate and subscription IDs, not the shop's VPA or bank account.

### Gaps
- I found no source on Razorpay's or Cashfree's exact merchant KYC document list, or on any end-customer KYC rules for mandates.
- The RBI e-mandate pre-debit notification rule (24 hours before each debit, normally sent by the gateway) was not verified in this pass.

## 4. DPDP Act 2023 / DPDP Rules 2025: notice, consent, minimisation, retention

### Takeaway
The Rules were notified in November 2025 and come into force in stages. Most substantive duties apply from about **May 2027**, with some from November 2026. Grocery Khata needs a plain-language notice in English and local languages, consent tied to a stated purpose, collection of only what the subscription needs, and deletion when the purpose ends. Laws that require us to keep records, such as the GST and Income Tax Acts for invoices and payment records, override deletion.

### Cited Findings
- The Rules were notified around 13–14 Nov 2025 and start in phases: 14 Nov 2025, 14 Nov 2026 and 13/14 May 2027. Most substantive obligations fall in the 18-month phase. — [AMS Shardul](https://www.amsshardul.com/insight/enforcement-of-the-dpdp-act-and-notification-of-the-dpdp-rules/); [JSA](https://www.jsalaw.com/wp-content/uploads/2025/11/JSA-Prism-InfoTech-November-2025-DPDP-Rules.Final_.pdf); [Mondaq](https://www.mondaq.com/data-retention-and-deletion-under-the-dpdp-act-how-long-can-businesses-retain-personal-data-in-india/1844062)
- The fixed rule of erasure after 3 years of inactivity applies only to large classes: e-commerce, gaming and social media platforms with 2 crore or more users. It requires 48 hours' notice before erasure. Retention required by other laws (for example tax records) overrides erasure. — [Mondaq](https://www.mondaq.com/data-retention-and-deletion-under-the-dpdp-act-how-long-can-businesses-retain-personal-data-in-india/1844062); [KS&K](https://ksandk.com/md/data-protection-and-data-privacy/data-retention-deletion-dpdp-act-india/)
- Overviews of the Rules' requirements on consent notices (itemised data, purposes, how to withdraw consent and how to complain) and on keeping logs: — [EY](https://www.ey.com/en_in/insights/cybersecurity/transforming-data-privacy-digital-personal-data-protection-rules-2025); [KPMG](https://assets.kpmg.com/content/dam/kpmgsites/in/pdf/2025/11/dpdp-rules-2025-guidance-to-dpdp-act-implementation.pdf)

### Inferences
- Our data is mostly the shop owner's personal data: phone, name, and possibly a proprietor's PAN/GSTIN, since a sole proprietor's GSTIN is linked to a person. It is also customers' khata data, which we process for the shop; the shop is arguably the data fiduciary for its customers' data and we act as its processor.
- Retention after closure: delete or anonymise the profile and khata data when the purpose ends, after a short grace period for data export. Keep subscription invoices and payment records for the GST and Income Tax retention periods. CGST s.36 is commonly cited as about 72 months after the annual return due date, but I did not verify it here.
- Do not collect Aadhaar, shop photos or PAN unless there is a stated purpose. Collecting less lowers both DPDP risk and drop-off at sign-up.

### Gaps
- I did not read the Gazette text. The exact dates differ by a day between sources, and I did not check the precise notice contents in Rule 3 or the logging and retention duty for traffic data (reported as one year).

## 5. Fraud / fake-shop checks and acceptable friction for kirana owners

### Takeaway
Market-leading khata apps rely on phone OTP alone ("no forms, no documents"). Heavier checks such as GSTIN or PAN verification APIs are cheap and exist off the shelf. They are best used only when a shop supplies a GSTIN or PAN for its B2B invoice, not as a gate for every shop.

### Cited Findings
- OkCredit onboarding is a mobile number plus OTP with no documents. — [OkCredit FAQ](https://okcredit.in/faq)
- Cashfree's GSTIN verification API returns legal name, registration type, registration date, registered address and GSTIN status. Its PAN-to-GSTIN API lists every GSTIN tied to a PAN. Its PAN verification API returns the registered name and PAN type. There is a sandbox. — [Cashfree GST verification](https://www.cashfree.com/gst-verification/); [Cashfree PAN-to-GSTIN docs](https://www.cashfree.com/docs/secure-id/know-your-business/pan-to-gstin); [Cashfree PAN verification](https://www.cashfree.com/PAN-verification/)
- A claim, made only by Cashfree's comparison page, says Razorpay has a free manual GST search but no standalone PAN verification API. — [Cashfree comparison](https://www.cashfree.com/gst-search-by-pan/)

### Inferences
- Suggested layers: (1) OTP plus rate limits on each number and device. (2) Check the GSTIN checksum and that its first 2 digits match the chosen state. If the GSTIN is given, call a verification API, prefill the legal name and address, and reject cancelled GSTINs. (3) Successful mandate set-up is a strong real-payer signal, so let gateway risk checks do the work. (4) Ask for a shop photo or geo-location only for manual review or high-risk flags, not by default.
- Because the subscription is paid, the main fraud risk is free-trial abuse (many phones, one shop), not money loss. Limiting each GSTIN or UPI mandate to one trial addresses it.

### Gaps
- I found no published data on sign-up drop-off from extra KYC steps for kirana users, and none on which checks Khatabook, Vyapar or myBillBook use against fake shops.
- I did not look up the pricing of verification APIs.
