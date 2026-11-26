# Compliance, Payments and Hardware Requirements for a Supermarket / Mall-Store POS in India

Research date: 26 Nov 2026. Budget: 15 tool calls (searches plus one failed fetch). Most findings come from search-result extracts, not full-text reads of the notifications, so exact rule wording should be checked against the CBIC/GSTN/Legal Metrology primary texts before it goes into product copy. Items marked **[background, unverified this session]** come from prior knowledge and could not be re-sourced within the budget. They belong in Gaps for verification.

## GST: invoices, e-invoicing, B2C dynamic QR, GSTR-1/HSN, composition

### Takeaway
A retail POS must produce a Rule 46 tax invoice, a Rule 49 bill of supply (exempt goods or composition dealers), or a Rule 46A invoice-cum-bill of supply for mixed baskets to unregistered buyers. For B2C sales below ₹50,000 the customer's name and address are optional. If the retailer's AATO exceeds ₹5 crore it must generate an IRN (e-invoice) for B2B invoices. If AATO is ₹10 crore or more, that IRN must be reported within 30 days (from 1 Apr 2025). If aggregate turnover exceeds ₹500 crore, every B2C invoice must carry a dynamic QR code (Notification 14/2020-CT, effective 1 Dec 2020 via Notification 71/2020-CT). GSTR-1 Table 12 needs HSN-wise summaries (4 digits up to ₹5 crore AATO, 6 digits above), split B2B/B2C and chosen from a dropdown under Phase-III (2025).

### Cited Findings
**Invoice types and contents**
- Rule 46 (tax invoice): for an unregistered recipient, the invoice must show the recipient's name, delivery address, State name and State code where the taxable supply is ₹50,000 or more. Clause (f) covers supplies below ₹50,000 where the recipient asks for these details. — [CBIC Rule 46](https://taxinformation.cbic.gov.in/content/html/tax_repository/gst/rules/cgst_rules/active/chapter6/rule46_v1.00.html)
- Rule 49: a bill of supply under Section 31(3)(c) is issued for exempt supplies and by composition-scheme taxpayers, and the provisos to Rule 46 apply to it. — [CBIC invoice rules](https://cbic-gst.gov.in/gst-invoice-rules.html); [ICAI Navi Mumbai](https://navimumbai.icai.org/detail/28853.aspx)
- Rule 46A: a registered person supplying both taxable and exempt goods to an unregistered person may issue one "invoice-cum-bill of supply" carrying the particulars of Rule 46/54 and Rule 49. This is the usual case for a supermarket basket mixing taxed goods with exempt items such as fresh vegetables or loose unbranded staples. — [CBIC invoice rules](https://cbic-gst.gov.in/gst-invoice-rules.html)
- Section 31(3)(b): where each supply is below ₹200 and the recipient is unregistered and does not ask for an invoice, the supplier may issue one consolidated tax invoice at the end of the day. — [ICMAI GST deck](https://icmai.in/upload/Taxation/Courses/PPT/ACCGST12_0609_2024.pdf) (exact rule wording not verified; it sits in the Rule 46 proviso)
- GST Council flyer on tax invoices and other documents. — [GST Council flyer](https://www.gstcouncil.gov.in/sites/default/files/e-version-gst-flyers/Tax_Invoice_and_other_new.pdf)

**E-invoicing (IRN)**
- The e-invoicing threshold was cut in phases, from ₹500 crore (Oct 2020) to ₹5 crore from 1 Aug 2023. — [Tally threshold guide](https://tallysolutions.com/gst/e-invoicing-threshold/); [Tally 2026 rules](https://tallysolutions.com/business-guides/e-invoicing-compliance-rules-in-2026/)
- The GSTN advisory of 5 Nov 2024 lowered the AATO threshold for the 30-day IRP reporting limit from ₹100 crore to ₹10 crore, effective 1 Apr 2025. The limit also covers credit and debit notes, and the IRP rejects documents older than 30 days. — [GSTN advisory (MahaGST copy)](https://www.mahagst.gov.in/public/uploads/gstnadvisory/1760531580_293%20Advisory%20Time%20Limit%20for%20Reporting%20e%20Invoice%20on%20the%20IRP%20Portal%20%20Lowering%20of%20Threshold%20to%20AATO%2010%20Crores%20and%20Above.pdf); [Taxmann](https://www.taxmann.com/post/blog/time-limit-of-30-days-for-reporting-e-invoice-on-irp-portal-for-taxpayers-with-aato-of-10-crores-and-above-gstn-update/); [Business Today](https://businesstoday.in/personal-finance/tax/story/gst-rule-change-from-april-2025-taxpayers-need-to-upload-e-invoices-within-30-days-of-issuance-453138-2024-11-09)
- One secondary source says businesses below ₹10 crore don't need to generate e-invoices. Other sources contradict it: the ₹5 crore threshold still applies to IRN generation, and only the 30-day hard stop starts at ₹10 crore. Treat ₹5 crore as the IRN threshold. — [Xflowpay](https://www.xflowpay.com/blog/e-invoice-limit) (the outlier) vs [Tally](https://tallysolutions.com/gst/e-invoicing-threshold/)

**B2C dynamic QR code**
- The B2C dynamic QR code applies to tax invoices issued to unregistered persons by registered persons whose aggregate turnover exceeds ₹500 crore in any FY from 2017-18 onwards. It took effect 1 Dec 2020 under Notification 71/2020-CT (deferred from 1 Oct 2020). Export invoices are excluded. — [Tally](https://tallysolutions.com/gst/dynamic-quick-response-qr-code-on-b2c-invoices); [Avalara](https://www.avalara.com/blog/en/apac/2021/05/everything-you-need-to-know-about-dynamic-qr-code-under-b2c-eInvoicing.html)
- The penalty for non-compliance was waived until 31 Mar 2021. Sources disagree on when it began to bite (1 Apr 2021 vs 1 Oct 2021). Section 125 is cited as the ₹25,000 general penalty. — [BloombergQuint](https://www.bloombergquint.com/business/govt-waives-penalty-for-non-compliance-with-qr-code-provisions-for-b2c-transactions-till-mar-21); [Deccan Herald](https://www.deccanherald.com/business/govt-waives-penalty-for-non-compliance-with-qr-code-provisions-for-b2c-transactions-921640.html)
- CBIC Circular 146/02/2021 (QR code FAQs): a UPI dynamic QR on the invoice, or a cross-reference to a payment made by UPI, card or cash, can satisfy the requirement. — [Circular 146](https://gstcouncil.gov.in/sites/default/files/2024-06/circular_refund_146.pdf); Circular 156/12/2021 has further clarifications — [Circular 156](https://gstcouncil.gov.in/sites/default/files/2024-06/156-12-2021_20gst_20circular.pdf)
- A UPI-based dynamic QR on the B2C invoice carries the supplier GSTIN, UPI ID, bank account and IFSC, invoice number and date, total value and the GST split. — [Tally UPI QR](https://tallysolutions.com/gst/upi-based-dynamic-qr-code-on-b2c-invoices); [Razorpay](https://razorpay.com/blog/how-to-generate-gst-dynamic-qr-codes-with-razorpay/); [Cashfree](https://cashfree.com/blog/dynamic-gst-qr-codes/)
- Searching for a 2025–26 change to the ₹500 crore threshold found none. Status as of late 2026 looks unchanged at ₹500 crore, but that is unconfirmed.

**GSTR-1 data and HSN digits**
- Notification 78/2020-CT (15 Oct 2020): minimum HSN digits in GSTR-1 Table 12 are 4 for AATO up to ₹5 crore and 6 for AATO above ₹5 crore. — [GSTN advisory on Table 12/13](https://www.mahagst.gov.in/public/uploads/gstnadvisory/1760596735_345%20Reporting%20of%20HSN%20codes%20in%20Table%2012%20and%20list%20of%20documents%20in%20table%2013%20of%20GSTR%201%201A.pdf); [IndiaFilings](https://www.indiafilings.com/learn/hsn-code-reporting-in-gstr1)
- Phase-III of Table 12 removes manual HSN entry: codes are picked from a dropdown, and Table 12 is split into separate B2B and B2C tabs. Table 13 (documents issued: invoice series ranges, cancelled counts) is mandatory. Sources conflict on the effective date: an advisory dated 11 Apr 2025 is reported as applying from the April 2025 tax period ([Taxmann](https://www.taxmann.com/post/blog/gstn-issued-new-advisory-on-table-12-of-gstr-1-or-gstr-1a-manual-entry-of-hsn-will-not-be-allowed/)) or from the May 2025 tax period ([CAclubindia](https://www.caclubindia.com/guide/reporting-of-hsn-codes-in-gstr-1/); [Taxscan](https://www.taxscan.in/phase-3-of-reporting-of-hsn-codes-in-table-12-of-gstr-1-1a-from-may-2025-onwards-gstn-issues-advisory/513246)). An earlier report says it applies from returns filed for the Jan/Feb 2025 periods. — [TaxGuru](https://taxguru.in/goods-and-service-tax/gstn-implements-phase-iii-table-12-gstr-1-1a-april-2025.html)

### Inferences
- POS data model: every item master needs an HSN code (6 digits by default is safe for all sizes), GST rate, cess, a taxable/exempt/nil/non-GST flag, and UQC (unit quantity code) for Table 12.
- POS data model: every bill needs a document series per counter or register (for Table 13, which requires unique, sequential invoice numbers of up to 16 characters per FY), a B2B/B2C flag, the buyer GSTIN where given, place of supply, and the taxable value with CGST/SGST/IGST split.
- B2B sales at a supermarket counter (a business buyer quoting a GSTIN) by a retailer above ₹5 crore AATO need a real-time IRN call through an IRP/GSP API, with the IRN and signed QR printed on the invoice. B2C bills do not need an IRN.
- For chains above ₹500 crore (large supermarket chains and big mall brands), the B2C dynamic QR is mandatory on every bill. In practice that means a UPI dynamic QR printed on the receipt, or a printed payment cross-reference when the bill is paid by card or cash, per Circular 146.
- Composition dealers (small kirana or franchisee stores) must issue bills of supply, cannot collect tax, and must print "composition taxable person, not eligible to collect tax on supplies" **[background, unverified this session]**. The POS needs a composition mode that suppresses the tax lines.

### Gaps
- No full-text confirmation of a 2025–26 B2C e-invoicing pilot or any change to the ₹500 crore B2C QR threshold.
- Composition scheme limits (₹1.5 crore for goods, ₹75 lakh in special-category states) and the mandatory bill-of-supply legend were not re-sourced this session.
- Record retention: Section 36 CGST requires accounts to be kept for 72 months from the due date of the annual return **[background, unverified this session]**. Verify at the CBIC Section 36 page.
- E-invoicing exemptions (SEZ units, banks, GTA and others) were not checked for retail relevance.

## Legal Metrology: weighing scales, MRP, loose goods

### Takeaway
Any scale used in trade must be verified and stamped by the state Legal Metrology department, with periodic re-verification and a fee. Using an unverified or non-standard weight is an offence under Section 25 of the Legal Metrology Act 2009. Under the Packaged Commodities Rules 2011, pre-packaged goods cannot be sold above the declared MRP, and the 2022 amendments made unit sale price declaration mandatory on packages. The POS should therefore block any selling price above MRP for packaged SKUs and keep price-per-kg/unit data for loose and weighed items.

### Cited Findings
- Legal Metrology Act 2009: Chapter IV covers verification and stamping of weights and measures (Section 24), and Chapter V covers offences, starting with Section 25 (use of non-standard weight or measure). "Weight or measure" includes weighing instruments. — [Act text (India Code)](https://www.indiacode.nic.in/bitstream/123456789/6927/1/legal_metrology_act_2009.pdf)
- Offences under Sections 25, 27–31, 33–37, 45–47 are compoundable. A Delhi schedule lists compounding amounts, but the extracted figures were garbled. — [Delhi W&M penalties](https://weightnmeasures.delhi.gov.in/sites/default/files/inline-files/penalties.pdf)
- State departments stamp all weights and measures in commercial use periodically for a stamping fee, and traders must present instruments for verification on the date the Inspector sets. — [Haryana Legal Metrology](https://haryanafood.gov.in/?p=14418)
- During inspection, violations lead to a seizure receipt and possible prosecution of the retail or wholesale dealer. — [HP Weights & Measures](https://hpwm.hp.gov.in/en-US/Download/Index/70)
- Packaged Commodities Rules 2011: packaged commodities cannot be sold above MRP. — [TaxGuru commentary](https://taxguru.in/?p=37794); [Rules text (THC)](https://www.thc.nic.in/Central%20Governmental%20Rules/Legal%20Metrology%20(Packaged%20Commodities)%20Rules,2011.pdf); consolidated text amended up to 13 Feb 2026: [Khaitan & Co](https://www.khaitanco.com/sites/default/files/2026-03/Legal%20Metrology%20%28Packaged%20Commodities%29%20Rules%202011.pdf)
- The 2021–22 amendments (MoCA, PIB 2021) introduced mandatory unit sale price declaration (per kg/litre/unit) and MRP in INR inclusive of all taxes, and dropped the standard-pack-size schedule. They took effect 1 Apr 2022, with enforcement of the labelling changes extended to 1 Oct 2022. A later amendment exempts unit sale price where the retail sale price equals the unit sale price (rule 6(11)). — [PIB 1770064](https://pib.gov.in/PressReleasePage.aspx?PRID=1770064); [Zee Biz](https://zeebiz.com/india/news-centre-amends-packaged-commodities-rules-2011-for-enhanced-protection-of-consumer-rights-to-change-from-april-1-2022-170152); [Lexplosion](https://lexplosion.in/amendments-to-legal-metrology-packaged-commodities-rules-2022-kick-in-extends-timeline-to-comply-with-new-labelling-requirements-till-01-10-2022/); [Goa GSR 226](https://clm.goa.gov.in/uploads/GSR226.pdf)
- MoCA has issued a Legal Metrology (QR code) notification allowing some declarations via QR code. — [MoCA QR notification](https://consumeraffairs.nic.in/sites/default/files/uploads/legal-metrology-acts-rules/Notification%20-%20%20Legal%20Metrology%20(QR%20Code).pdf)

### Inferences
- POS rules to enforce: (1) a hard block or warning when price exceeds MRP for packaged SKUs; (2) storing MRP per batch, since the same EAN can carry different MRPs; (3) loose goods sold by weight, with price per kg × net weight and the weight shown on the bill.
- In-store packed items (labelled by a barcode scale) are "pre-packed" if packed in the customer's absence, which triggers PC Rules labelling: name, net quantity, MRP, unit sale price, packed date, packer address. Label templates on barcode scales need these fields.
- Scales must be approved models, such as Essae or Avery models with Legal Metrology model approval, and must carry a current verification stamp. The POS vendor should not let the software recalibrate or alter weight readings.

### Gaps
- Re-verification periodicity for weighing instruments (commonly 1 year, but set by state rules) and the stamping fee schedule were not sourced.
- Section 25 penalty amounts (fine and imprisonment) were not extracted.
- No specific state notice on labelling of loose goods at counters was found. Whether loose goods sold in front of the customer need any display (for example, price per kg at the counter) was not confirmed.

## Payments: terminal integration, costs, MDR, UPI QR, cash limits

### Takeaway
Terminal providers integrate in three ways: (1) cloud APIs, where the billing app posts the amount and the terminal picks it up (Pine Labs Plutus Cloud); (2) local wired, serial or USB, or LAN integration (Pine Labs wired and local APIs); (3) app-to-app on an Android POS that runs the billing app and payment app together (Pine Labs POS Lib: setConfiguration/doTransaction). Rentals run about ₹200–800 per month. Card MDR for grocery and supermarket is around 1.3% (Razorpay published rate). Zero-MDR policy changed in 2026: a Finance Ministry notification (S.O. 5067(E), 14 Sep 2026, under the amended Section 10A of the PSS Act) now keeps UPI up to ₹2,000 and RuPay debit cards charge-free, opening the door to MDR on UPI above ₹2,000. Section 269ST bars receiving ₹2 lakh or more in cash from one person in a day, per transaction, or per event, with a 100% penalty under Section 271DA on the recipient.

### Cited Findings
**Integration APIs**
- Pine Labs Plutus cloud integration: the billing app on any device calls a Pine Labs-hosted web API to push the transaction. The terminal picks it up, or the cashier enters the Plutus reference ID, and the app polls status by reference ID. Device IMEIs are provisioned in Pine Labs' UI, and the merchant needs a Plutus subscription. — [Pine Labs cloud integration](https://developer.pinelabs.com/in/instore/cloud-integration); [Zenoti help](https://help.zenoti.com/en/integrations/plutus.html)
- Pine Labs also documents wired integration (serial or USB), local integration APIs including Linux, server-to-server integration, and app-to-app on Android using POS Lib methods setConfiguration, getConfiguration and doTransaction. — [Wired](https://developer.pinelabs.com/in/instore/wired-integration); [Local APIs](https://developer.pinelabs.com/in/instore/local-integration-apis); [Linux](https://developer.pinelabs.com/in/instore/local-integration-linux); [App-to-app](https://developer.pinelabs.com/in/instore/app-integration); [Server](https://developer.pinelabs.com/in/instore/server-integration); [Billing integrations](https://www.pinelabs.com/use-cases/drive-efficiency-billing-integrations)
- There is no official Pine Labs app on the Shopify App Store, so integration with that platform is custom. — [Shopify community](https://community.shopify.com/t/help-integrating-pine-labs-pos-with-shopify-security-token-provided/563510)
- Razorpay POS documents Android Smart POS and Android Smart Mini POS devices, plus DigiPOS, which turns a phone or tablet into a terminal. — [Razorpay POS docs](https://razorpay.com/docs/pos?preferred-country=IN); [Android Smart POS](https://razorpay.com/docs/pos/android-smart-pos); [Smart Mini POS](https://razorpay.com/docs/pos/android-smart-mini-pos.md)
- PhonePe POS accepts UPI, cards and wallets, with reconciliation in the PhonePe Business App. PhonePe says advanced terminals connect to billing software but publishes no API detail. — [PhonePe POS](https://www.phonepe.com/pos-machine/); [PhonePe EDC article](https://business.phonepe.com/articles/what-is-an-edc-machine-understanding-types-charges-how-it-works)

**Costs and MDR**
- Razorpay POS monthly plans: ₹1,000 installation; rental ₹425/month (Android Smart POS) and ₹325/month (Smart Mini POS). Card MDR is 1.30% for grocery/supermarket, 1.10% for utility/government/education/fuel/insurance/transport, and 1.85% for others. — [Razorpay POS charges](https://razorpay.com/blog/pos-machine-charges/)
- PhonePe says EDC rentals typically run ₹200–500 per month. — [PhonePe](https://business.phonepe.com/articles/what-is-an-edc-machine-understanding-types-charges-how-it-works)
- ICICI EazyPay standard fixed rental: ₹700/month for APOS (IMSL/Ezetap) and Pine Labs Platinum APOS, and ₹800/month for Paytm APOS. RuPay credit card on UPI carries 1.90% + GST MDR. — [ICICI schedule of charges](https://www.icicibank.com/business-banking/cash-management-services/eazypay-schedule-of-charges)
- Zero MDR on UPI applies at the bank level, but aggregators may charge "platform fees" (Razorpay's 2% on online UPI is a platform fee, not MDR). — [Razorpay cheapest PG blog](https://razorpay.com/blog/cheapest-payment-gateway-for-businesses-under-%E2%82%B925-lakh-monthly-gmv-how-to-compare-real-costs/)
- **2026 change:** the Taxation and Other Laws (Amendment) Act 2026 amended Section 10A of the Payment and Settlement Systems Act 2007, effective 17 Aug 2026. Instead of a blanket zero charge, the government now specifies which modes are charge-free. The DFS gazette notification S.O. 5067(E) of 14 Sep 2026 names RuPay debit cards and UPI transactions up to ₹2,000. Sources differ on whether UPI above ₹2,000 now carries MDR: one says the notification itself sets no MDR, another says it "could" face MDR. — [Business Today 14 Sep 2026](https://www.businesstoday.in/latest/economy/story/upi-stays-free-finance-ministry-bars-charges-on-transactions-up-to-rs-2000-555475-2026-09-14); [NewKerala](https://www.newkerala.com/news/a/govt-notifies-zero-charges-upi-up-rs-2000-670.htm); [Moneylife](https://www.moneylife.in/article/upi-transactions-above-2000-could-face-mdr-as-govt-redraws-nocharge-rule/81648.html); [Outlook Business](https://www.outlookbusiness.com/news/no-charges-on-upi-transactions-up-to-2000-govt-yet-to-decide-on-mdr-for-higher-value-payments); [Inc42](https://inc42.com/buzz/govt-notifies-new-rules-no-mdr-for-upi-transactions-under-%e2%82%b92000/); [Supreme Today](https://supremetoday.ai/centre-notifies-statutory-ban-on-upi-charges-up-to-rs-2000-and-rupay-debit-cards-20260915080); [The Wire](https://thewire.in/banking/finance-ministry-notification-sets-stage-for-charges-on-upi-transactions-above-rs-2000) (the Fox Mandal analysis, which reportedly carries the S.O. number, could not be fetched because the host was blocked)

**Cash (Section 269ST)**
- Section 269ST bars receiving ₹2 lakh or more (a) in aggregate from one person in a day, (b) for a single transaction, or (c) for transactions relating to one event or occasion, other than by account-payee cheque, bank draft or electronic modes. It has applied from 1 Apr 2017. — [Income Tax Dept, 269ST](https://www.incometaxindia.gov.in/w/section-269st); [modes of receipts](https://www.incometaxindia.gov.in/w/mode-of-receipts-and-payments)
- Section 271DA: the penalty equals the amount received, levied on the recipient. — [Income Tax Dept, 271DA](https://www.incometaxindia.gov.in/w/section-271da-8); [TaxGuru examples](https://taxguru.in/income-tax/rs-2-lakh-cash-transaction-limit-wef-01apr2017-details-examples.html)
- Splitting one bill across days to stay under ₹2 lakh still breaches limb (b). Commentators disagree on whether several sub-₹2 lakh bills to the same person in one day breach limb (a). The plain text, which aggregates from one person per day, suggests they do. — [TaxGuru](https://taxguru.in/income-tax/rs-2-lakh-cash-transaction-limit-wef-01apr2017-details-examples.html)

### Inferences
- POS features needed: (1) a terminal abstraction layer with drivers for cloud-API terminals (Pine Labs Plutus Cloud, Razorpay/Ezetap, Paytm, PhonePe, Mswipe, BharatPe) and app-to-app intents on Android POS; (2) push the amount and bill reference to the terminal and receive the auth code, RRN, last 4 digits and card network back to print on the bill; (3) a split-tender UI (cash + card + UPI).
- Dynamic UPI QR on the customer-facing display: generate a upi://pay intent with the payee VPA, amount, and bill number as transaction reference, and confirm via the PSP's webhook or status API, or through soundbox confirmation. Because of the 2026 Section 10A change, the POS should record UPI transactions above ₹2,000 separately so MDR can be reconciled if acquirers start charging it.
- Cash-limit guard: warn or block at checkout when cash tendered by one customer reaches ₹2 lakh in a day. This needs the customer phone number or ID for aggregation.
- Reconciliation: match POS tender lines to acquirer settlement files by TID/MID, RRN and date, and account for MDR and GST on MDR.

### Gaps
- No public API documentation was found for Paytm EDC, PhonePe POS, Mswipe or BharatPe billing-software integration. Their developer or partner portals and sales teams need direct contact.
- Outright purchase prices for Pine Labs, Paytm and Mswipe terminals were not found. Rental figures above are the best available.
- Soundbox pricing (Paytm, PhonePe SmartSpeaker, BharatPe) was not sourced. Verify RuPay credit-card MDR on POS and current card MDR caps (RBI's debit-card MDR framework).
- How the post-14 Sep 2026 regime is being applied (actual MDR on UPI above ₹2,000) was not confirmed.

## Data and security: DPDP, PCI-DSS, retention

### Takeaway
DPDP Rules 2025 were notified on 13/14 Nov 2025 with an 18-month phase-in: consent-manager provisions take effect around Nov 2026, and core data-fiduciary duties (notice, consent, security safeguards, breach notification, erasure) around May 2027. A POS that collects customer phone numbers or loyalty data must have consent notices, purpose limitation, deletion and breach processes ready by then. Keeping card data out of the POS by using P2PE/EMV terminals that return only tokens and masked PANs minimises PCI-DSS scope.

### Cited Findings
- DPDP Rules 2025 were notified on 13 Nov 2025 (gazette) or 14 Nov 2025 (PIB). Rules 1, 2 and 17–21 took effect immediately. Rule 4 (consent managers) takes effect about 12 months later (around 13 Nov 2026). Most substantive obligations take effect 18 months later (around 13 May 2027). The SPDI Rules 2011 continue in the interim. — [PIB document](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2025/nov/doc20251117695301.pdf); [YourStory](https://yourstory.com/2025/11/digital-personal-data-protection-dpdp-rules-2025-rollout-over-18-months); [Legal500/Saga Legal](https://www.legal500.com/firms/237811-saga-legal/c-india/news-and-developments/the-privacy-timer-starts-now-analysing-the-dpdp-rules-and-its-18-month-countdown); [Mondaq](https://www.mondaq.com/india/privacy-protection/1708314/update-indias-digital-personal-data-protection-framework-comes-into-effect); [Chambers](https://chambers.com/articles/s-r-data-india-s-digital-personal-data-protection-regime-takes-effect)
- Experts flag that businesses need data mapping and consent systems. — [Outlook Business](https://www.outlookbusiness.com/deeptech/tech/govt-notifies-dpdp-rules-experts-flag-need-for-data-mapping-consent-systems)

### Inferences
- Loyalty and phone capture: show an itemised notice (what is collected and why) at enrolment, for example on the customer display or by SMS link. Record consent with a timestamp, offer withdrawal and erasure, and avoid making a phone number mandatory for a sale.
- Conflict to design for: GST records must be kept (72 months, see GST Gaps), but marketing or loyalty data must be erased when the purpose ends. Store the invoice buyer fields (needed only for B2B, or B2C ≥ ₹50,000) separately from the loyalty profile.
- PCI-DSS: if the POS never sees the PAN (integrated EMV terminal does the card read and returns a token or masked PAN), the merchant can typically use a reduced SAQ (P2PE or B-IP style) **[background, unverified this session]**. Never log full track or PAN data from terminal responses.

### Gaps
- Exact DPDP Rules text (breach notification within 72 hours to the Board, the retention rules in Third Schedule, which applies mainly to large platforms) was not read in full this session. Verify against the MeitY gazette.
- No PCI SSC India-specific source was fetched. RBI tokenisation rules (CoF) are mostly an online-card concern and were not researched.

## Hardware: costs (INR) and device interfaces

### Takeaway
A full supermarket counter (PC or touch POS, 3" thermal printer, 2D scanner, cash drawer, customer display) costs roughly ₹35,000–75,000+. A tablet-plus-printer-plus-drawer setup costs about ₹8,000–17,000 extra. Barcode label scales cost about ₹28,000–44,000. Android all-in-one desktop POS units (Sunmi T2 class) run around ₹36,000–38,000, and handheld PAX A910 units around ₹12,000–12,500.

### Cited Findings
- Cash drawer: Essae HS-410 ₹3,900; generic ₹3,500 — [IndiaMART POS peripherals](https://m.indiamart.com/impcat/pos-peripheral.html); Rugtek CR410 ₹5,180 — [Supreme India](https://supremeindia.com/pages/pos-solutions-chennai)
- 3" thermal receipt printers: Rugtek RP82 ₹6,042 — [Supreme India](https://supremeindia.com/pages/pos-solutions-chennai); HPRT TP80K ₹9,900; Epson TM-T82X ₹11,390 — [IndiaMART](https://m.indiamart.com/impcat/pos-peripheral.html)
- Barcode scanners: Rugtek LS-3002 2D ₹2,750; Havsol SC-2018 ₹2,800; Honeywell Voyager 1452g 2D wireless ₹14,500 — [IndiaMART](https://m.indiamart.com/impcat/pos-peripheral.html); [Supreme India](https://supremeindia.com/pages/pos-solutions-chennai)
- Bundle estimates: printer + cash drawer with an existing tablet or laptop costs ₹8,000–17,000. An "advanced setup" (desktop terminal + heavy-duty scanner + drawer + software) costs ₹35,000–75,000+. Confirm whether dealer prices include GST and shipping. — [Razorpay retail POS guide](https://razorpay.com/blog/retail-pos-system-guide-india/); [Techimply](https://www.techimply.com/blog/pos-billing-machine-for-retail-shops)
- Barcode label printing scales: Essae SI-810 family listed at ₹28,000 and ₹44,253 (TradeIndia) and SI-810PR at ₹40,000 (IndiaMART). The Essae DS-852 is a plain weighing scale, not a label printer. — [TradeIndia barcode label scales](https://www.tradeindia.com/products/barcode-label-printer-scales-c6084729.html); [IndiaMART SI 810PR](https://m.indiamart.com/adcodigitalsystems/printing-scale.html); [IndiaMART label scales](https://m.indiamart.com/impcat/label-printing-scales.html)
- Android POS: Sunmi T2 POS Lite ₹37,900; Sunmi T2 with 3" printer ₹36,000 — [IndiaMART Sunmi](https://m.indiamart.com/impcat/sunmi-billing-machines.html); [IndiaMART T2 Lite](https://m.indiamart.com/proddetail/sunmi-t2-pos-lite-display-27374297233.html). PAX A910 ₹12,000, A910S ₹12,500 — [Acural IndiaMART](https://m.indiamart.com/acural/android-pos-machine.html). Sunmi T2s listing — [POSCentral](https://www.poscentral.in/sunmi-t2s-smart-desktop-pos-terminal.html); Sunmi V2 handheld — [Moglix](https://www.moglix.com/sunmi-v2-android-handheld-pos-terminal/mp/msnnkqg6ppdv9x)

### Inferences (device interfaces) **[background, unverified this session]**
- Receipt printers speak ESC/POS over USB (printer class or vendor), serial or USB-serial, Ethernet (raw TCP port 9100) or Bluetooth SPP. The cash drawer opens via an RJ11/RJ12 kick from the printer (ESC p command), so the POS never talks to the drawer directly.
- Barcode scanners default to USB HID keyboard emulation (no driver needed; the app receives keystrokes ending in Enter). Some offer USB-COM/serial mode for reliable parsing. 2D scanners are needed for UPI QR and GS1 DataMatrix.
- Weighing scales: for live weight at the POS, a serial (RS-232) or USB-serial protocol that is vendor-specific (Essae, Avery). For pre-packed items, the scale prints EAN-13 labels with a "price-embedded" or "weight-embedded" prefix (commonly prefix 2x + PLU + price/weight + check digit). The POS needs a configurable parser for these barcodes, and PLU masters are pushed to the scale via vendor software or LAN.
- Browser PWA: Web Serial and WebUSB are available only in Chromium-based browsers (Chrome/Edge desktop, with limited Android support), not Safari or Firefox. A common pattern is a local print/bridge agent (localhost WebSocket) or QZ Tray-style helper, or running on Android POS with native SDKs (the Sunmi built-in printer has its own SDK/AIDL service).
- Customer display: a 2-line VFD pole display over serial (Epson ESC/POS-compatible command sets), or a secondary HDMI screen on dual-screen Android POS (Sunmi T2), which can show a dynamic UPI QR.

### Gaps
- No price was found for customer displays, label printers (TSC, Zebra, TVS), Avery scales or Pine Labs Android POS for outright purchase.
- No primary source on Web Serial/WebUSB browser support was fetched this session (check MDN or Chrome Platform Status).

## Mall operator technical requirements for tenants

### Takeaway
Indian malls typically take daily tenant sales data (for revenue-share or turnover rent) through "ADSR" (All Day Sales Report) integrations offered by POS vendors, or a scoped read-only sales-data access. No public standard or API spec from Indian mall operators was found. The Singapore and Malaysia GTO-sync systems are the closest documented model.

### Cited Findings
- GoFrugal offers a licensed "ADSR Mall Integration" add-on that gives the mall owner access to sales data only, for commission or revenue-share calculation. — [GoFrugal ADSR](https://community.gofrugal.com/portal/en/kb/articles/adsr-mall-integration-license-sync-in-rpos)
- QueueBuster and VasyERP advertise ADSR mall integrations that collect and report daily sales from outlets within malls. — [QueueBuster ADSR](https://queuebuster.co/pos-software-partners/adsr); [VasyERP mall billing](https://vasyerp.com/retail/shopping-mall-billing-software)
- In Singapore and Malaysia, leases require daily sales submission to the mall or its agent for gross-turnover (GTO) rent, and tenants who miss submissions risk estimated or higher rent. The integration needs a mall-specific tenant or machine ID (for example CapitaLand "Machine ID") and scheduled transmission. — [HitPay mall GTO](https://docs.hitpayapp.com/pos/mall-gto); [EasyStore](https://blog.easystore.co/en-my/shopping-mall-gto); [SHOPLINE](https://help.shopline.com/hc/en-001/articles/18237228352537-Automated-GTO-Transmission-for-Malls)

### Inferences
- The POS should support a pluggable "mall connector": a daily or hourly push of bill-level or summary sales (gross, discounts, returns, net, tax, tender mix) via SFTP, CSV, API or a read-only DB view, keyed by a mall tenant ID. Each mall's lease annexure will define the format.

### Gaps
- No Indian mall operator (DLF, Phoenix Mills, Nexus, Lulu) published tenant IT or network rules or an API spec. These are in lease annexures and fit-out manuals that are not public.
