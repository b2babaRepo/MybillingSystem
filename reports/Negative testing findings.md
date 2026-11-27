# Negative testing findings (25 Nov 2026)

Run on the local app (API on port 5274, website on 5173) with the Test Sona Jewellers shop and two throwaway shops.

## What was tried

| Area | Cases | Result |
|---|---|---|
| Every screen route (about 95), signed in as a jewellery owner | 95 | No crash, no blank screen, no console errors |
| Bad links: unknown ids, ids that aren't ids, bad months, a 3,000-letter link, HTML in the link, unknown paths | 27 | 3 problems (below) |
| API: no sign-in, broken token, broken JSON, wrong types | 21 | All refused |
| API: bad customers, khata entries, bills, products, rates, painters, payouts, shop settings | 49 | 2 problems |
| API: one shop using another shop's customer, bill, painter and product ids | 10 | All refused or harmless; no data leaked |
| API: bad jewellery input (rates, pieces, girvi, schemes, orders, approvals) | 19 | 1 problem |
| Screen forms: add customer with blanks, spaces, a short phone, HTML in the name | 4 | 1 problem |

The API cases are in `deploy/demo/negative-test.mjs` and can be rerun at any time:
`node deploy/demo/negative-test.mjs` (makes its own throwaway shops).

## Bugs

| # | Severity | Where | What happens | Fix |
|---|---|---|---|---|
| 1 | High | Khata → Add customer | A 5-digit mobile number is accepted on screen. The customer shows up on this phone, but the server refuses it, so the customer never reaches other phones or the server. The only sign is "Refused by server" on the Sync screen, which shopkeepers rarely open. | Check the number on screen (10 digits, or empty) before saving, with the same rule as the server. Check the other forms that take a phone (suppliers, staff, painters) the same way. |
| 2 | Medium | Today's rates → change by percent (new) | -100% sets every matching price to ₹1; -250% does too. The prices only show as changes to check, but one tap on Save would apply them. | Allow only -90% to +200% and show a message outside that. |
| 3 | Medium | Jewellery savings scheme | A scheme can be made with a bonus bigger than everything the customer pays (2 × ₹100 with a ₹1,00,000 bonus). | Refuse a bonus above one instalment, or above a set share of the total. |
| 4 | Low | Estimate print link with a wrong id | The page stays on "…" for good. | Show "Estimate not found" with a back button. |
| 5 | Low | Kharcha print link with a bad month (2026-99) | The page stays on "…" for good. | Show "Month not valid" with a back button. |
| 6 | Low | Bill print link with an unknown id | Says "This is not on the server yet. Sync, then open it again", which is wrong for a bill that doesn't exist. | Say "Bill not found" when nothing is waiting to sync for that id. |
| 7 | Low | API: save rates for an unknown item | Answers OK with "changed: 0" and no error. Another shop's item ids are also just skipped (no leak). | Answer 400 "Item not found" so a bad import is noticed. |

## Checked and fine (by design)

- **Signing up again with a number that already has an account:** with the same password it just signs the owner in; with a different password it is refused. No second shop is made.
- **A khata entry dated next year:** saved with today's date. The app treats a future date as a wrong phone clock.
- **Unknown screen links** such as `#/does-not-exist` show the home screen, and an unknown customer link shows the khata list.
- **HTML in a customer name** is shown as plain text, not run.
- **One shop using another shop's ids:** every read, change and payment was refused (404 or 400), including the new painter payouts.
