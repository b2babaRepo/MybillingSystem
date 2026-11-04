# Software and apps for Indian milk collection centres and dairy societies (2026)

## Which products exist (AMCU vendors, NDDB systems, Play Store apps, general khata apps)?

### Takeaway
There are three tiers. At the top, co-operatives use AMCU/AMCS hardware and software systems (Prompt, NDDB AMCS). In the middle are many small Android apps for private collection centres (Hamari Dairy, MCS, My Dairy Center, Simple Dairy, Meri Dairy, Liter). At the bottom, doodhwalas use general khata apps (OkCredit, Khatabook) or simple "milk diary" apps. Nobody clearly combines fat/SNF collection with WhatsApp-first khata and UPI.

### Cited Findings
- Prompt AMCS records quantity, fat and SNF in real time and calculates the farmer's payment from the dairy's stored rate chart. Hardware is an analyser (Fatomatic/Milkoscreen), an electronic scale, a PC or DPU, and a printer. Payments go to farmers' bank accounts, and there are farmer and supervisor apps. — [Prompt AMCS](https://www.promptdairytech.com/automatic-milk-collection-system/); [AMCS brochure](https://promptdairytech.com/wp-content/uploads/2019/11/AMCS-brochure.pdf)
- Prompt says Amul is a deployment, with about 2 lakh farmer app users. This is the vendor's own claim. — [Prompt slideshare](https://pt.slideshare.net/promptsoftech/prompt-milkpro)
- Prompt Milk Sangrah (Android) gets readings from analyser and scale through the "Easy5" printer, over RS-232 or a Bluetooth dongle. It works online and offline, imports rate charts from Excel (FAT, FAT-SNF, FAT-LR and solids bases), sends SMS to farmers and prints thermal slips. Prompt claims 55,000+ users in 27+ states. — [Milk Sangrah](https://www.promptdairytech.com/milk-sangrah-app); [brochure](https://promptdairytech.com/wp-content/uploads/2022/04/Milk-Sangrah-brochure_25.3.22.pdf)
- Prompt Milk Saathi is the farmer app. It shows fat, SNF, quantity and a passbook. — [Milk Saathi](https://www.promptdairytech.com/milk-saathi-app)
- Prompt's iSmart ultrasonic analyser does Bluetooth cloud calibration through a mobile app. Stated accuracy is ±0.1% for fat and ±0.15% for SNF. — [iSmart PDF](https://www.promptdairytech.com/wp-content/uploads/2024/03/ismart.pdf)
- NDDB AMCS runs in more than 26,000 societies across 12 states/UTs, according to a PIB document dated January 2026. It has a farmer app. NDERP is NDDB's dairy ERP, built on Frappe ERPNext and integrated with AMCS. — [PIB doc](https://static.pib.gov.in/WriteReadData/specificdocs/documents/2026/jan/doc202619754001.pdf); [NDDB ICT](https://beta.nddb.coop/?p=495); [AMCS farmer app](https://apppage.net/preview/coop.amcs.member)
- Hamari Dairy: morning and evening entry for cow, buffalo or mixed milk, priced by fat, SNF or fixed rate. It has automatic rate and bonus, 10-day, 15-day or monthly bills, advance and due tracking, SMS, multi-agent roles and farmer login. Languages are Hindi, English and Gujarati. It is offline-first and supports Bluetooth printers. Free, about 23K installs (AppGoblin estimate). — [AppGoblin](https://appgoblin.info/apps/com.hamari_dairy)
- MCS Dairy Billing: fat, SNF and CLR, Excel rate-chart import, purchase and sale, P&L. Bills are shared by SMS or WhatsApp. It is online with offline entries, and has 5 languages. Free trial, then a subscription (price not shown). About 237K installs. — [AppGoblin](https://appgoblin.info/apps/com.milkcollectionsystem)
- My Dairy Center (fat, SNF, CLR, shifts) and Simple Dairy (fat, fat/SNF and fixed-rate charts). — [My Dairy Center](https://appgoblin.info/apps/com.bharatdairy); [Simple Dairy](https://appgoblin.info/apps/com.gemsessence.milkdairy)
- Doodhwala-side apps: Milk Diary (Doodh Khata Hisab) builds monthly bills, with about 2.8K installs. DudhHisaab has separate cow and buffalo rates. Aapka Doodhwala exists too. — [Milk Diary](https://appgoblin.info/apps/com.milkkhata.diary); [DudhHisaab](https://appgoblin.info/apps/com.dudhhisaab.app); [Aapka Doodhwala](https://appgoblin.info/apps/milkmaster.aapkadoodhwala)
- OkCredit markets itself to dairy sellers. It logs daily delivery per household, shows a running balance the household can see, sends reminders, collects by UPI and works offline. Its free plan has unlimited customers. It does not mention fat, SNF, litres or rate per litre. — [OkCredit dairy](https://okcredit.in/khata-app-for-dairy-business)

### Inferences
- Small apps already cover the basics: fat/SNF rate charts, shifts, 10-day bills, advances and SMS. So feature parity alone will not stand out.
- General khata apps lack litre × rate and fat/SNF pricing. That is the gap a "dairy" shop type in a khata app could fill.

### Gaps
- I found no sourced details on Akashganga, Everest, or Lactosure/Ekomilk software. Searches returned nothing usable.
- I found no app named "Smart Dairy" or "Dairy Plus", and no ePashuHaat collection features. (ePashuHaat is a livestock marketplace, not collection software. That is from memory and not verified.)
- I could not get Khatabook's milkman usage from a primary source.

## Features, pricing, complaints, offline and device connection

### Takeaway
Small apps cost about ₹0 to ₹2,000 a year, and AMCS systems are around ₹1 lakh a year. Offline use and Bluetooth printers are standard. Analyser and scale data usually reaches the app through a vendor printer or DPU (RS-232 or Bluetooth), not directly to a phone.

### Cited Findings
- Liter Milk Collection starts at ₹480. Meri Dairy is about ₹1,500 to ₹2,000 a year and sends an SMS for every entry. Modern Webz is ₹2,000 a year, Shweta is ₹4,000 a year and QS is ₹12,000. Prompt AMCS is listed at ₹1,00,000 a year on one aggregator and "on request" on another. — [Techjockey Liter](https://www.techjockey.com/detail/liter-milk-collection-software); [SoftwareSuggest Meri Dairy](https://www.softwaresuggest.com/meri-dairy); [Techjockey Meri Dairy](https://techjockey.com/detail/meri-dairy); [SoftwareSuggest Prompt](https://www.softwaresuggest.com/prompt-automatic-milk-collection)
- Hamari Dairy and MCS both support offline entry with sync. — [Hamari](https://appgoblin.info/apps/com.hamari_dairy); [MCS](https://appgoblin.info/apps/com.milkcollectionsystem)
- One hardware bundle sells an analyser, scale and printer that send data over Bluetooth to the owner's own Android phone. — [Prompt MilkSetu](https://www.promptdairytech.com/?p=13265)

### Inferences
- A first version could rely on manual fat/SNF entry plus OCR of the analyser slip. Bluetooth import from common analysers that print serial strings could come later.
- Charging per-entry SMS is a known cost and also something users value. Moving to WhatsApp is a cost and richness argument.

### Gaps
- I could not access Play Store reviews. AppGoblin pages showed no ratings or reviews, so specific complaints are unsourced.
- I found no details on dispatch-to-dairy reconciliation (matching union bills against local collection) in any small app.

## Door-to-door doodhwala khata in the same shop type?

### Takeaway
Doodhwala needs (monthly litres × rate per household, skip days, UPI collection) are served by OkCredit and milk-diary apps. Collection-centre needs (buying from farmers by fat/SNF) are the mirror image. One "dairy" shop type with both a purchase side (farmers) and a sales side (households) is plausible, and MCS already combines purchase and sale.

### Cited Findings
- OkCredit's dairy page focuses on household delivery khata. — [OkCredit](https://okcredit.in/khata-app-for-dairy-business)
- MCS handles both milk purchase and milk sales, with P&L. — [AppGoblin MCS](https://appgoblin.info/apps/com.milkcollectionsystem)
- The Doodhwala delivery startup shut down and moved its customers to FreshToHome. — [OfficeChai](https://officechai.com/stories/milk-delivery-startup-doodhwala-stops-operations-redirects-customers-fresh-home-app/)

### Inferences
- Treat this as one shop type with modes: "buy from farmers" and "sell to homes".

### Gaps
- I have no data on how many doodhwalas use apps compared with paper diaries.

## WhatsApp Business API cost for per-collection messages (India)

### Takeaway
A utility template costs about ₹0.145 per delivered message in India. Since 1 October 2026, utility templates sent inside the 24-hour window are also charged, according to a reseller. Two slips a day for 100 farmers comes to about ₹870 a month, which is comparable to or cheaper than SMS.

### Cited Findings
- From 1 October 2026: service messages become chargeable after 1,000 free per number per month. Utility templates sent inside the window are no longer free. Utility and authentication cost ₹0.145 and marketing ₹1.09. Volume tiers still apply to utility. Source is AiSensy, a reseller, so verify against Meta. — [AiSensy Oct 2026](https://m.aisensy.com/blog/whatsapp-pricing-update-october-2026/)
- Other guides put utility at about ₹0.11 to ₹0.16 ($0.0014). — [MyOperator](https://myoperator.com/blog/whatsapp-business-api-pricing-india-2026); [ChatMaxima](https://chatmaxima.com/blog/whatsapp-business-api-pricing-2026-complete-guide/)

### Inferences
- 100 farmers × 2 shifts × 30 days = 6,000 messages, which at ₹0.145 is about ₹870 a month.
- Cost-saving options: send one daily summary instead of one message per shift, send the bill at the 10-day close, or use free click-to-chat links from the shopkeeper's own WhatsApp.

### Gaps
- I did not fetch Meta's official rate card directly.
