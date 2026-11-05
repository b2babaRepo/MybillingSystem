# Fragrance, bath and body, and men's grooming: beauty shop master item list

Scope: India, current as of Nov 2026. Seed rows for a billing app. No prices. "?" marks values I could not confirm. Pack sizes are common retail SKUs from memory of Nykaa, Amazon and brand listings. I did not check them against live listings in this session (I made only 2 searches), so treat every Packs value as roughly right and verify before shipping.

## GST and HSN after the 22 Sept 2025 rate changes

### Takeaway
GST 2.0 took effect on 22 Sept 2025. It cut talcum powder, face powder, shaving cream, aftershave lotion and toilet soap bars from 18% to 5%. Liquid soap, body wash, deodorant and perfume appear to stay at 18%. Sanitary napkins have been exempt since 2018 (a pre-2025 decision, still to confirm). I found no confirmation for razors or trimmers.

### Cited Findings
- The 56th GST Council meeting (3 Sept 2025) approved the changes, effective 22 Sept 2025. It removed the 12% and 28% slabs, leaving 5%, 18% and 40% — [TaxGuru](https://taxguru.in/?p=987577); [Business Standard](https://www.business-standard.com/economy/news/new-gst-rate-cut-what-gets-cheaper-expensive-5-18-percent-125092200215_1.html)
- Grooming products moved from 18% to 5%: talcum powder, face powder, shaving cream and after-shave lotion — [Upstox 5% list](https://upstox.com/news/personal-finance/tax/new-gst-rates-full-list-of-items-with-5-goods-and-services-tax/article-180697); [The Week](https://www.theweek.in/news/biz-tech/2025/09/04/gst-2-0-toothpaste-shampoos-shaving-creams-to-become-cheaper-but-not-mouthwash-like-listerine-and-colgate-plax-why.html)
- Toilet soap bars are now 5%; liquid soaps stay higher (18%) — [ClearTax old vs new](https://cleartax.in/s/new-vs-old-gst-rates-comparison); [Deccan Herald](https://www.deccanherald.com/amp/story/business%2Feconomy%2Frevised-gst-rates-full-list-of-items-that-will-get-cheaper-or-costlier-3711489)
- Hair oil, shampoo, toothpaste, soap and shaving cream went from 18% to 5% — [Covai Mail](https://covaimail.com/?p=19594)
- Sanitary napkins were 12% in 2017. A later Council decision exempted them (I recall July 2018; the source page's dates are inconsistent) — [Business Standard 2017](https://www.business-standard.com/amp/article/pti-stories/tax-incidence-on-sanitary-napkins-lowered-to-12-under-gst-117071001082_1.html); [India TV](https://www.indiatvnews.com/business/news-new-gst-rate-cuts-announced-here-are-the-list-of-items-that-might-get-cheaper-gst-council-meeting-new-delhi-piyush-goyal-announcements-key-rate-cuts-453950)
- One HSN database lists razors and blades (8212) at 18%, but it may predate GST 2.0 — [RegisterKaro](https://www.registerkaro.in/hsn/gst-rate-hsn-code-8212)

### Inferences (HSN and rates used in the tables)
- Soap bar 3401 11 → 5%. Liquid hand wash and body wash 3401 30 → 18%.
- Talcum and face powder 3304 91 → 5%.
- Shaving cream, foam and gel 3307 10 → 5%. Whether foam and gel count as "shaving cream" for the 5% rate is not explicit, so I mark them 5?.
- Aftershave lotion 3307 10 → 5%. Aftershave balm 5?.
- Deodorant and antiperspirant 3307 20 → 18%. Perfume, EDT, EDP, body mist and attar 3303 → 18%. Neither appears in any GST 2.0 cut list I found.
- Hair oil 3305 → 5%. Hair gel and wax 3305 90 → 18?. Beard oil: either 3305 at 5% or 3304 at 18%, so marked "?".
- Skin creams, scrubs, hand cream, foot cream, hair removal cream and men's face wash: 3304 99 at 18% (face wash is sometimes filed under 3401 30, also 18%).
- Razors and cartridges 8212 → 18?. Trimmers 8510 → 18?.
- Sanitary pads 9619 → 0 (exempt?). Cotton buds and cotton wool 5601 → 5?. Loofahs and bath sponges 3924 / 9603 → 18?. Bath salt 2501 / 3307 30 → 18?. Wax strips 3307 90 → 18?.

### Gaps
- I found no official CBIC notification text in this session for perfume, deodorant, razors, trimmers or pad exemption status. Verify the 3307 / 3303 / 8212 / 8510 rows against Notification 9/2025-CT(Rate) Schedules I and II.

## Seed rows: Fragrance

| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Fogg Body Spray Men | Fogg | Deodorant | 120, 150 | ml | 18 | 33072000 |
| Fogg Body Spray Women | Fogg | Deodorant | 120, 150 | ml | 18 | 33072000 |
| Fogg Scent Perfume | Fogg | Perfume | 100 | ml | 18 | 33030090 |
| Fogg Master Perfume | Fogg | Perfume | 100 | ml | 18 | 33030090 |
| Wild Stone Deodorant | Wild Stone | Deodorant | 150, 225 | ml | 18 | 33072000 |
| Wild Stone Code Perfume | Wild Stone | Perfume | 100 | ml | 18 | 33030090 |
| Wild Stone Edge Perfume | Wild Stone | Perfume | 100 | ml | 18 | 33030090 |
| Axe Deodorant Body Spray | Axe | Deodorant | 150, 215 | ml | 18 | 33072000 |
| Axe Signature Body Perfume | Axe | Perfume | 122, 154 | ml | 18 | 33030090 |
| Engage Deo Man | Engage | Deodorant | 150, 220 | ml | 18 | 33072000 |
| Engage Deo Woman | Engage | Deodorant | 150, 220 | ml | 18 | 33072000 |
| Engage On Pocket Perfume | Engage | Perfume | 18 | ml | 18 | 33030090 |
| Engage EDP | Engage | Perfume | 90, 100 | ml | 18 | 33030090 |
| Park Avenue Deodorant | Park Avenue | Deodorant | 150, 220 | ml | 18 | 33072000 |
| Park Avenue Voyage Perfume | Park Avenue | Perfume | 50, 100 | ml | 18 | 33030090 |
| Park Avenue Good Morning Perfume | Park Avenue | Perfume | 100 | ml | 18 | 33030090 |
| Denver Deodorant | Denver | Deodorant | 165, 200 | ml | 18 | 33072000 |
| Denver Perfume Hamilton | Denver | Perfume | 60, 100 | ml | 18 | 33030090 |
| Yardley Gentleman Deodorant | Yardley | Deodorant | 150, 220 | ml | 18 | 33072000 |
| Yardley English Lavender Deodorant | Yardley | Deodorant | 150 | ml | 18 | 33072000 |
| Yardley English Rose Body Spray | Yardley | Deodorant | 150 | ml | 18 | 33072000 |
| Nivea Men Deodorant Fresh Active | Nivea | Deodorant | 150 | ml | 18 | 33072000 |
| Nivea Women Deodorant Whitening | Nivea | Deodorant | 150 | ml | 18 | 33072000 |
| Nivea Roll On Men | Nivea | Roll-on | 50 | ml | 18 | 33072000 |
| Nivea Roll On Women | Nivea | Roll-on | 50 | ml | 18 | 33072000 |
| Set Wet Deodorant | Set Wet | Deodorant | 150 | ml | 18 | 33072000 |
| Set Wet Perfume Spray | Set Wet | Perfume | 120 | ml | 18 | 33030090 |
| Spinz Deo Women | Spinz | Deodorant | 150 | ml | 18 | 33072000 |
| Envy Perfume | Envy | Perfume | 60, 100 | ml | 18 | 33030090 |
| Rexona Roll On | Rexona | Roll-on | 25, 50 | ml | 18 | 33072000 |
| Dove Roll On | Dove | Roll-on | 40, 50 | ml | 18 | 33072000 |
| Old Spice Deodorant | Old Spice | Deodorant | 150 | ml | 18 | 33072000 |
| Bella Vita Luxury EDP Men | Bella Vita | Perfume | 20, 100 | ml | 18 | 33030090 |
| Bella Vita Luxury EDP Women | Bella Vita | Perfume | 20, 100 | ml | 18 | 33030090 |
| Bella Vita Perfume Gift Set | Bella Vita | Perfume | 4x20 | box | 18 | 33030090 |
| Skinn by Titan EDP Men | Skinn by Titan | Perfume | 20, 50, 100 | ml | 18 | 33030090 |
| Skinn by Titan EDP Women | Skinn by Titan | Perfume | 20, 50, 100 | ml | 18 | 33030090 |
| Villain EDP | Villain | Perfume | 100 | ml | 18 | 33030090 |
| Ajmal Wisal EDP | Ajmal | Perfume | 50 | ml | 18 | 33030090 |
| Ajmal Attar | Ajmal | Attar | 3, 6, 10, 12 | ml | 18 | 33030020 |
| Al Nuaim Attar | Al Nuaim | Attar | 6 | ml | 18 | 33030020 |
| Loose Attar (bottle) | Local | Attar | 3, 6, 8, 12 | ml | 18 | 33030020 |
| Body Mist (generic) | Plum? / Local | Body mist | 100, 150 | ml | 18 | 33030090 |
| Ponds Dreamflower Talc | Ponds | Talcum | 50, 100, 200, 400 | g | 5 | 33049110 |
| Ponds Sandal Talc | Ponds | Talcum | 50, 100, 200 | g | 5 | 33049110 |
| Nycil Prickly Heat Powder | Nycil | Talcum | 50, 100, 150 | g | 5 | 33049110 |
| Dermicool Prickly Heat Powder | Dermicool | Talcum | 75, 150 | g | 5 | 33049110 |
| Navratna Cool Talc | Navratna | Talcum | 100, 200, 400 | g | 5 | 33049110 |
| Cinthol Talc | Cinthol | Talcum | 100, 300 | g | 5 | 33049110 |
| Yardley Lavender Talc | Yardley | Talcum | 100, 250 | g | 5 | 33049110 |
| Santoor Talc | Santoor | Talcum | 100, 300 | g | 5 | 33049110 |
| Engage Talc | Engage | Talcum | 100 | g | 5 | 33049110 |
| Fogg Talc | Fogg | Talcum | 100 | g | 5? | 33049110 |
| Johnson's Baby Powder | Johnson's | Talcum | 100, 200, 400 | g | 5 | 33049110 |
| Gokul Santol Talc | Gokul | Talcum | 100 | g | 5 | 33049110 |
| Denver Talc | Denver | Talcum | 100 | g | 5 | 33049110 |

## Seed rows: Bath and body

| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Lux Soap | Lux | Soap | 41, 100, 150, 3x100, 4x150 | g | 5 | 34011190 |
| Dove Cream Beauty Bar | Dove | Soap | 50, 75, 100, 3x100 | g | 5 | 34011190 |
| Pears Pure and Gentle Soap | Pears | Soap | 75, 125, 3x125 | g | 5 | 34011190 |
| Santoor Sandal Soap | Santoor | Soap | 50, 100, 150, 4x100 | g | 5 | 34011190 |
| Medimix Ayurvedic Soap | Medimix | Soap | 75, 125, 3x125 | g | 5 | 34011190 |
| Lifebuoy Soap | Lifebuoy | Soap | 46, 100, 125, 4x125 | g | 5 | 34011190 |
| Dettol Original Soap | Dettol | Soap | 45, 75, 125, 4x125 | g | 5 | 34011190 |
| Cinthol Soap | Cinthol | Soap | 100, 3x100 | g | 5 | 34011190 |
| Fiama Gel Bar | Fiama | Soap | 125, 3x125 | g | 5 | 34011190 |
| Mysore Sandal Soap | Mysore Sandal | Soap | 75, 125, 150 | g | 5 | 34011190 |
| Hamam Soap | Hamam | Soap | 100, 150 | g | 5 | 34011190 |
| Liril Soap | Liril | Soap | 75, 125 | g | 5 | 34011190 |
| Godrej No.1 Soap | Godrej | Soap | 50, 100, 4x100 | g | 5 | 34011190 |
| Vivel Soap | Vivel | Soap | 100, 150 | g | 5 | 34011190 |
| Margo Neem Soap | Margo | Soap | 75, 100 | g | 5 | 34011190 |
| Patanjali Saundarya Soap | Patanjali | Soap | 75, 150 | g | 5 | 34011190 |
| Nivea Creme Soap | Nivea | Soap | 75, 125 | g | 5 | 34011190 |
| Himalaya Neem Turmeric Soap | Himalaya | Soap | 75, 125 | g | 5 | 34011190 |
| Moti Sandal Soap | Moti | Soap | 75, 150 | g | 5 | 34011190 |
| Dove Body Wash | Dove | Body wash | 190, 250, 800 | ml | 18 | 34013090 |
| Fiama Shower Gel | Fiama | Body wash | 100, 250 | ml | 18 | 34013090 |
| Nivea Shower Gel | Nivea | Body wash | 250, 500 | ml | 18 | 34013090 |
| Pears Body Wash | Pears | Body wash | 250 | ml | 18 | 34013090 |
| Lux Body Wash | Lux | Body wash | 245 | ml | 18 | 34013090 |
| Palmolive Body Wash | Palmolive | Body wash | 250, 750 | ml | 18 | 34013090 |
| Mamaearth Body Wash | Mamaearth | Body wash | 300 | ml | 18 | 34013090 |
| Plum BodyLovin Shower Gel | Plum | Body wash | 300 | ml | 18 | 34013090 |
| Bath Salt (Epsom / Scented) | Local / Ancient Living? | Bath salt | 200, 500 | g | 18? | 25010090? |
| Loofah Bath Sponge | Local / Vega | Bath accessory | 1 | piece | 18? | 96039000? |
| Natural Loofah | Local | Bath accessory | 1 | piece | 5? | 14049090? |
| Back Scrubber Belt | Vega? | Bath accessory | 1 | piece | 18? | 96039000? |
| St Ives Body Scrub | St Ives | Body scrub | 150, 283 | g | 18 | 33049990 |
| Mamaearth Coffee Body Scrub | Mamaearth | Body scrub | 100, 200 | g | 18 | 33049990 |
| mCaffeine Coffee Body Scrub | mCaffeine | Body scrub | 100 | g | 18 | 33049990 |
| WOW Body Scrub | WOW | Body scrub | 200 | ml | 18 | 33049990 |
| Dettol Liquid Hand Wash | Dettol | Hand wash | 175, 200, 750, 1500 | ml | 18 | 34013011 |
| Lifebuoy Hand Wash | Lifebuoy | Hand wash | 190, 750, 1500 | ml | 18 | 34013011 |
| Savlon Hand Wash | Savlon | Hand wash | 200, 750 | ml | 18 | 34013011 |
| Palmolive Hand Wash | Palmolive | Hand wash | 250 | ml | 18 | 34013011 |
| Himalaya Hand Wash | Himalaya | Hand wash | 250, 750 | ml | 18 | 34013011 |
| Dettol Hand Sanitizer | Dettol | Sanitizer | 50, 200, 500 | ml | 18 | 38089400 |
| Nivea Hand Cream | Nivea | Hand cream | 75, 100 | ml | 18 | 33049990 |
| Neutrogena Hand Cream | Neutrogena | Hand cream | 56 | g | 18 | 33049990 |
| Vaseline Hand Cream | Vaseline | Hand cream | 75 | ml | 18 | 33049990 |
| Krack Heel Repair Cream | Krack | Foot cream | 20, 25, 50 | g | 18 | 33049990 |
| Scholl Cracked Heel Cream | Scholl | Foot cream | 60 | ml | 18 | 33049990 |
| Himalaya Footcare Cream | Himalaya | Foot cream | 50 | g | 18 | 33049990 |
| Vaseline Body Lotion | Vaseline | Body lotion | 100, 200, 400 | ml | 18 | 33049990 |
| Nivea Body Lotion | Nivea | Body lotion | 120, 200, 400 | ml | 18 | 33049990 |
| V Wash Plus Intimate Wash | V Wash | Intimate hygiene | 100, 200, 350 | ml | 18 | 34013090? |
| Lactacyd Intimate Wash | Lactacyd | Intimate hygiene | 100, 200 | ml | 18 | 34013090? |
| Sirona Intimate Wash | Sirona | Intimate hygiene | 100 | ml | 18 | 34013090? |
| Veet Hair Removal Cream | Veet | Hair removal | 25, 50, 100 | g | 18 | 33049990 |
| Anne French Hair Removal Cream | Anne French | Hair removal | 25, 50 | g | 18 | 33049990 |
| Nair Hair Removal Cream | Nair | Hair removal | 25, 80? | g | 18 | 33049990 |
| Veet Wax Strips | Veet | Wax | 8, 12, 20 | packet | 18? | 33079090? |
| Wax Strips (Roll) | Local / Rica? | Wax | 1 | packet | 18? | 48239090? |
| Hot Wax Tin | Rica / Local | Wax | 600, 800 | g | 18? | 33079090? |
| Veet Men Hair Removal Spray | Veet | Hair removal | 150 | ml | 18 | 33049990 |
| Veet Face Hair Removal Cream | Veet | Hair removal | 50 | g | 18 | 33049990 |

## Seed rows: Men's grooming

| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Gillette Shaving Foam Regular | Gillette | Shaving | 196, 418 | g | 5? | 33071010 |
| Gillette Shaving Gel | Gillette | Shaving | 60, 195 | g | 5? | 33071010 |
| Gillette Shaving Cream | Gillette | Shaving | 30, 70, 93 | g | 5 | 33071010 |
| Old Spice Shaving Cream | Old Spice | Shaving | 30, 70 | g | 5 | 33071010 |
| Old Spice Shaving Foam | Old Spice | Shaving | 300? | ml | 5? | 33071010 |
| Park Avenue Shaving Cream | Park Avenue | Shaving | 25, 84 | g | 5 | 33071010 |
| Park Avenue Shaving Foam | Park Avenue | Shaving | 300 | ml | 5? | 33071010 |
| Godrej Shaving Cream | Godrej | Shaving | 25, 70 | g | 5 | 33071010 |
| Godrej Shaving Round | Godrej | Shaving | 50? | g | 5? | 33071010 |
| Denim Shaving Cream | Denim | Shaving | 30, 70 | g | 5 | 33071010 |
| Nivea Men Shaving Foam | Nivea | Shaving | 250 | ml | 5? | 33071010 |
| Bombay Shaving Company Shaving Foam | Bombay Shaving Company | Shaving | 264 | ml | 5? | 33071010 |
| Shaving Brush | Local / Gillette | Shaving accessory | 1 | piece | 18? | 96032900 |
| Gillette Guard Razor | Gillette | Razor | 1 | piece | 18? | 82121010 |
| Gillette Guard Cartridges | Gillette | Razor | 2, 4, 6, 8 | packet | 18? | 82122019? |
| Gillette Mach3 Razor | Gillette | Razor | 1 | piece | 18? | 82121010 |
| Gillette Mach3 Cartridges | Gillette | Razor | 2, 4, 8 | packet | 18? | 82122019? |
| Gillette Vector Plus Razor | Gillette | Razor | 1 | piece | 18? | 82121010 |
| Gillette Presto Use and Throw | Gillette | Razor | 1, 2 | packet | 18? | 82121090? |
| Gillette 7 O'Clock Blades | Gillette | Blade | 5, 10 | packet | 18? | 82122011? |
| Laser Platinum Blades | Laser | Blade | 5 | packet | 18? | 82122011? |
| Topaz Blades | Topaz | Blade | 5, 10 | packet | 18? | 82122011? |
| Ultra Max / Super Max Razor | Supermax | Razor | 1, 3 | packet | 18? | 82121090? |
| Old Spice After Shave Lotion | Old Spice | Aftershave | 50, 100, 150 | ml | 5 | 33071090 |
| Park Avenue After Shave Lotion | Park Avenue | Aftershave | 50, 100 | ml | 5 | 33071090 |
| Gillette After Shave Gel | Gillette | Aftershave | 60 | g | 5? | 33071090 |
| Nivea Men After Shave Balm | Nivea | Aftershave | 100 | ml | 5? | 33071090 |
| Beardo Beard Oil | Beardo | Beard care | 30, 50 | ml | 18? | 33059090? |
| Beardo Beard Wash | Beardo | Beard care | 100 | ml | 18 | 33059090? |
| Beardo Hair and Beard Growth Oil | Beardo | Beard care | 50 | ml | 5? | 33059011? |
| Ustraa Beard Growth Oil | Ustraa | Beard care | 35 | ml | 18? | 33059090? |
| Ustraa Beard Softener | Ustraa | Beard care | 50 | ml | 18? | 33049990? |
| Bombay Shaving Company Beard Oil | Bombay Shaving Company | Beard care | 30 | ml | 18? | 33059090? |
| Bombay Shaving Company Razor | Bombay Shaving Company | Razor | 1 | piece | 18? | 82121010 |
| The Man Company Beard Oil | The Man Company | Beard care | 30 | ml | 18? | 33059090? |
| The Man Company Beard Wash | The Man Company | Beard care | 100 | ml | 18 | 33059090? |
| Beard Balm / Wax | Beardo / Ustraa | Beard care | 50 | g | 18? | 33059090? |
| Philips Beard Trimmer BT1232 | Philips | Trimmer | 1 | piece | 18? | 85101000 |
| Philips OneBlade | Philips | Trimmer | 1 | piece | 18? | 85101000 |
| Mi Beard Trimmer 2C | Mi | Trimmer | 1 | piece | 18? | 85102000 |
| Havells BT5100C Trimmer | Havells | Trimmer | 1 | piece | 18? | 85102000 |
| Nova Trimmer | Nova | Trimmer | 1 | piece | 18? | 85102000 |
| Philips OneBlade Replacement Blade | Philips | Trimmer accessory | 1 | piece | 18? | 85109000 |
| Garnier Men Acno Fight Face Wash | Garnier | Men face wash | 50, 100, 150 | g | 18 | 33049990 |
| Garnier Men Oil Clear Face Wash | Garnier | Men face wash | 50, 100 | g | 18 | 33049990 |
| Nivea Men Dark Spot Face Wash | Nivea | Men face wash | 50, 100 | g | 18 | 33049990 |
| Himalaya Men Face Wash | Himalaya | Men face wash | 50, 100 | ml | 18 | 33049990 |
| Ponds Men Face Wash | Ponds | Men face wash | 50, 100 | g | 18 | 33049990 |
| Beardo De-Tan Face Wash | Beardo | Men face wash | 100 | ml | 18 | 33049990 |
| Set Wet Hair Gel | Set Wet | Hair styling | 50, 100 | ml | 18? | 33059090 |
| Gatsby Hair Wax | Gatsby | Hair styling | 25, 75 | g | 18? | 33059090 |
| Gatsby Hair Gel | Gatsby | Hair styling | 100? | g | 18? | 33059090 |
| Brylcreem Hair Cream | Brylcreem | Hair styling | 50, 100 | ml | 18? | 33059090 |
| Beardo Hair Wax | Beardo | Hair styling | 75 | g | 18? | 33059090 |
| Ustraa Hair Wax | Ustraa | Hair styling | 50 | g | 18? | 33059090 |
| Parachute Advansed Men Hair Cream | Parachute | Hair styling | 100 | g | 18? | 33059090 |

## Seed rows: Feminine and personal care

| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Whisper Choice Pads | Whisper | Sanitary pad | 6, 20 | packet | 0? | 96190010 |
| Whisper Ultra Clean XL+ | Whisper | Sanitary pad | 7, 15, 30, 50 | packet | 0? | 96190010 |
| Whisper Bindazzz Nights | Whisper | Sanitary pad | 7, 15 | packet | 0? | 96190010 |
| Stayfree Secure XL | Stayfree | Sanitary pad | 6, 20, 40 | packet | 0? | 96190010 |
| Stayfree Dry Max | Stayfree | Sanitary pad | 8, 28 | packet | 0? | 96190010 |
| Sofy Antibacteria XL | Sofy | Sanitary pad | 7, 15, 28 | packet | 0? | 96190010 |
| Carefree Panty Liners | Carefree | Panty liner | 20 | packet | 0? | 96190010 |
| Johnson's Cotton Buds | Johnson's | Cotton buds | 30, 100 | box | 5? | 56012110? |
| Ear Buds (generic) | Local | Cotton buds | 100, 200 | box | 5? | 56012110? |
| Cotton Wool Roll | Local / Cotton King? | Cotton | 50, 100, 400 | g | 5? | 30051090? |
| Cotton Pads (Makeup) | Swiss Beauty? / Local | Cotton | 80, 100 | packet | 5? | 56012110? |

Row count: about 200.
