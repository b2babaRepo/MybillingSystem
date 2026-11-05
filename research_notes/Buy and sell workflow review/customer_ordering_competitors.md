# Customer-side grocery ordering flows in India (as of Nov 2026)

Scope note: about 12 tool calls were used. Official help-centre pages for Blinkit, Zepto and Instamart did not show up in search, and Business Standard returned 403. Many details below therefore come from press coverage and third-party teardowns, and are marked as such.

## Quick-commerce and e-grocery apps: steps, reorder, lists, substitutions, slots, payment

### Takeaway
The big apps cut steps by doing three things. They pick the store automatically from the delivery location (a dark store or micro-fulfilment centre), they offer one-tap reorder of past items, and they let customers fix an order after paying (Blinkit, Dec 2025). Out-of-stock handling is weak and mostly ends in a refund after the fact. The evidence found shows no proactive substitution step offered to the customer.

### Cited Findings
- Blinkit (Dec 2025): customers can add items to an order that is already placed, while it is "being packed", with no extra delivery fee. This removes the need for a second order when something is forgotten. The CEO said the flow is still being refined. No exact time window or cap was stated — [CIOL, 3 Dec 2025](https://www.ciol.com/news/blinkit-enables-last-minute-order-additions-at-no-extra-fee-10872275); [Angel One](https://www.angelone.in/news/market-updates/blinkit-introduces-post-order-item-addition-feature-in-qcom-competition); [Republic World](https://www.republicworld.com/business/blinkit-now-lets-you-add-items-after-payment-all-details-user-reactions)
- Zepto piloted slotted (scheduled) delivery in select cities in Dec 2025, so users can pick a time window instead of 10-minute delivery. The full article was paywalled (403); this is from the headline and search snippet — [Business Standard](https://www.business-standard.com/companies/news/zepto-pilots-slotted-delivery-feature-in-select-cities-125120400504_1.html)
- A product teardown shows a Blinkit notification inviting a one-tap reorder after an item was out of stock. This is a blog illustration, not confirmed product documentation — [Prodlab Substack](https://prodlab.substack.com/p/blinkit-product-teardown-deconstructing)
- BigBasket has a "My Smart Basket" (a list of past or frequent items built by the app) in the account area. Its About page offers "Quick reorder to restock your favourites in one tap" — [BigBasket Smart Basket](https://www.bigbasket.com/member/smart-basket/); [BigBasket About](https://www.bigbasket.com/about-us)
- BB Daily is BigBasket's subscription for milk and daily essentials: customers order the night before for next-morning delivery. The App Store listing claims 5,000+ daily essentials — [Inc42](https://inc42.com/buzz/bigbasket-taps-micro-delivery-segment-with-3-acquisitions/); [App Store](https://apps.apple.com/in/app/bbdaily/id1148331468)
- Out-of-stock and missing items (anecdotal): users report items shown as available, then cancelled after payment, on both Zepto and Blinkit. Refunds often need the customer to raise a "missing item" complaint — [Voxya Zepto complaint 2024](https://voxya.com/consumer-complaints/missing-items-refund-delayed/234108); [Voxya Blinkit Oct 2025](https://voxya.com/consumer-complaints/wrong-product-delivered-and-refund-denied/252729); [DesiDime](https://www.desidime.com/discussions/received-wrong-item-on-zepto-but-what-happened-next-surprised-me)
- A South Mumbai consumer commission held Blinkit liable for not delivering or refunding a grocery item, and ordered a refund plus Rs 5,000. The customer wanted the specific item, not a refund — [LiveLaw](https://livelaw.in/consumer-cases/south-mumbai-district-commission-holds-blinkit-liable-for-failure-to-refund-price-for-undelivered-grocery-item-244061)
- A third-party audit claims that on a partial delivery, Blinkit refunds the item price but keeps the GST and fee portion. This is unverified — [Zepto vs Blinkit audit](https://casestudiesbykunal.vercel.app/Zepto_Blinkit_Audit.docx)
- Zepto claims an order fulfilment rate above 90%, as reported by a blog — [Prodlab Substack](https://prodlab.substack.com/p/zepto-improving-grocery-delivery)
- Amazon Now (10-minute delivery from micro-fulfilment centres) started in Bengaluru (Dec 2024 pilot), then Delhi, Mumbai (around Sept 2025, 100+ MFCs) and Gurugram (Dec 2025). A "100 cities" claim is unverified — [Inc42](https://inc42.com/buzz/amazons-quick-commerce-offering-now-live-in-delhi); [Indian Retailer](https://indianretailer.com/news/amazon-now-brings-10-min-delivery-mumbai-adds-100-mfcs); [Entrackr](https://entrackr.com/news/amazon-expands-10-min-quick-delivery-service-amazon-now-to-gurugram-10918873); [TechMitra (doubtful)](https://techmitra.in/amazon-now-expands-to-100-cities/)
- Flipkart Minutes launched in 2024. It was in 14–17 cities by mid-2025 (sources disagree) — [Free Press Journal](https://www.freepressjournal.in/business/amazon-rolls-out-10-minute-delivery-competition-with-blinkit-swiggy-instamart-zepto-intensifies)

### Inferences
- The fewest-step pattern is: the location sets the store automatically, the home screen leads with "buy again" or past items, then the cart, then a saved UPI payment. For a kirana app, the customer's chosen shop plays the dark store's role, and the last order or monthly list should be the first screen.
- Letting customers edit the order after placing it until the shop starts packing (Blinkit-style) suits kiranas, where the shopkeeper packs by hand.
- The big apps' weak point, cancelling items quietly and refunding later, is a gap a kirana app can fill. The shopkeeper can mark items "not available / offer substitute" before confirming, and the customer approves in one tap.

### Gaps
- Exact tap-counts for each app, and official reorder or "buy again" help pages for Blinkit, Zepto and Instamart, were not found.
- Nothing current found for JioMart's kirana partner flow, Amazon Local Shops, Paytm/Ola ONDC grocery flows, or pay-later/khata at checkout on these apps.

## ONDC and JioMart kirana ordering

### Takeaway
ONDC buyer apps gave kiranas reach, but the consumer side has shrunk. PhonePe's Pincode closed its consumer (B2C) business in Dec 2025 and moved to B2B tools for shopkeepers. Magicpin carries most ONDC volume (food-led). JioMart's WhatsApp-to-kirana flow is documented only from its 2020 launch.

### Cited Findings
- Pincode (launched Apr 2023) wound down B2C quick commerce in Dec 2025 and now focuses on inventory, order-management and ERP tools for offline retailers. It said a consumer vertical was a distraction from helping shopkeepers — [YourStory Dec 2025](https://yourstory.com/2025/12/phonepes-pincode-winds-down-b2c-quick-commerce-operations); [Angel One](https://www.angelone.in/news/unlisted-companies/phonepe-s-pincode-shuts-quick-commerce-and-b2c-operations-to-focus-solely-on-b2b)
- Pincode early on listed retailers and kiranas in a hyperlocal model (5,000+ orders/day and 50K installs in its first Bengaluru month, 2023) and used Magicpin's delivery network — [Inc42](https://inc42.com/?p=398546)
- Magicpin reportedly held about 90% of ONDC order volume (from anonymous sources) and claimed 1.5 lakh daily orders by Oct 2024 — [Inc42](https://inc42.com/features/amidst-ondc-swiggy-zomato-war-magicpin-is-the-silent-winner); [Entrackr Oct 2024](https://entrackr.com/2024/10/magicpin-becomes-largest-food-delivery-seller-app-on-ondc)
- Bajaj Markets added ONDC grocery in Aug 2025, with delivery from 30 minutes to 2 days — [Angel One](https://www.angelone.in/news/market-updates/bajaj-markets-expands-into-grocery-and-food-delivery-services-with-ondc)
- JioMart WhatsApp (2020, older data): the customer sends "Hi" to 88500 08000, gets a link to build an order, and the order goes to a local kirana over WhatsApp. At launch there was pickup only, payment at the store, and orders were ready in about 48 hours — [The News Minute](https://www.thenewsminute.com/article/reliance-s-e-grocery-offering-jiomart-goes-live-whatsapp-here-s-how-it-works-123417); [TechRadar (1,200 kirana pilot)](https://techradar.com/in/news/over-1200-kirana-stores-join-hands-with-jiomart-facebook-pilot-program)

### Inferences
- A standalone consumer marketplace for kiranas is hard to sustain (Pincode, and earlier Dunzo). A shop-first app, where each shop brings its own regular customers, avoids the cold-start problem.

### Gaps
- 2025–26 data on JioMart's kirana partner programme and on ONDC grocery order volumes was not found.

## WhatsApp-based ordering to local shops

### Takeaway
Regular kirana customers order on WhatsApp with a typed list, a photo of a handwritten list or old bill, or a voice note, and often just say "same as last time". The shopkeeper confirms what is available, and payment is by UPI or cash. People like it because they already use WhatsApp. The evidence is mostly from vendors, not independent research.

### Cited Findings
- Customers send text lists, photos of handwritten lists or previous bills, or voice messages. Regulars often reference their previous order. The owner confirms availability, then payment is by UPI or cash on delivery. WhatsApp works because it is already widely used. (Vendor marketing; that page now returns 404) — [chotu blog](https://owner.chotu.com/blog/blog-how-kirana-stores-take-online-orders-on-whatsapp); [chotu 2026 guide](https://owner.chotu.com/?p=3301)
- A COVID-era case: a small grocer used WhatsApp or Messenger with daily product lists plus a simple web form for picking delivery slots (Nepal, older) — [case note](https://arjankc.com.np/blog/notes/it204/u1-case-grocery-covid-adoption)
- WhatsApp on iPhone reportedly transcribes received voice notes (secondhand report) — [GoTranscript](https://gotranscript.com/public/whatsapp-introduces-voice-note-transcription-feature-in-latest-iphone-update)

### Inferences
- To copy for a kirana app: accept "repeat last order" as a single action, allow a photo or voice attachment as the order, and put a confirm-availability step before the bill. In effect, add structure to the WhatsApp habit rather than replace it.

### Gaps
- No independent study was found that measures how WhatsApp ordering affects repeat rates or satisfaction.

## What increases repeat orders

### Takeaway
Paid memberships raise spending (Zepto Pass: over 30% more spend in its pilot) but have not reliably built loyalty. Zepto shut its low-cost Zepto Daily membership in Feb 2026. Daily subscriptions work for milk and other essentials (BB Daily). No hard data was found on "monthly list" reminders.

### Cited Findings
- Zepto Pass (Feb 2024) passed 1M subscribers within a week. Pilot members spent more than 30% more — [YourStory](https://yourstory.com/2024/03/zepto-pass-loyalty-grocery-quick-commerce-user-experience); [Medianama](https://www.medianama.com/2024/02/223-zepto-experimental-membership-program/)
- Zepto Daily (late 2024) was discontinued in Feb 2026 "as subscriptions fail to anchor loyalty". Instamart (Swiggy One) was reported as the only top player with an active loyalty programme — [YourStory Feb 2026](https://yourstory.com/2026/02/zepto-discontinues-zepto-daily-as-subscriptions-fail-to-anchor-loyalty-in-quick-commerce); [Inc42](https://inc42.com/buzz/ipo-bound-zepto-shuts-down-loyalty-programme-zepto-daily)
- Zepto is shifting to fewer discounts, premium carts and an invite-only paid club — [Inc42](https://inc42.com/features/zeptos-retention-play-fewer-discounts-premium-carts-a-paid-club/)
- Weakly sourced: about 30–35% six-month retention and 80–85% of orders from repeat users in quick commerce — [Substack](https://asadabrar.substack.com/p/blog-6-is-quick-commerce-really-doomed)

### Inferences
- Discount-led memberships do not build habit. For kiranas, the existing relationship (khata credit, trust) is the anchor. A low-cost lever would be a saved monthly list with a "reorder now?" reminder timed to the customer's own past ordering gap.

### Gaps
- No public A/B results were found on reorder reminders, and no tap-count benchmarks.
