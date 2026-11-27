# Jewellery winning strategy: codebase status (audit of 24 Nov 2026)

Sources are repo files (paths relative to C:/Users/Ravi Mishra/source/repos/Grocery_app). B = backend/src/GroceryKhata.Api, F = frontend/src.

## Overall: what is committed, tests, half-built

### Takeaway
All jewellery work is committed and merged (PR #6). `git status` shows only `B/appsettings.Development.json` modified plus untracked reports, notes, marketing and deploy folders. Nothing jewellery-related is uncommitted.

### Cited Findings
- Commit history for `B/Ledger/Jewellery.cs`: phase 1 (2325911), phase 2 (f5a3be9), phase 3 karigar/scales/trays (1e347d5), phase 4 reports (eb2733a), owner-only cash report (f8f20d8), "quick wins" (35b6c1d: silver grades, HUID QR, old gold vouchers, rate sharing, due list, booked rates, book report, staff discount limit), gram schemes (7ca7971), girvi compounding, top-ups, ticket and tagged pieces online (71c8fbb). Source: `git log`.
- Backend files: `B/Ledger/Jewellery.cs` (840 lines: rates, pieces, quote, bills, settings), `JewelleryBook.cs` (703: girvi, schemes, orders, approvals), `JewelleryWorkshop.cs` (554: karigar parties, metal moves, loose metal, trays, scales), `JewelleryReports.cs` (163: stock, sales, cash), `OldMetalVouchers.cs` (138), `CashLimits.cs`.
- Frontend: `F/screens/JewelleryScreen.tsx`, `JewelleryTagsScreen.tsx`, `JewelleryTodayPanel.tsx`, `JewelleryBookPanels.tsx`, `JewelleryWorkshopPanels.tsx`, `JewelleryReportsPanel.tsx`, `JewelleryBookReport.tsx`, `OldGoldPanel.tsx`, `GirviTicket.tsx`, `F/components/MetalRates.tsx`, `JewelleryBreakdown.tsx`, `StorePieces.tsx`, `F/lib/jewellery.ts`, `F/lib/scale.ts`.
- Tests: backend `JewelleryTests.cs` (12 Fact/Theory), `JewelleryBookTests.cs` (8), `JewelleryWorkshopTests.cs` (4), `JewelleryReportTests.cs` (3), and some in `ShopDayTests.cs`. Frontend `F/lib/rateMessage.test.ts`, `F/lib/scale.test.ts`, `F/screens/JewelleryBookReport.test.ts`. That is 27 backend test methods (Theories may expand). There are no frontend tests for the Today panel, girvi ticket or old gold panel.

### Inferences
- Test coverage is thin for the money logic: 8 tests cover girvi compounding, schemes, orders and approvals together.

### Gaps
- I did not run the tests, so pass/fail status is unknown.

## Pillar 1: compliance (HUID, QR, URD vouchers, s.269ST, PAN) and plan items 1–8

### Takeaway
Most of Pillar 1 is built. HUID works for gold and silver (IS 2112:2025 silver grades). The printed bill carries per-HUID QR codes with BIS CARE wording. Standalone URD vouchers exist. Jewellery bills block cash at ₹2 lakh per person per day and require PAN/Form 60 at ₹2 lakh. Items 1–8 of the "Jewellery software features India" plan all shipped in commit 35b6c1d. What is missing: the QR holds only the raw HUID (no hosted verify link), the QR goes on the bill rather than the tag, and the cash-limit check is not applied to girvi or scheme cash.

### Cited Findings
- Silver HUID: `JewelleryMath.SilverPurities = [999, 990, 970, 958, 925, 900, 800]`, with the doc comment "a hallmarked silver piece carries a HUID like gold" — `B/Ledger/Jewellery.cs` ~l.249. Gold purities are 999/995/916/750/585.
- QR: `F/components/JewelleryBreakdown.tsx` l.66–73 renders "A QR per hallmarked piece holding its HUID, for the customer to scan and check in the BIS CARE app". The QR payload is just the HUID string (`QRCode.toDataURL(huid)`). The i18n string `huidVerify` exists in en/hi/gu (`F/i18n/enMore.ts` l.706, `hiMore.ts` l.708, `guMore.ts` l.367).
- Tags: `F/screens/JewelleryTagsScreen.tsx` l.35 prints the text "HUID xxx" on the dumbbell tag. A grep found no QR on the tag in that file. The tag code is `J{tagNo}` (`F/lib/jewellery.ts` `tagCode`).
- URD purchase: `B/Ledger/OldMetalVouchers.cs` handles "Old gold or silver bought from someone without a sale (URD purchase)", paid in cash, UPI or bank. The metal joins loose metal. Endpoints: `GET/POST /old-metal-vouchers` and `GET /old-metal-vouchers/{id}`. It requires PAN/Form 60 from ₹2 lakh (l.96) and warns when cash is above ₹10,000 (s.40A(3)) (l.49, 63). UI: `F/screens/OldGoldPanel.tsx`.
- s.269ST: `B/Ledger/CashLimits.cs` has `PerPersonPerDay = 200_000` and `GET /api/v1/customers/{id}/cash-today`. Its doc comment says "The app only warns" for ordinary bills, which are served by `F/components/CashLimitNote.tsx` and used in BillScreen, CustomerScreen and AttachCustomer. Jewellery bills hard-block instead: `Jewellery.cs` l.687 computes `CashLimit - 1 - cashToday`, and l.763–765 rejects with "Cash of ₹2 lakh or more from one person in a day is not allowed". `CashTodayAsync` counts the cash part of bills plus cash khata payments, so scheme instalments and order advances (which are khata payments) are included.
- PAN: `JewelleryBill.Pan` (l.89–90) holds "PAN, or FORM 60 … needed from ₹2 lakh". It is validated by `PanPattern` (l.760–762) and is mandatory when `q.PanNeeded`.
- Plan items 1–8 ("reports/Jewellery software features India.md" l.56–62 show items 5–8): 5 due-date reminders, 6 rate booking on orders (`JewelleryOrder.FixedRate`), 7 small reports (girvi book, scheme liability: `JewelleryBookReport.tsx`), 8 staff discount caps (`F/screens/StaffCapSetting.tsx`). Commit 35b6c1d's message lists silver grades, HUID QR, old gold vouchers, rate sharing, due list, booked rates, book report and staff discount limit, which covers all 8.

### Inferences
- Missing: (a) a QR on the tag itself, about 0.5 day; (b) a hosted public "verify" page per piece that deep-links to BIS CARE, plus optional shop and piece details, about 2–3 days; (c) a cash check on girvi repayments in cash (`GirviPayAsync` does not call CashLimits). Girvi lending in cash may fall under s.269SS/269T (₹20,000 loan rules), which are not modelled. That is about 1 day, plus a CA check.
- The s.269ST threshold is hard-coded as a constant, not server config, even though the report recommends server-side rules. Making it configurable is about 1 day.

### Gaps
- I did not see items 1–4 verbatim in the report (I only read l.55–83). The mapping of 1–4 comes from the commit message.
- No HUID-missing warning tied to a hallmark district list was found (Phase 4 of "reports/Jewelry shop management in India.md"). It appears unbuilt.

## Pillar 2: daily rates, rate card, morning action list, reminders

### Takeaway
Daily rate entry and a WhatsApp rate message exist. A "Due today" list with WhatsApp buttons exists for girvi, schemes, orders and approvals, in en/hi/gu. The share is text, not a generated image. There is no server push for jewellery dues.

### Cited Findings
- Rates: `GET/POST /rates` (`Jewellery.cs` l.325–326). `MetalRate` holds sellPerGram, buyPerGram and derived (`F/lib/jewellery.ts` l.10). Unset purities are derived from 999 (`JewelleryMath.Derive`). UI: `F/components/MetalRates.tsx`.
- Rate share: `rateMessage(rates, title, note, silverLabel)` in `F/lib/jewellery.ts` l.19 is tested in `rateMessage.test.ts`. It builds text. A grep found no canvas or image generation in MetalRates.tsx. An image pipeline does exist elsewhere (`F/lib/posters.ts`, `F/screens/PostersScreen.tsx`).
- Morning list: `useDueToday` in `F/screens/JewelleryTodayPanel.tsx` builds girvi due/overdue items (l.26–28), scheme instalments due or missed (l.31–33), orders that are ready or late (l.36–38) and late approvals (l.43–44). Each row has a `whatsAppLink(phone, message)` button (l.63). The messages are i18n keys `jwl5.*`.
- Existing push reminders: `B/Planner/KhataDues.cs` runs an hourly hosted loop that acts after 09:00 IST (l.166–167). `B/Ledger/StockAlerts.cs` also loops hourly (l.285). There are push-reminder migrations (`20261111114804_PushReminders`). `whatsAppLink` lives in `F/lib/format`.

### Inferences
- A rate-card image could reuse `posters.ts` in about 1–2 days. A jewellery morning push could reuse the KhataDues hosted service in about 1–2 days. The trigger runs from 9 am, not 8 am.

### Gaps
- I did not confirm whether a Hindi/English toggle per message exists beyond the app language.

## Pillar 3: schemes and girvi depth

### Takeaway
Schemes support both rupee ("amount") and gram kinds. A gram scheme converts each instalment to grams of a set purity at the day's selling rate. Girvi has full interest maths (simple or compound yearly, day-wise or month-wise) and records pledged gross, reading and fine weight plus a pledge value. The top-up is capped at the pledge value. There is no running loan-to-value or "interest approaching pledge value at today's rate" warning.

### Cited Findings
- `SchemePlan.Kind` is "amount" or "grams". With grams, "each instalment buys gold of Purity at that day's rate … never less than they paid" (`JewelleryBook.cs` l.102–108). `SchemeInstalment.Grams` and `RatePerGram` are at l.142–143 and are set in `PayInstalmentAsync` (l.555). Instalments are khata payments, an advance that the final bill uses (l.112, 533). The bonus comes off making charges (l.97).
- `GirviLoan` has Items, Metal, Gross, Reading, Fine and PledgeValue ("Value of the pledge at the day's buying rate when lent"), Principal, RatePerMonth (0–10%), DayWise, CompoundYearly, DueOn (1–60 months) and KYC text (l.15–56, 415–430). `GirviMath.Interest` and `Compound` are at l.256–330. Payments go to interest first (l.439–450). The top-up is rejected if principal + interest + top-up exceeds PledgeValue (l.471–472). Endpoints: `/girvi`, `/girvi/{id}/payments`, `/girvi/{id}/top-up`. The printed ticket is `F/screens/GirviTicket.tsx`.
- `GirviAddAsync` validation has no check of principal against PledgeValue at creation (l.415–421).

### Inferences
- Missing: an LTV percentage setting with a block or warning at creation; a live revaluation of the pledge at today's buy rate, with a warning when owed > X% of value, shown in the Today panel; item photos; partial release; auction notice; state rate caps. The LTV and live warning would take about 2–3 days. Photos and partial release would take about 3–4 days.
- Schemes: I found no grace period or missed-instalment status field. The "missed" state is computed client-side only (`nextDue < today`). Adding a server-side grace period would take about 1 day.

### Gaps
- I did not verify how redemption values grams at the redemption rate in the bill code.

## Pillar 4: scale, languages, offline, plan limits

### Takeaway
Web Serial scale reading to the milligram exists, with a scale master and stamping reminders. The UI is in Hindi, English and Gujarati. I found no jewellery-specific plan limits (tags, girvi count). Offline support was not confirmed.

### Cited Findings
- `F/lib/scale.ts` has `parseGrams`, `toInterval(grams, readsTo)`, `canReadScale` (navigator.serial) and `readScaleGrams(readsTo, baud=9600)`. It is tested in `scale.test.ts`. Backend `/scales` CRUD is in `JewelleryWorkshop.cs` l.213–215. Commit 1e347d5 describes "counter scales read to the milligram with stamping reminders".
- i18n files: `F/i18n/en.ts, hi.ts, gu.ts, hn.ts` plus `*More*.ts` with jewellery keys.
- Plan limits: `B/Billing` (BillingService, Subscriptions, SubscriptionWatch, RazorpayCheckout, Invoices) has no reference to jewellery, tags or girvi. A grep for tag, girvi or piece limits returned nothing.

### Inferences
- Web Serial works only in desktop Chromium. A phone (Bluetooth scale) path is missing; it would take about 3–5 days.
- Plan gating for jewellery (tag count, girvi count, jewellery as a paid tier) is unbuilt. It would take about 2–3 days in the billing and tenant code.

### Gaps
- Offline behaviour: a grep for serviceWorker/offline hit `F/App.tsx`, `F/lib/api.ts` and others, but I did not confirm whether jewellery screens work offline.
