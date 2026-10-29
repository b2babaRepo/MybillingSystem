# Cement / Building-Materials Shops (India, 2026): Workflows and Product Needs

## Units, pricing, GST, HSN and e-way bills

### Takeaway
Since 22 Sep 2025, cement is taxed at 18% GST, down from 28%. Sand and aggregates are at 5%, clay and fly-ash bricks at 12%, and steel, tiles and PVC pipes at 18%. One shop therefore bills at several GST rates. Goods are sold in units a grocery app does not have: bags, brass/cft, tonnes, kg converted from bar length, and per-truck loads. Most loads are worth over ₹50,000, so they need an e-way bill.

### Cited Findings
- Cement (HSN 2523) was cut from 28% to 18% GST by Notification 09/2025-Central Tax (Rate) dated 17 Sep 2025, effective 22 Sep 2025. The rate covers OPC, PPC, slag and white cement. — [Tally](https://tallysolutions.com/gst/cement-gst-percentage-rate-hsn-code-applicability/); [Business Standard](https://www.business-standard.com/amp/industry/news/gst-rate-cut-on-cement-to-boost-infrastructure-125090401466_1.html)
- India Ratings (Ind-Ra) estimated the cut would lower prices by about ₹30–35 per bag. UltraTech passed the cut on to customers. — [Business Standard](https://www.business-standard.com/amp/industry/news/gst-rate-cut-to-lower-cement-prices-by-30-to-35-per-bag-reports-ind-ra-125091001256_1.html); [TaxGuru](https://taxguru.in/goods-and-service-tax/cement-becomes-cheaper-ultratech-passes-gst-benefits-customers.html)
- **Flag:** some pages still show cement at 28%, for example Motilal Oswal. They predate Sep 2025 and are out of date. — [Motilal Oswal](https://www.motilaloswal.com/personal-finance/tax/gst-rate-on-cement-impact-of-gst-on-cement-industry)
- Sources disagree on cement sub-headings. Accountune gives 2523 29 for OPC/PPC and 2523 21 for white cement. Munim gives other codes. — [Accountune](https://accountune.com/hsn-code-cement-construction); [Munim](https://themunim.com/blog/gst-on-cement/)
- Rates after the reform, as Tally lists them:

  | Item | HSN | GST |
  |---|---|---|
  | Sand (river and crushed) | 2505 | 5% |
  | Aggregates (gravel, crushed stone, ballast) | 2517 | 5% |
  | Clay and fly-ash bricks | 6901 | 12% (Notification 14/2025) |
  | Refractory bricks | 6902 | 18% |
  | Iron and steel | 7206–7217 | 18% |
  | TMT bars | 7214 | 18% |
  | Structural steel | 7308 | 18% |
  | Ceramic tiles | 6907/6908 | 18% |
  | Plastic/PVC pipes and fittings | 3917 | 18% |

  — [Tally HSN guide](https://tallysolutions.com/gst/hsn-code-of-cement-bricks-sand/)
- **Conflict on TMT bars:** disytax.com claims TMT bars fell to 5% GST. Most sources, including Tally and Accountune, say 18%. Accountune also calls a vendor's 5% example for TMT an error. — [Disytax](https://disytax.com/hsn-72-iron-steel-gst-rate/) vs [Tally](https://tallysolutions.com/gst/steel-iron-tmt-gst/), [Accountune](https://accountune.com/hsn-code-steel)
- 1 brass = 100 cft, about 2.83 m³. Dealers quote sand per brass. — [InfraLens](https://infralens.in/prices/aggregates)
- Sand prices in 2026 are roughly ₹55–85/cft for river sand and ₹45–70/cft for M-sand. Transport adds ₹15–40/cft on top. — [ConstructionEstimatorIndia](https://constructionestimatorindia.com/?p=11306)
- Prices move sharply with local supply. During a 2025 shortage, Nagpur sand went from ₹40 to ₹105–120/cft. — [Hitavada](https://www.thehitavada.com/Encyc/2025/10/6/sand-mafia-creates-artificial-shortage-pushes-up-prices-to-rs-105-120cft.html)
- Maharashtra's 2023 government scheme sold sand at ₹600/brass. This is old, state-specific information. — [Hitavada](https://www.thehitavada.com/Encyc/2023/6/9/Now-buy-sand-for-Rs-600-per-brass-from-government.html)
- E-way bill rules (Rule 138, CGST Rules):
  - An e-way bill is required for goods over ₹50,000 regardless of distance. Thresholds for movement within a state vary by state.
  - Validity is 1 day per 200 km, in force since 1 Jan 2021. Over-dimensional cargo gets 1 day per 20 km.

  — [IRIS GST](https://irisgst.com/?p=52867); [StudyCafe](https://studycafe.in/validity-of-e-way-bill-narrowed-by-increasing-distance-from-100-km-to-200-km-per-day-93584.html)
- **Flag:** some pages still show the pre-2021 slab table. — [TaxAJ](https://www.taxaj.com/learn/?p=936)

### Inferences
- **Several GST rates per bill.** The data model needs HSN and GST rate on each catalogue item. It needs a GST breakup per invoice line, because a single bill can mix 5%, 12% and 18%.
- **Unit conversions.** Each item needs a base unit plus alternate units with conversion factors:
  - Cement: bag = 50 kg.
  - Sand: brass = 100 cft.
  - Steel: tonne = 1000 kg.
  - TMT bars: weight is about D²/162 kg per metre. This formula is standard engineering knowledge and did not come from a cited source here.
- **Decimal quantities.** Sand (0.5 brass) and steel weight (kg) need fractional quantities.
- **Frequently changing rates.** The shop needs a quick way to bulk-edit today's rate. The app should keep a history of rate changes.
- **Tiered prices.** A per-customer or per-tier price list is needed, such as a contractor rate and a retail rate.
- **Transport and loading.** These belong on the bill as separate charge lines (freight, loading/unloading, hamali). They have their own tax treatment, which still needs checking.
- **E-way bill fields.** Any bill over ₹50,000 needs vehicle number and transporter details. The app should at least capture these and prompt for an e-way bill. Integrating with the portal can wait until after the MVP.

### Gaps
- The search did not establish whether the shop must charge GST separately on transport and loading charges, or whether they take the rate of the main supply.
- No verified figures on how often dealers revise cement rates in practice.
- No source found for typical contractor-versus-retail margin gaps.
- Not checked: GST rates for sanitaryware and paints.
- Not checked: state-specific e-way bill thresholds for movement within a state.

## Customers, credit, challans, deliveries and schemes

### Takeaway
Credit customers are mostly contractors and builders. They run large accounts that often cover many sites over the life of a project. Software for this trade offers:
- site- or project-wise accounts;
- delivery challans that convert to invoices;
- credit limits and receivables aging.

Cement distributor tools also track dealer commissions and schemes.

### Cited Findings
- Relaso markets its software to hardware and building-material shops that sell bulk cement. It keeps "running accounts for local contractors and builders with AR aging reports". — [Relaso](https://billingpro.relaso.com/industry/hardware)
- OkCredit's hardware khata app offers contractor accounts that "run for the length of a project", with supplier dues on the same ledger. — [OkCredit](https://okcredit.in/khata-app-for-hardware-store)
- Vyapar offers:
  - site-wise inventory and site-wise invoices;
  - per-site cost tracking for labour and materials;
  - delivery challans that convert into invoices.

  — [Vyapar construction inventory](https://api1.vyaparapp.in/free/inventory-management-software/construction); [Vyapar construction invoicing](https://vyaparapp.in/free/invoicing-software/construction)
- myBillBook handles several construction sites from one dashboard. It also has a contractor and vendor ledger. — [myBillBook construction](https://mybillbook.in/free/invoicing-software/construction/)
- LEDGERS' challan module reduces stock automatically when goods are dispatched. — [LEDGERS](https://ledgers.cloud/in/deliverychallan)
- HysabOne is built for cement distribution in Pakistan. It covers dealer commissions, credit limits and dealer-wise profitability. — [HysabOne](https://hysabone.com/industries/cement/)
- HisabPlus (Bangladesh) shows each dealer's credit limit and current due before the next delivery is approved. — [HisabPlus](https://hisabplus.com/Blog/dealership-management-software-bangladesh)

### Inferences
- **Customer → project/site.** Add an optional site or project under each customer. Bills and payments get tagged to a site. Show balances per site and a total for the customer.
- **Challan-to-invoice flow.** Stock goes out on a challan. Several challans, including partial deliveries against one order, can later be combined into one tax invoice. This is the biggest workflow difference from kirana billing.
- **Credit limit and terms.** Each customer needs a credit limit and a credit period, typically 30–60 days. The user brief gives this range; I found no source for it. The app should:
  - warn when a bill would take the customer over the limit;
  - age the dues in 0–30, 31–60, 61–90 and 90+ day buckets;
  - send WhatsApp reminders based on due date, not just on the balance.
- **Payments.** The app needs:
  - advance payments held as an unapplied credit balance;
  - cheque entries with cheque number, date and status (pending, cleared, bounced) plus post-dated cheques;
  - part-payments allocated against specific bills.
- **Schemes.** Brand or dealer scheme tracking, such as per-bag incentives and targets, is a feature for the distributor tier. It is not MVP.

### Gaps
- No primary dealer interviews or forum and YouTube sources were gathered, because the search was cut short. Typical credit periods, cheque-bounce rates and site-account habits are therefore unverified.
- No India-specific cement scheme mechanics were sourced.

## Apps used today and what they offer

### Takeaway
General Indian SME billing apps market hardware and construction pages. Vyapar, myBillBook, OkCredit and Relaso are examples, and there are also Tally/BUSY GST guides. Their claims are mostly generic: multi-unit billing, challans, site-wise tracking and contractor ledgers. None of them showed brass or sand-specific unit handling.

### Cited Findings
- Vyapar's construction pages list:
  - purchase logging for cement, steel and bricks;
  - low-stock alerts;
  - site-wise billing;
  - estimates and quotations shared on WhatsApp or SMS.

  — [Vyapar](https://vyaparapp.in/free/invoicing-software/construction); [Vyapar estimate](https://vyaparapp.in/free/invoicing-software/estimate)
- Capterra reviewers of Vyapar praise WhatsApp invoice sharing and the dashboard. They note a learning curve, and one says the app suits simple GST billing but may fall short for growing businesses. — [Capterra](https://www.capterra.in/reviews/180579/vyapar)
- myBillBook's hardware page lists multi-unit billing, barcode scanning, batch tracking and supplier management. One page says plans start at ₹399/yr; another describes a 14-day trial. The two do not match, so verify current pricing directly. — [myBillBook hardware](https://mybillbook.in/s/billing-software-for-hardware-store/)
- Tally and BUSY publish GST rate guides for cement and steel. These suggest they serve this trade's accountants, but they are guides, not trade-specific feature pages. — [BUSY cement](https://busy.in/gst-rates/cement); [Tally](https://tallysolutions.com/gst/steel-iron-tmt-gst/)

### Inferences
- Gaps in what competitors offer are where a product could stand out:
  - brass and cft units;
  - converting TMT bar length to weight;
  - updating the daily rate for all items at once;
  - site-wise khata combined with WhatsApp reminders in a simple mobile-first app.

### Gaps
- Not researched: Marg and Khatabook pages for this trade.
- No dealer complaints about these apps were collected from Play Store reviews or forums.

## MVP must-have vs nice-to-have

### Takeaway
The MVP extends Grocery Khata with:
- alternate units and decimal quantities;
- GST and HSN on each item;
- tiered prices;
- site-wise khata;
- challan-to-invoice billing;
- extra charge lines;
- credit limits and aging;
- cheque and advance tracking.

E-way bill portal integration, schemes, and multiple godowns come after the MVP.

### Inferences (based on the sections above)
**Must-have (MVP)**
1. Item data:
   - unit conversions (bag, kg, tonne, brass, cft, piece/rod);
   - decimal quantities;
   - HSN and GST rate on each item.
2. A building-materials starter catalogue: cement brands (OPC, PPC), TMT bars by diameter (8–32 mm), sand, aggregates, bricks, pipes, tiles, and common sanitary/hardware items.
3. Pricing:
   - a "today's rate" screen for quick bulk updates;
   - a retail or contractor price tier on each customer.
4. Charge lines on the bill for transport, loading/unloading and others.
5. Customer → site/project accounts, with balances shown per site.
6. Delivery challans that reduce stock, can deliver part of an order, and convert to one or more invoices.
7. Credit terms:
   - a credit limit and credit period per customer;
   - an over-limit warning;
   - aging reports;
   - WhatsApp reminders by due date.
8. Payment modes (cash, UPI, cheque with status and post-dated date, bank transfer), advances, and allocation against bills.
9. Bill fields for vehicle number and driver, plus a warning when a bill is over ₹50,000 and needs an e-way bill.

**Nice-to-have**
- Generating e-way bills and e-invoices through the GST/NIC portal APIs.
- Calculating TMT weight from length and diameter.
- Brand or dealer scheme and incentive tracking.
- Several godowns or yards.
- Purchase-side truck/load receiving with weighbridge slips.
- Quotations and estimates.
- Supplier ledger for cement company dues.
- Rate-change history charts.

### Gaps
- None of these priorities have been checked with actual dealers yet.
