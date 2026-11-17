# Multi-counter store practice: exchanges when the return desk and the billing counter differ

Scope note: I found no public SOP from any named Indian chain (Reliance Trends, Max, V-Mart, Pantaloons, Westside, Bata, D-Mart, Lifestyle) that describes the inside-the-store flow between desks. Policy pages, complaints and Indian retail POS vendor docs (Ginesys/Zwing, GoFrugal, Fynd) are the best evidence available. Everything else is inference and is marked as such.

## Typical flow: who inspects, what document is issued, when stock goes back

### Takeaway
Indian large-format apparel chains handle an in-store return by issuing a store **credit note (CN)** at the store, not cash. Validity is printed on the note and differs by chain: Max 90 days, Westside 6 months, and anecdotally about 1 year at Reliance Trends. POS systems post the CN to accounts and put the item back into that store's stock when the return is completed.

### Cited Findings
- Max Fashion (India help centre): a credit note is issued for the value of the exchanged item. It can be used at any Max store nationwide and is valid 90 days from the date of issue. Altered garments cannot be exchanged. — [Max Fashion help](https://helpin.maxfashion.com/support/solutions/articles/9000209608-how-can-i-exchange-a-product-) (the page returned 404 when fetched later; its content was taken from the search snippet)
- Westside: 30-day return window. An in-store return gets a "Credit Note" redeemable only at Westside stores, valid 6 months. — [Westside return policy](https://westside.com/pages/return-policy)
- Reliance Trends: no official policy page found. In one complaint the customer was told the CN was valid 1 year but it was refused at other Reliance stores. In another, a CN conditional on buying at the same store lapsed after about 3 months. An industry article says Trends offers both store credit and refunds. — [consumercomplaints.in](https://www.consumercomplaints.in/reliance-trends-credit-note-received-from-reliance-trends-not-being-used-c2933831); [Apparel Resources](https://in.apparelresources.com/?p=224045)
- Reliance Brands mono-brand stores (e.g. Armani Exchange) have a 6-month CN rule. This is a different company from Trends. — [Amex RBL T&C](https://www.americanexpress.com/content/dam/amex/in/benefits/rbl_platrcp_tc.pdf)
- Bata: an older consumer-case record quotes the cash memo as promising exchange within 15 days, and replacement within 90 days for major manufacturing defects. If the same product is out of stock, the value can be used on a later purchase at the same store. — [LiveLaw PDF](https://www.livelaw.in/pdf_upload/bata-538122.pdf)
- Pantaloons is listed among brands that refund as store credit, with a "threshold on returns". No day count was given. — [Apparel Resources](https://apparelresources.com/?p=300172)
- POS practice (GoFrugal): the POS creates credit notes on return and posts them to accounts automatically. Refunds are issued as "Return Redemption Notes", adjustable against future bills. The exchange value is based on the returned item's value. Real-time stock adjustment happens on return. — [GoFrugal](https://www.gofrugal.com/retail/billing-software/customer-return-process-retail.html)
- Fynd Store OS: return steps are open order, select item quantity and reason, choose refund method (credit note is one of four), complete. For self-pickup orders, returns are accepted only at the ordering store and items go back into that store's inventory. Only admin or store-manager users can start these returns. CNs are printable and usable for future purchases. — [Fynd docs](https://documentation.fynd.com/commerce/docs/store-os/pos/returns-and-refunds)
- Uniqlo India (offline): the store issues a "refund slip", and card refunds reach the account within 14 working days of getting it. — [Uniqlo India FAQ](https://faq-in.uniqlo.com/articles/en_US/FAQ/How-can-I-return-my-purchase-from-an-Offline-store)

### Inferences
- Fields the CN must carry (implied by the controls described): a unique CN number (printed or barcoded), amount, issue date and expiry, the store, and a link to the original bill. Ginesys tags a mobile number to each CN, and BFL checks the phone on the CN against the customer account.
- Stock re-entry happens when the return transaction is saved at the desk, not later when the CN is redeemed.

### Gaps
- No public document shows the printed layout of a Trends, Max or Westside CN. No published policy found for V-Mart, D-Mart or Lifestyle India.
- No source says whether a sales associate or the returns-desk cashier does the physical inspection.

## At the billing counter: how the slip is redeemed

### Takeaway
At the billing counter the CN is a **tender (payment mode)**. The cashier enters or scans the CN number, the POS checks it against the central record, and it can be combined with cash, card, voucher or loyalty points on the same bill. Higher-control systems send an OTP to the mobile number tagged to the CN. If the new bill is less than the CN, what happens to the balance is a per-system setting.

### Cited Findings
- Ginesys: a genuine mobile number must be recorded when the CN is generated, and an OTP is sent to that number at redemption. One OTP can cover several redemptions on the same invoice (CN, gift voucher, loyalty points). — [Ginesys blog](https://www.ginesys.in/blog/credit-note-becomes-more-secured-with-ginesys-otp)
- Max says its CN works at any Max store nationwide. Westside says Westside stores only. Reliance Trends complaints show it refused at other stores. Cross-store acceptance therefore varies by chain. — sources above
- Fynd: CNs "can be reused for future purchases". Partial use is possible there, so one-time use has to be a deliberate configuration choice. — [Fynd docs](https://documentation.fynd.com/commerce/docs/store-os/pos/returns-and-refunds)
- Cin7: credit is stored with an expiry period set in POS settings, and the customer keeps the receipt as proof. — [Cin7](https://help.omni.cin7.com/hc/en-us/articles/9128593549455-Issue-and-redeem-store-credits)
- Expired or lost CN (BFL Group, Gulf): a CN cannot be reissued or replaced. An unused expired or lost CN can be moved to the customer's wallet after checking the phone match and confirming in the system that it is unused. Any active CN must first be deactivated by IT, and a manager approves. — [BFL Kapture KB](https://knowledge-bfleu.kapturecrm.com/article-detail.html?cont_id=97)
- Bata gift vouchers: no CN or refund is given for the unused amount, so partial use loses the rest. — [IndusInd Bata T&C](https://indusind.com/in/en/personal/tnc-bata.html)

### Inferences
- The common pattern is: the CN number identifies a record in the central system, and redemption reduces that record's balance or closes it. Two designs exist. In one, the CN is fully consumed and any balance becomes a new CN. In the other, the CN keeps a running balance.

### Gaps
- No Indian chain publicly states whether the leftover balance gets a fresh CN or the original CN carries the balance.

## Same cashier does both the return and the new sale: one combined bill or a separate slip?

### Takeaway
Modern POS systems (Shopify POS and the Indian vendors) support a single **exchange transaction**: return lines and new lines go on one bill, and only the difference is collected or refunded. A separate CN is needed when the new purchase is billed later or at another counter, or when the customer takes credit instead.

### Cited Findings
- Shopify POS: select the returned items and the new items together. If a balance remains, tap Refund or Collect; if nothing remains, tap Exchange. — [Shopify help](https://help.shopify.com/en/manual/sell-in-person/shopify-pos/order-management/exchange-refund)
- POS v11.4 processes returns and exchanges directly in the cart, with sale and return lines mixed (third-party summary). — [PowerCommerce](https://powercommerce.com/blogs/shopify-updates/shopify-pos-v11-4-returns-refunds-and-exchanges-now-processed-directly-in-the-cart-what-retailers-need-to-know)
- Known pitfalls: one merchant reported that when the new items cost less than the return, the leftover is not carried forward and has to be re-entered as store credit by hand. Another reported a limit of one exchange per transaction. — [Shopify Community](https://community.shopify.com/t/shopify-pos-exchange-issue-remaining-store-credit-balance-resets-to-zero-after-discount/621900); [Shopify Community](https://community.shopify.com/t/why-cant-i-perform-multiple-exchanges-per-transaction-in-my-retail-business/34813)
- GoFrugal recommends dedicated return counters, kept separate from billing counters, so returns do not block the billing queue. That separation is what creates the need for a CN handed between counters. — [GoFrugal](https://www.gofrugal.com/retail/billing-software/customer-return-process-retail.html)

### Inferences
- A practical design is: same counter, one exchange bill showing the net amount. Different counter, or purchase later: CN issued at the desk and redeemed as a tender at the billing counter.

### Gaps
- No Indian chain SOP was found confirming which mode its stores use at the counter.

## Controls: approvals, double use, cash vs credit, reconciliation

### Takeaway
The main controls are:
- validating the CN against the central system at the moment of redemption, with OTP to a tagged mobile number
- limiting who may process returns, by user role or manager level
- giving credit only, not cash (Westside, Max)
- watching for override-heavy or return-heavy staff

Public material does not cover end-of-day reconciliation of open CNs counter by counter.

### Cited Findings
- SAP KB: one POS did not validate store credit numbers on redemption, so "any store credit number can be entered". This is the classic failure that central validation prevents. — [SAP KB 1882033](https://userapps.support.sap.com/sap/support/knowledge/en/1882033)
- A USPTO patent describes checking voucher validity at the POS to stop over-redemption, with a one-time code good only for its amount. — [USPTO 6754636](https://image-ppubs.uspto.gov/dirsearch-public/print/downloadPdf/6754636)
- A Square developer thread describes the hard part as binding a credit to store, customer and amount, and settling it exactly once across tills running at the same time. It is an open question in the thread, not a solution. — [Square forum](https://developer.squareup.com/forums/t/supported-native-pos-authorization-and-order-evidence-for-externally-tracked-store-credit-discounts/28473.md)
- Fynd: only admin or store-manager users can start self-pickup returns. Bank refunds need merchant approval and may need a customer OTP. — [Fynd docs](https://documentation.fynd.com/commerce/docs/store-os/pos/returns-and-refunds)
- GoFrugal: user-based access limits who can make counter entries. It recommends return-pattern analysis (frequency, categories, time from purchase to return). It also, notably, recommends training staff to complete returns "without consulting managers". — [GoFrugal](https://www.gofrugal.com/retail/billing-software/customer-return-process-retail.html)
- Insider fraud warning signs include a specific employee processing unusually many returns, heavy use of override codes, and off-hours processing. — [Red Stag Fulfillment](https://redstagfulfillment.com/return-fraud/)
- NRF/Appriss figures (via secondary sources): about $101B of return fraud in 2023. Over 25% of retailers saw fraud with counterfeit paper receipts, against 16% with e-receipts. — [Mastercard insight](https://www.mastercard.com/mt/en/news-and-trends/Insights/2024/return-fraud--the--100-billion-problem-facing-retailers.html); [Red Stag](https://redstagfulfillment.com/return-fraud/). Figures differ between secondary sources.
- Store credit abuse can be cut by requiring a receipt or ID, capping store credit issued, and monitoring transaction data. — [Red Stag](https://redstagfulfillment.com/return-fraud/)

### Inferences
- Double use, including at two counters at the same time, is prevented only if the CN status is checked and set to redeemed in one central, atomic step at tender time. Paper-only slips without a central lookup are the weak point.
- Credit-only rules (Westside, Max) remove the cash-out incentive behind fake returns.
- A daily register of open CNs is a natural accounting by-product, since CNs post as liabilities (GoFrugal says they post to accounts automatically). No retailer publicly describes a per-counter reconciliation.

### Gaps
- No public source on supervisor-approval thresholds (for example, CNs above a set amount) at Indian chains.
- No source on end-of-day CN reconciliation per counter.
- Gulf/BFL and US data are global comparison only.
