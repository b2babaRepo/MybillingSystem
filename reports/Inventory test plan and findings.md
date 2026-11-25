# Inventory test plan and findings (25 Nov 2026)

How buying, selling, stock and reports were tested, what was wrong, and what is still not covered.

## How it was tested

A scripted scenario with answers worked out by hand (`deploy/demo/inventory-test.mjs`, 38 checks). It makes its
own throwaway kirana shop, so it can be rerun at any time against the local API:

```
node deploy/demo/inventory-test.mjs
```

| Step | What happens | Checked against |
|---|---|---|
| Buy | 20 sugar @ ₹40, 10 oil @ ₹150, 30 soap @ ₹30 on credit (₹3,200) | Stock, cost price |
| Sell | Bill A cash ₹650 (5 sugar, 10 soap); bill B ₹720 oil on khata, ₹200 paid; bill C ₹100 UPI, then cancelled | Stock, day report, drawer, customer due |
| Return | 2 soap from bill A back on the shelf, ₹80 refunded in cash (credit note) | Stock, drawer, returns in month and P&L |
| Send back | 1 oil to the supplier, then the supplier's ₹150 credit note | Stock, supplier due before and after |
| Write off | 1 sugar damaged | Stock, stock loss |
| Buy again, cancel | 10 sugar @ ₹44, then the bill cancelled | Stock, cost price, supplier due |
| Count | Stock count finds 12 sugar where books say 14 | Stock, stock loss |
| Cancel after return | Cancelling bill A after its return | Must be refused |
| Oversell | 8 oil sold with 5 in stock | Stock value must not count stock below zero |
| No cost | An item with no cost price sold | Profit coverage, items report |

Reports compared: product stock, day report, cash drawer, customer statement, supplier due, month report, items
(margin) report, and the year's P&L.

## Bugs found and fixed

| # | Severity | Bug | Effect | Fix |
|---|---|---|---|---|
| 1 | High | **Cancelling a supplier bill reversed the stock but not the average cost.** Buying 10 sugar at ₹44 lifted the cost from ₹40 to ₹41.67; cancelling the bill left it at ₹41.67. | Stock value, profit on every later sale, and stock losses all used the wrong cost until the next purchase. | The cancel now takes the bill's units out of the average: (stock × cost − units × their cost) ÷ what stays. |
| 2 | Medium | **Write-offs and stock-count corrections didn't keep the cost of that day.** Reports valued them at whatever the cost was later. | The month report said ₹40 lost while the year's P&L said ₹125 for the same damage. | A write-off or count correction now stores the item's cost when it happens, so all reports agree. |

Both have a regression test (`TestingFixesTests`), and the scenario checks them end to end.

## Checked and working

- **Stock:** goes up on a purchase and down on a sale. A cancelled bill puts goods back; a credit note with "back on shelf" restocks; a supplier return takes stock out; a damage write-off and a stock count adjust it.
- **Day report:** sales leave out the cancelled bill. Cash ₹850, UPI ₹0, ₹520 on khata, and ₹80 shown as refunded.
- **Drawer:** ₹850 in, ₹80 out, ₹770 expected.
- **Customer due:** ₹520. **Supplier due:** ₹3,200, then ₹3,050 after their credit note.
- **Items report:** each item's sales, cost and margin match the hand figures (oil ₹120, soap ₹80, sugar ₹50).
- **Month report and P&L agree:**
  - ₹270 profit on goods, less ₹20 given back on returns, less ₹40 damaged = ₹210.
  - Closing stock at cost ₹1,970.
- **Safety rules hold:**
  - a bill with returned items can't be cancelled as a whole;
  - a purchase whose goods were sold can't be cancelled;
  - stock below zero isn't counted as value.

## Things that work as designed, but read oddly

| What | Why it reads oddly | Suggestion |
|---|---|---|
| "Returns" in the month report and P&L is ₹20, not the ₹80 refunded | It is the profit given back (₹80 sale − ₹60 cost), since the goods went back on the shelf | Label it "Profit lost on returns" |
| Month report "stock bought" is ₹0 after a ₹3,200 supplier bill | That figure is stock bought with cash from the drawer (kharcha); supplier bills are counted separately | Show supplier bills of the month next to it |
| Supplier due stays ₹3,200 after goods are sent back | By design: it comes down only when the supplier's credit note arrives | Show "₹150 waiting for credit note" on the supplier screen |
| P&L "Supplier bills" stays ₹3,200 after the supplier's credit note | It's the bills' gross total, shown for information; profit doesn't use it | Show "less supplier credit notes" under it, so opening + bills − closing ties up |
| Selling 8 with 5 in stock is allowed and stock shows −3 | Shops often bill before entering a purchase | Warn on the bill when stock would go below zero, if not already shown |
| Average cost is kept to 2 decimals | Undoing an average can leave a paisa or two (₹40.01, not ₹40) | Fine; no change |

## GST-registered shop (added the same day)

Script: `deploy/demo/gst-test.mjs` (46 checks), with a Rajasthan (08) shop:
- **Buying:** from a supplier in Rajasthan (CGST + SGST) and one in Maharashtra (IGST).
- **Selling:** a cash sale, a B2B sale in the state, and a B2B sale to Gujarat (IGST).
- **Corrections:** a credit note and a cancelled bill.

**Correct:**
- **Bills:** each bill's tax split (CGST/SGST in the state, IGST to Gujarat, place of supply 24 on the Gujarat bill).
- **Cost and dues:** cost price kept before GST, and suppliers owed the bill amount including GST.
- **GSTR-1 totals:** cancelled bill left out, B2B ₹3,410, credit note tax ₹36.
- **Input credit:** ₹1,082 on ₹8,900.
- **GSTR-3B working:**
  - output net of the credit note;
  - IGST credit used first;
  - nothing payable, ₹568 carried forward.
- **HSN table:** by B2B/B2C, net of the return.

**Bug found and fixed:**

| # | Severity | Bug | Effect | Fix |
|---|---|---|---|---|
| 3 | High | **The year's P&L didn't take credit notes off sales, GST or turnover.** The GST month did; the year report didn't. | Turnover read ₹4,500 for ₹4,300 of real sales. Turnover drives the tax-audit limit alerts and the 44AD presumptive profit, so returns made the shop look bigger than it was. | Each month's credit notes (and RN return notes) now come off sales, GST and turnover in the year report and P&L. |

## The remaining areas (added the same day)

Script: `deploy/demo/gaps-test.mjs` (46 checks). Each part makes its own throwaway shop.

| Area | What was checked | Result |
|---|---|---|
| **Medicines** | Two batches with different expiry; a 25-strip sale takes the first-expiring batch first; half a strip sold loose; a batch aged past expiry can't be sold; it shows in the expiry list; an already-expired batch can't be bought in; selling above MRP is refused | All correct |
| **Units** | 4 bags of 25 kg bought (100 kg, 4 sealed bags); one opened and 3.75 kg sold loose; one sold whole; 2 cartons of 24 soaps bought, 5 sold loose; cost per kg and per piece; stock value | 1 bug (below), fixed |
| **Godowns** | Bought straight into a godown; North → South → shop transfers; total stock unchanged by transfers; moving more than a godown holds is refused; a counter sale takes from the shop's own stock; cancelling a transfer whose goods were partly sold is refused | All correct |
| **Offline and two counters** | The same bill sent twice at once saves once; a bill made offline 3 hours ago keeps its time; a phone clock 2 hours fast is corrected; 20 bills at the same moment leave stock exact and all show in the day report | All correct |
| **Month end** | Books closed for last month: a new bill dated in it and cancelling a bill in it are refused; a credit note today for an old bill is allowed; the closed month's figures don't change; reopening needs a reason | All correct |
| **Composition GST** | Bills are bills of supply with no tax, even to a GSTIN buyer; the GST month knows it's composition; CMP-08 = 1% of turnover (₹5 on ₹500); the P&L shows no GST collected | All correct |
| **Report screens** | Same shop read through the screens: home, items, day report, sales report, items margin, stock statement, P&L, suppliers, cash book | 2 bugs (below), fixed; then all agree |

**Bugs found and fixed:**

| # | Severity | Bug | Effect | Fix |
|---|---|---|---|---|
| 4 | Medium | **Sealed cartons weren't reduced by loose sales.** 2 cartons of 24, 5 sold loose, and the app still showed 2 sealed cartons with only 43 pieces. | Shop thinks it has 2 unopened cartons to sell or return; "open a bag" allowed opening a carton that doesn't exist. | Sealed bags and cartons are capped at what the stock can fill (43 ÷ 24 = 1). |
| 5 | Medium | **The stock statement and the P&L still valued stock at a cancelled supplier bill's cost.** Items screen said ₹1,140; the stock statement and P&L closing stock said ₹1,160.04. | Closing stock in the P&L and the GST stock account were overstated. | The average-cost calculation behind both now takes a cancelled bill's units out, as the item's own cost does. |
| 6 | Medium | **The stock statement counted a cancelled bill as sold.** 2 sugar billed and cancelled showed as sold 7 (not 5), with the 2 back under "other in". | The GST stock account (sold quantity) was overstated. | A cancelled bill's goods coming back now come off "Sold". |

**Not built:** GST on **reverse charge** (RCM) for purchases, for example freight from a transporter (GTA). The app only handles it for jewellery karigar work. A kirana paying GTA freight pays this GST itself; today it has to be worked out by hand.

**Note for running the scripts:** sign-up is rate-limited. Running all four scripts back to back can hit it (429 or 401). Wait a minute and run again.

## Not covered yet (next tests)

| Area | Why it matters | Test type |
|---|---|---|
| Reverse-charge GST on purchases (feature not built) | Needed by shops that pay GTA freight | Build, then script |
| Restaurant tables, salon appointments, rooms and bookings | Other trades the app serves | Scenario scripts |
| Phone-sized screens for the reports | Shopkeepers use phones | Browser check at phone width |
