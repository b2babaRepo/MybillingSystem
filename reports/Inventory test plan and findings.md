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

## Not covered yet (next tests)

| Area | Why it matters | Test type |
|---|---|---|
| A GST-registered shop: GST on sales and purchases, input credit, GSTR-1 and 3B figures | This shop had no GSTIN, so all GST was ₹0 | Scenario script with a GSTIN |
| Medicines by batch and expiry; expired stock blocked | Batch stock is kept separately | Scenario script |
| Loose and bag items (kg, bags opened), units bought in cartons | Unit conversions are easy to get wrong | Scenario script |
| Godown transfers | Stock per godown must add up to the total | Integration test |
| Offline bills synced later, two counters selling the same item | Stock must not double-count | Integration test |
| Month end and the books closed for a month | Changes must go to the open month | Integration test |
| The screens showing these numbers (stock list, reports) | The API is right; the screens should show the same | Browser check |
