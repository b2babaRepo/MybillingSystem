# Jewellery gold savings schemes and girvi (gold loans), India 2026

## Gold savings schemes: rupee-instalment vs gram-accumulation, how chains run them

### Takeaway
Big chains use two models: (a) fixed rupee instalments with a discount or bonus at maturity (Tanishq Golden Harvest, Malabar, Kalyan), and (b) weight accumulation, where each payment is converted to 22KT grams at the jeweller's displayed rate at the moment of payment (AVR Swarna Mahal, GRT Golden Seed). Both models typically allow no cash refund and redemption only as jewellery.

### Cited Findings
- Tanishq Golden Harvest: 10 fixed monthly instalments, minimum Rs 2,000 in multiples of Rs 1,000, one per month. The benefit is up to 75% of one instalment as a discount, after 10 months (one source says after 366 days). There is a 7-day grace period, and a late payment waives the discount. Instalments not redeemed within 400 days are refunded with a voucher. — [Mia by Tanishq](https://www.miabytanishq.com/en_IN/blogs/beginners-guide/everything-you-need-to-know-about-golden-harvest); [BankBazaar](https://www.bankbazaar.com/gold-rate/tanishq-gold-schemes.html); [StableMoney](https://stablemoney.in/blog/is-tanishq-gold-investment-plan-worth-it). An older article gives a 7.5% figure instead ([Zeebiz](https://www.zeebiz.com/hindi/economy/open-tanishq-golden-harvest-account-and-get-minimum-75-percent-discount-on-tanishq-jewellery-and-many-other-benefits-97085)), which conflicts.
- Kalyan purchase-advance (weight) scheme: up to 10% variance in weight versus the selection, the customer pays for actual weight, and no extra discount. Tenure is described as 11 months, or a maximum of 365 days, with closure possible after the 11th instalment; sources conflict on this. — [BankBazaar Kalyan](https://www.bankbazaar.com/gold-rate/kalyan-jewellers-gold-schemes.html); [Gullak](https://gullak.money/blog/kalyan-jewellers-gold-scheme); [Coverfox](https://coverfox.com/personal-finance/gold-rate/kalyan-jewellers-gold-schemes)
- Malabar Golden Bloom: 11 monthly instalments, then a jewellery purchase. The sources found do not state the gram conversion rule. — [Gullak Malabar](https://gullak.money/blog/malabar-gold-scheme)
- GRT Golden Seed: gold is credited at the rate prevailing at each monthly payment. Redemption is allowed only after the final instalment, with no cash refund. — [BankBazaar GRT](https://www.bankbazaar.com/gold-rate/grt-gold-schemes.html)
- AVR Swarna Mahal Akshayam eGold+ (2026) has these rules:
  - Each successful payment is converted to 22KT gold weight at the AVR 22KT rate displayed when the payment succeeds.
  - Minimum Rs 2,500 at enrolment, then any amount in multiples of Rs 500, with no fixed schedule.
  - Bonus gold of 2% or 1% depending on enrolment date, for jewellery only (not coins or bars). Bonus is "rounded/recorded as per the scheme system display".
  - Pre-closure, or redeeming after the window, gives principal grams only with no bonus.
  - No partial redemption and no cash refund.
  - Making charges, value addition (VA), hallmark and GST are extra.
  - Source: [AVR Akshayam eGold](https://avrswarnamahal.com/akshayam-egold)
- The same AVR page gives inconsistent bonus wording ("25 mg per gram" and "10 mg/g") against its 2%/1% terms. Clear, single-rule disclosure is a design lesson. — [AVR](https://avrswarnamahal.com/akshayam-egold)
- AVR Golden One converts a one-time lump sum at the day's 22KT rate. — [AVR Golden One](https://avrswarnamahal.com/golden-one)

### Inferences
- A gram scheme design should store, for every instalment:
  - the amount paid;
  - the rate used (22K per gram, with a timestamp);
  - the purity basis;
  - the grams credited, rounded down to 3 decimals (mg).
- Bonus should be shown separately as a percentage of grams, and it should be forfeited on pre-closure or late payment.
- The scheme should be redeemable only as jewellery. Making charges and GST should be shown as payable at redemption.
- Rounding rules are rarely published. Pick an explicit rule (for example truncate to 0.001 g) and print it on the passbook.

### Gaps
- I found no official Joyalukkas or Malabar terms page that states the purity or rounding rule.
- How small jewellers run schemes is not documented in the sources found. It is likely informal, with rupee instalments.

## Legal limits: Deposit Rules, BUDS Act, entity type

### Takeaway
A scheme stays outside "deposit" rules if money is an advance against the supply of goods, adjusted within 365 days. This is why schemes run 10 to 11 months. A scheme running longer than 365 days, or one that promises cash returns, risks being treated as a deposit and as a banned unregulated deposit scheme under BUDS 2019, which applies to proprietors and partnerships too.

### Cited Findings
- Companies (Acceptance of Deposits) Rules 2014: an amount received is a "deposit" unless an exclusion applies. Jewellers rely on Rule 2(1)(c)(xii), advances for the supply of goods that are appropriated against supply within 365 days. Hence 11-month tenures. — [LiveLaw](https://livelaw.in/articles/law-relating-to-jewellery-brands-gold-savings-schemes-279580)
- After the Companies Act 2013, jewellery companies had to discontinue or restructure schemes, and Titan suspended and relaunched its scheme. — [LiveLaw](https://livelaw.in/articles/law-relating-to-jewellery-brands-gold-savings-schemes-279580); [Moneylife](https://moneylife.in/article/gold-investment-scheme-is-it-illegal-under-new-companies-act/36965.html)
- The GJF/GJC guideline (2014) advised members to close collections and record the sale within 364 days of the scheme start. — [GJC guidelines PDF](https://www.gjc.org.in/pdf/guidelines_on_golod_savings_scheme.pdf); [Indian Jeweller](https://www.indianjeweller.in/Indian-Jewellery-News/11664/new-federal-order-bans-extension-of-gold-savings-schemes-beyond-365-days)
- BUDS Act 2019 Section 3 bans unregulated deposit schemes. Commentary says a scheme beyond 365 days could be treated as an unregulated deposit. — [LiveLaw](https://livelaw.in/articles/law-relating-to-jewellery-brands-gold-savings-schemes-279580)
- Industry exposure: about Rs 20,000 crore of redemptions was flagged in 2014 when the rules changed. — [Business Standard](https://www.business-standard.com/article/markets/jewellers-face-rs-20-000-cr-redemption-from-gold-deposit-schemes-114071501024_1.html)
- Scheme collapse risk is real. In Mysuru, customers filed a police complaint after a jeweller running a monthly scheme closed the shop. — [Siasat](https://www.siasat.com/mysuru-jewellery-owner-held-for-alleged-gold-scheme-fraud-3535201/)
- In Navi Mumbai, a jeweller allegedly cheated 29 customers of Rs 1.44 crore and the owner absconded (reported 2026). — [Free Press Journal](https://www.freepressjournal.in/mumbai/navi-mumbai-crime-koparkhairane-jeweller-allegedly-cheats-29-customers-of-144-crore-shop-owner-on-run)

### Inferences
- The app should hard-cap scheme duration below 365 days from the first instalment, for example 11 instalments or 330 days. It should block the extension of matured schemes.
- No cash-interest or "return" wording should appear anywhere. Benefits should be framed as a discount or bonus on jewellery.
- These guards should apply to every entity type. The Deposit Rules bind companies, but BUDS reaches any "deposit taker", including individuals, proprietors and partnerships.
- Each receipt should be an advance receipt that states the redemption rule.

### Gaps
- The BUDS text was not fetched directly. The Rule 2(1)(c)(xii) wording comes from a secondary source (LiveLaw).
- I found no 2025-26 MCA clarification or court ruling specific to jeweller schemes.
- GST treatment of advances (for gold, generally at the time of supply) was not checked.

## Girvi: LTV, interest conventions, auction, loss warnings

### Takeaway
RBI's Lending Against Gold and Silver Collateral Directions 2025 (issued June 2025, compliance by April 1, 2026) bind banks and NBFCs, not unregulated jewellers or pawnbrokers. They are still the best safe benchmark. Pawnbroking by jewellers is governed by state Pawnbroker and Money-lender Acts, which cap interest and require licences.

### Cited Findings
- LTV caps, which must be maintained through the loan tenure:
  - 85% for loans up to Rs 2.5 lakh (per borrower, including interest);
  - 80% for Rs 2.5 to 5 lakh;
  - 75% above Rs 5 lakh.
  - Sources: [EY](https://ey.com/en_in/insights/strategy-transactions/rbi-gold-loan-guidelines-2025-impact-assessment-and-key-changes); [Onmanorama](https://www.onmanorama.com/news/business/2025/06/07/gold-loan-to-value-rules-eased-rbi-small-borrowers.html); [Business Standard](https://www.business-standard.com/finance/news/gold-loan-ltv-raised-to-85-percent-up-to-rs-2-5-lakh-rbi-125060601173_1.html)
- Compliance date: "no later than April 1, 2026". — [TaxGuru](https://taxguru.in/rbi/rbi-issues-gold-silver-loan-directions.html)
- Detailed credit appraisal is needed only above Rs 2.5 lakh. — [EY](https://ey.com/en_in/insights/strategy-transactions/rbi-gold-loan-guidelines-2025-impact-assessment-and-key-changes)
- Bullet loans must repay principal plus interest within 12 months. This ends interest-only renewal. — [Angel One](https://www.angelone.in/news/personal-finance/rbi-gold-loan-guidelines-2025-recap-7-big-changes-borrowers-and-lenders-should-know)
- Pledged gold must be returned within 7 working days of repayment, with Rs 5,000 per day compensation for delay. — [Angel One](https://www.angelone.in/news/personal-finance/rbi-gold-loan-guidelines-2025-recap-7-big-changes-borrowers-and-lenders-should-know)
- Caps per borrower are 1 kg of jewellery and 50 g of coins. — [Angel One](https://www.angelone.in/news/personal-finance/rbi-gold-loan-guidelines-2025-recap-7-big-changes-borrowers-and-lenders-should-know)
- Lenders must give advance notice before an auction and set a fair reserve price. Valuation must be transparent and market-linked, with the borrower present. — [Angel One](https://www.angelone.in/news/personal-finance/rbi-gold-loan-guidelines-2025-recap-7-big-changes-borrowers-and-lenders-should-know); detail also in [Cyril Amarchand](https://corporate.cyrilamarchandblogs.com/2025/11/fig-paper-no-52-rbi-directions-on-lending-against-gold-and-silver-collateral-a-harmonised-regulatory-framework/) and [Mondaq](https://www.mondaq.com/india/commoditiesderivativesstock-exchanges/1636334/a-new-era-of-responsible-lending-rbis-lending-against-gold-and-silver-collateral-directions-2025)
- Karnataka Pawnbrokers Act 1961 s.6: pledge loan interest is capped at 9% per annum simple, or a rate notified by the state. Licences run 5 years after the 1998 amendment. — [Karnataka Pawnbrokers Act (India Code)](https://www.indiacode.nic.in/bitstream/123456789/7163/1/13_of_1962_%28e%29.pdf)
- The Maharashtra Money-Lending (Regulation) Act 2014 penalises interest above notified rates (s.31). — [PRS Maharashtra 2014](https://prsindia.org/files/bills_acts/acts_states/maharashtra/2014/2014MH8.pdf)
- Jeweller misuse of pledged gold occurs. In Bengaluru, a "0% interest gold loan" shop sold about 4 kg of pledged gold and absconded (Nov 2025). — [Deccan Herald](https://www.deccanherald.com/india/karnataka/bengaluru/ccb-busts-0-interest-gold-loan-scam-in-bengaluru-duo-cheated-customers-of-4-kg-gold-3785868)
- In Ahmedabad, a jeweller fled with pledged ornaments. — [Gujarat Samachar](https://english.gujaratsamachar.com/news/gujarat/jeweller-booked-for-alleged-55-lakh-fraud-after-fleeing-with-pledged-gold-silver-ornaments-29340992916.html)
- OMUNIM markets girvi features: interest calculation, loan status and outstanding reports, tracking of due dates and profit/loss, and Email/SMS alerts and reminders. I found no public documentation of a specific "loss/margin warning". — [Techjockey](https://www.techjockey.com/blog/loan-finance-management-with-onlinemunim-jewellery-software/amp); [Techjockey listing](https://techjockey.com/detail/omunim-accounting-software); [TrustRadius](https://www.trustradius.com/products/omunim/details)

### Inferences
- **Loss warning formula.** Use current value = net fine weight × today's rate (22K or the actual purity) − a deduction for stones and wastage. Then margin % = (principal + accrued interest) / current value.
- **Alert bands.** Warn at 75%, show a strong alert at 85% (the RBI small-loan ceiling), and mark "loss risk" at 100% or more. Also warn at 11 months, before the 12-month mark.
- **Interest conventions.** Common jeweller practice is monthly simple interest (for example "1.5% per month") with part-months rounded up to a full month or to 15 days. This is not sourced here. The app should make the convention explicit and configurable, and should show the annualised rate next to the state cap, such as Karnataka's notified rate.
- **Release workflow.** Mirror RBI good practice for a safer product: a written pledge ticket, a release within 7 days, written auction notice, and a reserve price based on market value.

### Gaps
- I found no authoritative survey of jeweller and pawnbroker LTV (anecdotally 60 to 75%) or of the monthly-rate norm. Treat both as unverified.
- The auction notice period and the valuation basis (for example the 30-day average 22K rate) in the RBI Directions were not confirmed from the primary text, because the Business Standard page returned 403.
- Current state-notified pawnbroker rates outside Karnataka were not found.
- OMUNIM and SwarnaVyapar margin-warning screens were not documented publicly.

## Defaults and what reminders work

### Takeaway
The evidence base is thin. Scheme defaults are handled by forfeiting the bonus or discount (Tanishq waives the discount on a late payment after a 7-day grace period). Girvi defaults end in notice and auction. I found no studies that measure how well WhatsApp reminders work for jeweller EMIs.

### Cited Findings
- Tanishq: a 7-day grace period, after which the discount is waived. — [StableMoney](https://stablemoney.in/blog/is-tanishq-gold-investment-plan-worth-it)
- AVR: the bonus is lost on pre-closure or late redemption. Principal grams are always kept. — [AVR](https://avrswarnamahal.com/akshayam-egold)
- RBI: advance notice is required before an auction. — [Angel One](https://www.angelone.in/news/personal-finance/rbi-gold-loan-guidelines-2025-recap-7-big-changes-borrowers-and-lenders-should-know)
- OMUNIM provides SMS reminders. — [Techjockey listing](https://techjockey.com/detail/omunim-accounting-software)

### Inferences
- For schemes, send WhatsApp reminders 3 days before the due date, on the due date, and at the end of the grace period, each showing the grams held so far and the bonus at stake.
- For girvi, send a monthly interest statement, a margin alert when the gold price falls, an 11-month maturity notice, and a formal pre-auction notice.

### Gaps
- I found no data on scheme default rates, the effect of high 2025-26 gold prices on scheme drop-outs, or measured WhatsApp reminder effectiveness.
