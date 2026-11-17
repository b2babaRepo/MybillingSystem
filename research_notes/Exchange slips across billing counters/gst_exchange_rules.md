# GST and accounting treatment of retail exchanges (India, 2026)

Scope caveat: done on a short budget (4 searches, no page fetches). Most findings below come from search-result summaries of secondary sources. Check the key points against the CGST Act/Rules text before anyone relies on them in a design decision.

## 1. Section 34 / Rule 53: when a credit note is needed, and whether a combined net bill is allowed

### Takeaway
Section 34(1) lets the supplier issue a credit note when goods are returned. The credit note must quote the original invoice, and it may carry GST only if issued by 30 Nov of the next FY (or by the annual return, if earlier). I found no source or provision that recognises one "net" document doing both the return and the new sale. The safe, standard pattern is a credit note against the old invoice plus a full tax invoice (s.31) for the new goods, with the payment settled net.

### Cited Findings
- Section 34 allows a registered person to issue a credit note where goods supplied are returned or found deficient, or where the taxable value or tax charged was too high. — [CBIC Section 34 text](https://taxinformation.cbic.gov.in/content/html/tax_repository/gst/acts/2017_CGST_act/active/chapter7/section34_v1.00.html); [TaxGuru on s.34](https://taxguru.in/goods-and-service-tax/debit-note-credit-note-section-34-cgst-act-2017.html)
- The credit note should reference the original invoice number it is issued against. — [TaxGuru](https://taxguru.in/?p=733586)
- Section 34 does not cover other cases such as invoice amendment or cancellation. A replacement or new invoice falls under the general invoicing rules (s.31). — [TaxGuru](https://taxguru.in/?p=733586)
- A credit note must be declared in the return for the month it is issued. The deadline is 30 Nov following the end of the FY (or the annual return, if earlier), in force since 1 Oct 2022 (Finance Act 2022). — [Vyapar TaxOne](https://taxone.vyapar.com/post/handling-credit-debit-notes-in-gstr1); [Pice](https://piceapp.com/blogs/time-limit-for-amendment-in-gstr-1/)
- The Finance Act 2026 reportedly amended s.15(3)(b) and s.34 to simplify credit notes for post-sale discounts. I have not verified this, and it concerns discounts, not returns. — [Tally GST update](https://tallysolutions.com/gst/gst-update-handling-post-sale-discounts-just-got-simpler/)
- Commentary separates GST credit notes, which reduce output tax, from "financial" credit notes, which do not adjust GST. — [Busy guide](https://busy.in/gst/guide-to-a-credit-note-under-gst.md)

### Inferences
- Rule 53(1A) (from memory, not re-fetched) lists the required credit note contents: the words "Credit Note", a serial number, the date, recipient details, the original invoice number and date, value, taxable value, rate, and the tax credited. A combined bill showing negative lines rarely meets all of these. It also mixes two document series: invoices under Rule 46 and credit notes under Rule 53.
- Software design: keep two linked documents, a credit note (return) and a tax invoice (new goods). The printed slip can still show "net payable / refundable" as one settlement figure. Rule 46A (combined invoice-cum-bill of supply) covers taxable plus exempt goods, not returns, so it does not help here.
- If the return comes after the 30 Nov cutoff, the GST cannot be reduced. Any refund or credit is then purely commercial (a financial credit note).
- An unregistered or composition dealer cannot issue tax credit notes. A composition dealer issues bills of supply, and returns are handled as a reduction of turnover.

### Gaps
- I found no CBIC circular or FAQ that addresses retail exchange billing directly, or that allows or forbids netting.
- I did not fetch the exact Rule 53 text. Confirm it against CBIC.

## 2. Exchange slips, store credit and vouchers (s.12(4), Circular 243/37/2024)

### Takeaway
Since Circular 243/37/2024-GST (31 Dec 2024), vouchers are actionable claims, which are neither goods nor services. GST arises only on the goods supplied when the voucher is redeemed. An exchange slip issued for returned goods is better treated as evidence of a credit note (the return reverses the original supply) than as a voucher sale.

### Cited Findings
- Circular 243/37/2024-GST treats vouchers as actionable claims (Schedule III entry 6), which are neither goods nor services. Prepaid-instrument vouchers count as money. GST applies to the underlying supply on redemption, and there is no GST on unredeemed vouchers (breakage). — [GST Council PDF](https://gstcouncil.gov.in/sites/default/files/2025-01/circular-no-243-2024.pdf); [VJM Global](https://www.vjmglobal.com/blog/clarification-on-applicability-of-gst-provisions-on-vouchers); [TaxGuru](https://taxguru.in/goods-and-service-tax/clarification-gst-treatment-vouchers-cbic.html)
- Under s.12(4), time of supply for a voucher is the issue date if the supply is identifiable then, and the redemption date otherwise. — [HNA research paper](https://hnallp.com/assets/articles/77dab-full-research-paper_-discount-under-gst_mb_ah_final_july25-2.pdf)
- Finance Act 2025 reportedly omitted s.12(4)/13(4), the voucher time-of-supply rules, effective from a notified date. I could not confirm this in this session (my recollection, unverified).

### Inferences
- On a return, the shop issues a credit note, which reduces output tax. If the customer takes a "store credit / exchange slip" instead of cash, that slip is a payment instrument or liability against the credit note. Spending it later is just payment toward a new tax invoice charged at full GST. The slip itself carries no GST.
- Do not tax the slip when it is issued. Do not reduce GST when it is redeemed. Unused slips create no GST, though they matter for the books (a liability, then income if written off).

### Gaps
- I found no source on whether an exchange slip that references the returned invoice is legally a credit note document. Treat the credit note as the GST document and the slip as a customer-facing token.

## 3. GSTR-1 reporting for B2C returns

### Takeaway
B2C small credit notes are netted into Table 7 (B2CS) by rate and place of supply. B2C large inter-state supplies (B2CL) are reported in Table 9B with the note type "unregistered". The cutoff is 30 Nov of the next FY.

### Cited Findings
- B2C credit notes go into GSTR-1 Table 7, net of the supplies. B2B credit notes go into Table 9B (registered). — [West Bengal Credit Note FAQ](https://comtax.wb.gov.in/GST/GST_FAQ/Credit_Note_in_GST.pdf); [Vyapar](https://taxone.vyapar.com/post/handling-credit-debit-notes-in-gstr1)
- Table 9B (unregistered) is used for credit notes against B2C large invoices (inter-state, above the B2CL threshold). — [Vyapar](https://taxone.vyapar.com/post/handling-credit-debit-notes-in-gstr1)
- The cutoff is 30 Nov of the following FY or the annual return, whichever is earlier. For FY 2025-26 that is 30 Nov 2026. — [TaxGuru](https://taxguru.in/?p=1047226); [Pice](https://piceapp.com/blogs/time-limit-for-amendment-in-gstr-1/)
- CBIC has consulted on an alternate amendment mechanism, proposed from the April 2027 period. This is a proposal only. — [A2Z Taxcorp](https://a2ztaxcorp.net/?p=41338)

### Inferences
- Software should keep credit notes with rate and place of supply so that the B2CS net by rate (and HSN Table 12 net of returns) can be computed. The B2CL threshold is Rs 1 lakh as of the 2024 change (my recollection, unverified).

### Gaps
- I did not fetch the current GSTR-1/IFF table instructions, and the 2025 GSTR-1 changes (HSN B2B/B2C split) were not checked.

## 4. Returned and new goods at different GST rates

### Takeaway
Each document carries its own rate. The credit note uses the rate on the original invoice, and the new invoice uses the rate in force on its date for that item's price. Net settlement is commercial only. Apparel became 5% up to Rs 2,500 per piece and 18% above it from 22 Sep 2025, replacing the earlier 5%/12% at a Rs 1,000 threshold.

### Cited Findings
- Apparel: up to Rs 2,500 per piece is 5%, above it is 18%, under Notification 9/2025-CT(Rate), effective 22 Sep 2025. The threshold is tested per piece, excluding tax. — [Accountune](https://accountune.com/gst-on-garments-above-2500); [Taxo.online](https://taxo.online/latest-articles/understanding-the-₹2500-slab-under-gst-2-0/); [Busy](https://busy.in/gst-rates/garments/)
- A credit note for a return carries the rate on the original invoice even after a rate change. For example, goods sold at 18% on 15 Sep 2025 and returned in October still get an 18% credit note. — [Tally](https://tallysolutions.com/smart-moves/expert-insights/gst-credit-debit-notes-rate-change-scenarios/); [CalcGuru](https://calcguru.in/?p=8289)

### Inferences
- A combined net bill cannot show one rate for the difference. For example, returning a Rs 2,000 shirt (5%) and taking a Rs 3,000 jacket (18%) means a credit note at 5% and an invoice at 18%. This strongly argues for separate documents, or at least separate rate-wise lines that mirror the credit note and invoice structure.
- If the exchange gives the customer a discount that pushes a piece's price to Rs 2,500 or below, the rate follows the actual transaction value on the new invoice.

### Gaps
- These are blog sources, so verify the notification text.

## 5. CA and software practice for small retailers

### Takeaway
Mainstream GST software (Tally, Vyapar, Busy, Zoho) models a return as a credit note or sales return voucher linked to the original invoice, and the new goods as a fresh sale. Payment is adjusted against the credit note. I found no authoritative CA source endorsing a single net invoice.

### Cited Findings
- Software guides treat sales returns as credit notes linked to the original invoice, reported in GSTR-1. — [Zoho Books](https://www.zoho.com/in/books/gst/credit-note.html); [Busy](https://busy.in/gst/guide-to-a-credit-note-under-gst.md); [Vyapar](https://taxone.vyapar.com/post/handling-credit-debit-notes-in-gstr1)

### Inferences
- Recommended model: an exchange creates (a) a credit note against the original bill, at the original rates, and (b) a new tax invoice. The balance is settled by collecting the difference, refunding it, or issuing an exchange slip (store credit) for the rest. A single print can show both with a net-payable line.
- An unbilled "even exchange" (same item and size, same price) can arguably be a replacement with no new supply. I found no source on this.

### Gaps
- I found no CA article specifically on POS exchange slips. Even-swap treatment is unconfirmed.
