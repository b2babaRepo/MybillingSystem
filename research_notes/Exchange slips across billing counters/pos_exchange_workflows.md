# POS exchange workflows (Indian and global retail billing software)

## Q1: One combined exchange bill (net) or a separate return/credit note followed by a new sale?

### Takeaway
There are two families. Global cloud POS products (Lightspeed, Square, Shopify with Pro) run one combined cart: return lines are negative, new lines positive, and the net is settled. Indian ERP/accounting tools (Tally, Zoho Inventory, Vyapar, Marg) treat the return as a separate GST document (credit note / sales return) that is then adjusted against a new invoice. Ginesys and Gofrugal support both a "return against bill" exchange inside the POS and a credit note carried forward.

### Cited Findings
- Ginesys POS: items can be exchanged only against the return of a bill item ("Return against Bill"), and no promotion or discount applies to the replacement item. — [Ginesys GPOS wiki: Manage Return Menu](https://ginesys.atlassian.net/wiki/spaces/GPOS/pages/3840017301/How+To+Manage+Return+Menu+in+Ginesys+POS)
- Ginesys POS: a credit note is taken as payment in a new POS bill. Create the bill, open Payments (F8), and choose the credit note as the mode of payment (MOP). The credit note needs its own document numbering scheme, customer tagging is mandatory, and the credit note MOP must be assigned to the POS. — [Ginesys: Take payment using a Credit Note](https://ginesys.atlassian.net/wiki/spaces/GPOS/pages/3840017716/How+To+Take+payment+using+a+Credit+Note)
- Gofrugal advises unique barcodes so return or exchange items load by scan on the "return or exchange screen". It mentions Return Redemption Notes (RRNs) for refunds, adjusted in future customer bills, and "auto credit notes". — [Gofrugal: customer return process](https://www.gofrugal.com/retail/billing-software/customer-return-process-retail.html); [Gofrugal features](https://www.gofrugal.com/retail/features)
- TallyPrime: returns are recorded as Credit Notes (kept in the Credit Note Register). For an exchange, you record the credit note and adjust it against the new sales invoice. These are two vouchers. — [Tally help: Sales return](https://help.tallysolutions.com/tally-prime/sales-process/sales-return-tally/)
- Zoho Inventory product replacement: create a sales return, then a credit note from it, then a new sales order or invoice for the replacement, then apply the credit to the new invoice to offset it. — [Zoho Inventory KB: product replacement](https://www.zoho.com/inventory/kb/sales-returns/product-replacement.html)
- Zoho POS (Oct 2025 release): a return can go toward an exchange, a refund, or store credit issued as a credit note. Only items marked Returnable can be returned. — [Zoho POS What's New Oct 2025](https://help.zoho.com/portal/en/community/topic/whats-new-in-zoho-pos-oct-2025?page=2); [Zoho POS: create sales return](https://www.zoho.com/en-in/pos/resources/help/create-sales-return.html)
- Vyapar: the return is entered under Sales > Sale Return / Credit Note with customer, date and original invoice number. Vyapar also offers an option to adjust a credit note against its specific invoice. — [Vyapar credit note format page](https://vyaparapp.in/credit-note-format)
- Marg ERP sale return has a "Multiple" mode: one sale return entry can cover items from several different bills and be adjusted against a single bill. — [Marg Care: sale return](https://care.margcompusoft.com/Question.aspx/28701/1/sale-return); [Marg Books sale return](https://care.margcompusoft.com/marg-book/sale-return/175301/9/what-is-the-process-of-sale-return-in-marg-books)
- Lightspeed Retail: you pull up the original sale, its items appear with negative quantity, you add the replacement as a positive line and press Exchange. This is one sale. — [Lightspeed X-Series support](https://x-series-support.lightspeedhq.com/hc/articles/25534278666267); [Lightspeed Retail support](https://retail-support.lightspeedhq.com/hc/en-us/articles/360020062234)
- Square for Retail: go to Transactions, find the sale, tap Return or Exchange. The cart nets the two sides: equal value is an even exchange, and if the replacement is cheaper the difference can be refunded to the card. — [Square help 6350](https://squareup.com/help/us/en/article/6350-process-a-return-or-exchange-with-square)
- Shopify POS: exchanges are described for POS Pro locations. Shopify also suggests refunding to store credit and then spending that credit on the new item. The refund can be split, with part to the original payment and part to store credit. — [Shopify help: exchanging an item](https://help.shopify.com/manual/sell-in-person/transactions/orders/exchange-item); [Shopify changelog: store credit on POS](https://changelog.shopify.com/posts/store-credit-is-now-available-on-pos)

### Inferences
- For an Indian GST-compliant design, the common pattern is a separate credit note (sales return) document that is linked to the original invoice, plus a new tax invoice that redeems it. A single "exchange bill" screen can produce both documents behind the scenes, which is what Ginesys and Gofrugal appear to do.

### Gaps
- Busy, Logic ERP and myBillBook: I found no vendor documentation on their exchange flow, so their behaviour is unverified.
- Gofrugal: I found no step-by-step manual page for its exchange screen.

## Q2: Is there an exchange slip or credit voucher with a number or barcode, and how is it redeemed and protected?

### Takeaway
Yes, among Indian retail POS products. Ginesys credit notes are numbered documents tied to a customer and protected by an OTP, an expiry and void status, and they can be centralised across stores. Gofrugal uses RRNs. In global POS products the credit is held in a customer's store-credit balance rather than on a paper slip.

### Cited Findings
- Ginesys: only credit notes that are valid, not expired and not voided are listed for redemption. — [Ginesys: Take payment using a Credit Note](https://ginesys.atlassian.net/wiki/spaces/GPOS/pages/3840017716/How+To+Take+payment+using+a+Credit+Note)
- Ginesys: an OTP is required to adjust a credit note for a POS customer. Policy changed so that the OTP is generated when the note is issued, not when it is redeemed. — [Ginesys blog: OTP policy](https://www.ginesys.in/blog/new-policy-for-auto-generation-of-otp-for-adjustment-of-credit-note-to-pos-customer); [Ginesys blog: credit note secured with OTP](https://www.ginesys.in/blog/credit-note-becomes-more-secured-with-ginesys-otp)
- Ginesys: a global (centralised) credit note can be redeemed at any store only after sync from the store to head office and from head office to the other stores. — [Ginesys blog: centralized credit note](https://www.ginesys.in/blog/ginesys-retail-erp-now-attributed-with-centralized-credit-note)
- Ginesys also has "Redemption Tokens" that are adjusted at the POS. — [Ginesys: Adjust a Redemption Token](https://ginesysone.atlassian.net/wiki/spaces/GPOS/pages/29199007/How+to+Adjust+a+Redemption+Token+in+GINESYS+POS)
- Generic POS pattern (SalesPlay, not one of the target products): the cashier prints a credit note receipt with an ID. On the new sale, the cashier picks Credit Note as the payment and types the ID. — [SalesPlay help](https://help.salesplaypos.com/help/how-to-exchange-products-and-use-credit-notes)
- Shopify: store credit sits on the customer profile, and the "Return to Store credit" option must be enabled. — [Shopify changelog](https://changelog.shopify.com/posts/store-credit-is-now-available-on-pos)

### Inferences
- Protection against double use in these products comes from server-side status (valid, expired, voided), a remaining balance and customer binding or an OTP, not from the slip itself. A barcode on the slip only speeds up lookup.

### Gaps
- I found no explicit documentation of a barcode printed on the credit note for scanning in Ginesys or Gofrugal. Gofrugal's barcode advice refers to item barcodes.
- I found no default expiry periods.

## Q3: How are partial redemptions, leftover balances and difference payments handled?

### Takeaway
The difference is settled at the payment step. If the new goods cost more, the customer pays the extra by any tender. If they cost less, the remainder is refunded (Square, to the card) or kept as credit (Shopify store credit, Ginesys credit note balance). Leftover balance handling has known bugs (Shopify).

### Cited Findings
- Square: if the replacement is cheaper, you can refund the difference to the customer's card. Equal value is an "even exchange". — [Square help 6350](https://squareup.com/help/us/en/article/6350-process-a-return-or-exchange-with-square)
- Shopify: the refund can be split between the original payment method and store credit that funds the new item. A community report says a leftover store-credit balance reset to zero after a discount, and the workaround was to process the return and the repurchase separately. This is an unofficial source. — [Shopify help](https://help.shopify.com/manual/sell-in-person/transactions/orders/exchange-item); [Shopify community thread](https://community.shopify.com/t/shopify-pos-exchange-issue-remaining-store-credit-balance-resets-to-zero-after-discount/621900)
- Ginesys: several credit notes can be adjusted together using "Add CR Note" (shown in the deposit refund flow). — [Ginesys: Refund deposit](https://ginesys.atlassian.net/wiki/spaces/GPOS/pages/3838346698/How+To+Refund+deposit+from+the+store)
- Lightspeed: when the sale was paid through Square more than 60 days ago, the return cannot be processed in Retail POS. — [Lightspeed X-Series](https://x-series-support.lightspeedhq.com/hc/articles/25534278666267)
- Zoho POS: a community user reports there is no direct option to refund a credit note to a card, so it is done offline. — [Zoho POS Oct 2025 thread](https://help.zoho.com/portal/en/community/topic/whats-new-in-zoho-pos-oct-2025?page=2)

### Gaps
- I did not confirm whether a Ginesys credit note can be partly redeemed with the balance kept on the same number, or whether a new note is issued.

## Q4: Are returns taken at a separate returns desk or at the billing counter?

### Takeaway
I found no vendor documentation that prescribes a separate returns desk. Gofrugal frames returns as a load on the billing counter and recommends barcode-driven return screens to keep billing fast. The credit-note-as-payment design (Ginesys, generic POS) technically lets one counter issue the slip and another redeem it.

### Cited Findings
- Gofrugal says returns, refunds and exchanges slow down billing, and suggests scanning barcodes into the return/exchange screen. — [Gofrugal](https://www.gofrugal.com/retail/billing-software/customer-return-process-retail.html)

### Gaps
- I found no source on separate exchange-desk practice in Indian apparel chains.
