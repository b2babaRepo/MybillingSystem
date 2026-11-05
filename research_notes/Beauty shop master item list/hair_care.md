# Hair care products: beauty shop master item list (seed data)

## GST rate and HSN after the 22 Sept 2025 changes

### Takeaway
From 22 Sept 2025, only **hair oil, shampoo and mehendi paste in cones** moved to 5%. Every other item under heading 3305 stays at 18%: conditioner, serum, mask, gel/wax, hair colour/dye, developer, bleach, spray, smoothening/rebonding and perm preparations. Salon services moved to 5% (without ITC), but that is a service rate and does not change the rate on goods.

### Cited Findings
- Hair oil, shampoo, soap and toothpaste cut from 18% to 5% from 22 Sept 2025 — [Upstox](https://upstox.com/news/personal-finance/tax/new-gst-rates-full-list-of-items-with-5-goods-and-services-tax/article-180697); [GimBooks](https://www.gimbooks.com/blog/new-gst-rates-in-india-2025/)
- Notification 9/2025-CT(Rate) Sch. I covers hair oil and shampoo at 5%. Under 3305, everything other than hair oil, shampoo and mehendi paste in cones is 18%; conditioner, serum, hair colour and gel are named — [Taxgarden](https://taxgarden.in/blog/income-tax-gst-for-cosmetics-beauty-products-shop-owners-india-ay-2026-27)
- Hair dye got no GST cut (Nuvama, cited by) — [Upstox](https://upstox.com/news/market-news/stocks/gst-2-0-gst-cuts-elude-these-key-items-analysts-and-fmcg-players-say-it-is-surprising/article-180835)
- Salon services moved to 5% — [Upstox](https://upstox.com/news/personal-finance/tax/salon-gym-yoga-centre-services-to-get-cheaper-after-gst-slashed-to-5/article-180712)
- Conflicts: some HSN lookup sites still show hair oil at 18% ([Busy](https://busy.in/hsn/oil-hsn-code), [IncorpX 33059019](https://www.incorpx.io/hsn-codes/code-33059019)) and shampoo at 18% ([Mark IT](https://markitsolutions-website.pages.dev/hsn/3305)). These look stale against the notification-based sources. DISYTAX shows dye at 9%, which is likely the CGST half ([DISYTAX](https://disytax.com/hsn-chapter-33-cosmetics-perfumery-gst/)).

### Inferences
- 8-digit HSN used below (Indian tariff from memory, not re-verified this session; needs a CA check): shampoo 33051090; hair oil 33059011 (perfumed) / 33059019 (other); hair cream 33059030 ?; hair dye 33059040 ?; hair fixer/gel/wax 33059050 ?; other hair preparations (serum, mask, spa, keratin, bleach, developer) 33059090; hair spray 33053000; perm/rebonding/straightening 33052000 ?.
- Pure coconut oil (Parachute, Nihar) is labelled edible and is often billed under 1513 (5%), so the rate is the same either way.
- Henna/mehndi powder is probably HSN 1404 90 at 5% ?. Mehendi cones are explicitly 5%. Kali mehndi / black henna is a chemical dye, so 3305 at 18%.
- Medicated items (minoxidil, ketoconazole, anti-lice) fall under 3004 as drugs: 5% ?, check.
- Combo packs (shampoo + conditioner) take the higher rate as mixed supply, so 18%.

### Gaps
- I did not fetch the official notification text. Pack sizes were not verified this session; they come from general market knowledge of Nykaa/Amazon/brand listings, and doubtful values are marked ?.

## Seed rows

### Shampoos (5%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Clinic Plus Strong & Long Shampoo | Clinic Plus | Shampoo | 80 ml, 175 ml, 355 ml, 650 ml | bottle | 5% | 33051090 |
| Clinic Plus Shampoo Sachet | Clinic Plus | Shampoo | 1 Re sachet, 16 sachet strip | packet | 5% | 33051090 |
| Sunsilk Stunning Black Shine Shampoo | Sunsilk | Shampoo | 80 ml, 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Sunsilk Long & Healthy Growth Shampoo | Sunsilk | Shampoo | 80 ml, 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Sunsilk Thick & Long Shampoo | Sunsilk | Shampoo | 80 ml, 180 ml, 340 ml | bottle | 5% | 33051090 |
| Sunsilk Shampoo Sachet | Sunsilk | Shampoo | 16 sachet strip | packet | 5% | 33051090 |
| Dove Intense Repair Shampoo | Dove | Shampoo | 80 ml, 180 ml, 340 ml, 650 ml, 1 L | bottle | 5% | 33051090 |
| Dove Hair Fall Rescue Shampoo | Dove | Shampoo | 80 ml, 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Dove Daily Shine Shampoo | Dove | Shampoo | 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Dove Shampoo Sachet | Dove | Shampoo | 16 sachet strip | packet | 5% | 33051090 |
| Pantene Hair Fall Control Shampoo | Pantene | Shampoo | 75 ml, 180 ml, 340 ml, 650 ml, 1 L | bottle | 5% | 33051090 |
| Pantene Silky Smooth Care Shampoo | Pantene | Shampoo | 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Pantene Shampoo Sachet | Pantene | Shampoo | 16 sachet strip | packet | 5% | 33051090 |
| Head & Shoulders Cool Menthol Shampoo | Head & Shoulders | Anti-dandruff shampoo | 72 ml, 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Head & Shoulders Smooth & Silky Shampoo | Head & Shoulders | Anti-dandruff shampoo | 72 ml, 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Head & Shoulders Anti Hairfall Shampoo | Head & Shoulders | Anti-dandruff shampoo | 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Head & Shoulders Shampoo Sachet | Head & Shoulders | Anti-dandruff shampoo | 16 sachet strip | packet | 5% | 33051090 |
| TRESemmé Keratin Smooth Shampoo | TRESemmé | Shampoo | 85 ml, 185 ml, 340 ml, 580 ml, 1 L | bottle | 5% | 33051090 |
| TRESemmé Hair Fall Defense Shampoo | TRESemmé | Shampoo | 185 ml, 340 ml, 580 ml, 1 L | bottle | 5% | 33051090 |
| TRESemmé Pro Protect Sulphate Free Shampoo | TRESemmé | Shampoo | 340 ml, 580 ml | bottle | 5% | 33051090 |
| L'Oréal Paris Total Repair 5 Shampoo | L'Oréal Paris | Shampoo | 82.5 ml, 192.5 ml, 340 ml, 650 ml, 1 L | bottle | 5% | 33051090 |
| L'Oréal Paris Hyaluron Moisture Shampoo | L'Oréal Paris | Shampoo | 180 ml, 340 ml, 440 ml ? | bottle | 5% | 33051090 |
| L'Oréal Paris Extraordinary Oil Shampoo | L'Oréal Paris | Shampoo | 192.5 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| L'Oréal Paris Fall Resist 3X Shampoo | L'Oréal Paris | Shampoo | 192.5 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Himalaya Anti-Hair Fall Shampoo | Himalaya | Shampoo | 80 ml, 180 ml, 400 ml, 700 ml | bottle | 5% | 33051090 |
| Himalaya Anti-Dandruff Shampoo | Himalaya | Anti-dandruff shampoo | 80 ml, 180 ml, 400 ml, 700 ml | bottle | 5% | 33051090 |
| Himalaya Protein Shampoo | Himalaya | Shampoo | 100 ml, 200 ml, 400 ml, 700 ml | bottle | 5% | 33051090 |
| Mamaearth Onion Shampoo | Mamaearth | Shampoo | 80 ml ?, 250 ml, 400 ml, 650 ml ? | bottle | 5% | 33051090 |
| Mamaearth Rice Water Shampoo | Mamaearth | Shampoo | 250 ml, 400 ml | bottle | 5% | 33051090 |
| Mamaearth Tea Tree Anti-Dandruff Shampoo | Mamaearth | Anti-dandruff shampoo | 250 ml, 400 ml | bottle | 5% | 33051090 |
| Indulekha Bringha Shampoo | Indulekha | Shampoo | 100 ml, 200 ml, 340 ml | bottle | 5% | 33051090 |
| Chik Shampoo Sachet | Chik | Shampoo | 1 Re sachet strip, 2 Re sachet strip | packet | 5% | 33051090 |
| Meera Herbal Shampoo | Meera | Shampoo | 80 ml, 180 ml, 340 ml | bottle | 5% | 33051090 |
| Meera Shampoo Sachet | Meera | Shampoo | sachet strip | packet | 5% | 33051090 |
| Nyle Naturals Anti-Hairfall Shampoo | Nyle | Shampoo | 90 ml, 180 ml, 400 ml, sachet strip | bottle | 5% | 33051090 |
| Garnier Fructis Long & Strong Shampoo | Garnier | Shampoo | 175 ml, 340 ml, 640 ml ? | bottle | 5% | 33051090 |
| Biotique Bio Green Apple Shampoo | Biotique | Shampoo | 180 ml, 340 ml, 650 ml | bottle | 5% | 33051090 |
| Patanjali Kesh Kanti Shampoo | Patanjali | Shampoo | 100 ml, 200 ml, 450 ml | bottle | 5% | 33051090 |
| Kesh King Ayurvedic Shampoo | Kesh King | Shampoo | 80 ml, 200 ml, 340 ml | bottle | 5% | 33051090 |
| Nizoral Anti-Dandruff Shampoo | Nizoral | Medicated shampoo | 50 ml, 100 ml | bottle | 5% ? | 3004 ? |
| Johnson's Baby Shampoo | Johnson's | Baby shampoo | 100 ml, 200 ml, 475 ml | bottle | 5% | 33051090 |
| Himalaya Gentle Baby Shampoo | Himalaya | Baby shampoo | 100 ml, 200 ml, 400 ml | bottle | 5% | 33051090 |

### Conditioners, serums, masks (18%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Dove Intense Repair Conditioner | Dove | Conditioner | 80 ml, 180 ml, 335 ml | bottle | 18% | 33059090 |
| Pantene Hair Fall Control Conditioner | Pantene | Conditioner | 75 ml, 180 ml, 335 ml | bottle | 18% | 33059090 |
| Sunsilk Stunning Black Shine Conditioner | Sunsilk | Conditioner | 80 ml, 180 ml | bottle | 18% | 33059090 |
| TRESemmé Keratin Smooth Conditioner | TRESemmé | Conditioner | 80 ml, 190 ml, 340 ml | bottle | 18% | 33059090 |
| L'Oréal Paris Total Repair 5 Conditioner | L'Oréal Paris | Conditioner | 71.5 ml, 175 ml, 340 ml | bottle | 18% | 33059090 |
| Head & Shoulders Smooth & Silky Conditioner | Head & Shoulders | Conditioner | 180 ml | bottle | 18% | 33059090 |
| Himalaya Protein Conditioner | Himalaya | Conditioner | 100 ml, 200 ml | bottle | 18% | 33059090 |
| Mamaearth Onion Conditioner | Mamaearth | Conditioner | 250 ml | bottle | 18% | 33059090 |
| Livon Hair Serum | Livon | Hair serum | 2 ml sachet, 20 ml, 50 ml, 100 ml | bottle | 18% | 33059090 |
| Streax Hair Serum Walnut Oil | Streax | Hair serum | 45 ml, 100 ml | bottle | 18% | 33059090 |
| L'Oréal Paris Extraordinary Oil Serum | L'Oréal Paris | Hair serum | 30 ml, 100 ml | bottle | 18% | 33059090 |
| TRESemmé Keratin Smooth Serum | TRESemmé | Hair serum | 50 ml, 100 ml ? | bottle | 18% | 33059090 |
| Mamaearth Onion Hair Serum | Mamaearth | Hair serum | 100 ml | bottle | 18% | 33059090 |
| Minimalist Hair Growth Actives 18% Serum | Minimalist | Hair serum | 30 ml | bottle | 18% | 33059090 |
| Set Wet Hair Serum | Set Wet | Hair serum | 50 ml ?, 100 ml | bottle | 18% | 33059090 |
| Dove Intense Repair Hair Mask | Dove | Hair mask | 300 ml ? | box | 18% | 33059090 |
| L'Oréal Paris Total Repair 5 Hair Mask | L'Oréal Paris | Hair mask | 200 ml | box | 18% | 33059090 |
| Mamaearth Onion Hair Mask | Mamaearth | Hair mask | 200 ml | box | 18% | 33059090 |
| Garnier Fructis Hair Food Mask | Garnier | Hair mask | 390 ml ? | box | 18% | 33059090 |
| Dove Shampoo + Conditioner Combo | Dove | Combo | 180 ml + 180 ml | box | 18% | 3305 |

### Hair oils (5%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Parachute Coconut Oil | Parachute | Coconut oil | 25 ml, 50 ml, 100 ml, 175 ml, 250 ml, 500 ml, 1 L | bottle | 5% | 1513 / 33059019 ? |
| Parachute Advansed Jasmine Coconut Hair Oil | Parachute | Hair oil | 45 ml, 90 ml, 180 ml, 300 ml | bottle | 5% | 33059011 |
| Parachute Advansed Aloe Vera Hair Oil | Parachute | Hair oil | 75 ml, 150 ml, 250 ml | bottle | 5% | 33059011 |
| Parachute Advansed Gold Hair Oil | Parachute | Hair oil | 100 ml, 200 ml, 400 ml | bottle | 5% | 33059011 |
| Parachute Advansed Ayurvedic Hot Oil | Parachute | Hair oil | 90 ml, 190 ml, 300 ml | bottle | 5% | 33059019 |
| Dabur Amla Hair Oil | Dabur | Hair oil | 45 ml, 90 ml, 180 ml, 275 ml, 450 ml | bottle | 5% | 33059011 |
| Dabur Vatika Enriched Coconut Hair Oil | Dabur | Hair oil | 75 ml, 150 ml, 300 ml, 600 ml | bottle | 5% | 33059011 |
| Dabur Almond Hair Oil | Dabur | Hair oil | 100 ml, 200 ml, 500 ml | bottle | 5% | 33059011 |
| Dabur Anmol Jasmine Coconut Oil ? | Dabur | Hair oil | 100 ml, 200 ml | bottle | 5% | 33059011 |
| Bajaj Almond Drops Hair Oil | Bajaj | Hair oil | 50 ml, 100 ml, 200 ml, 300 ml, 500 ml, 650 ml | bottle | 5% | 33059011 |
| Bajaj Almond Drops Sachet | Bajaj | Hair oil | sachet strip | packet | 5% | 33059011 |
| Bajaj Brahmi Amla Hair Oil | Bajaj | Hair oil | 100 ml, 200 ml, 300 ml | bottle | 5% | 33059011 |
| Navratna Ayurvedic Cool Oil | Navratna | Cool hair oil | 50 ml, 100 ml, 200 ml, 300 ml, 500 ml | bottle | 5% | 33059011 |
| Navratna Almond Cool Oil | Navratna | Cool hair oil | 100 ml, 200 ml | bottle | 5% | 33059011 |
| Navratna Extra Thanda Oil | Navratna | Cool hair oil | 100 ml, 200 ml | bottle | 5% | 33059011 |
| Indulekha Bringha Hair Oil | Indulekha | Ayurvedic hair oil | 50 ml, 100 ml, 250 ml | bottle | 5% | 33059019 ? |
| Kesh King Ayurvedic Hair Oil | Kesh King | Ayurvedic hair oil | 60 ml, 120 ml, 300 ml | bottle | 5% | 33059019 ? |
| Hair & Care Fruit Oils Green | Hair & Care | Hair oil | 50 ml, 100 ml, 200 ml, 300 ml | bottle | 5% | 33059011 |
| Nihar Naturals Shanti Amla Badam Hair Oil | Nihar | Hair oil | 30 ml, 60 ml, 120 ml, 200 ml, 500 ml | bottle | 5% | 33059011 |
| Nihar Shanti Amla Sachet | Nihar | Hair oil | 1 Re / 2 Re sachet strip | packet | 5% | 33059011 |
| Nihar Naturals Coconut Oil | Nihar | Coconut oil | 50 ml, 100 ml, 200 ml, 500 ml | bottle | 5% | 1513 / 33059019 ? |
| Keo Karpin Hair Oil | Keo Karpin | Hair oil | 100 ml, 200 ml, 300 ml ? | bottle | 5% | 33059011 |
| Patanjali Kesh Kanti Hair Oil | Patanjali | Ayurvedic hair oil | 100 ml, 300 ml ? | bottle | 5% | 33059019 |
| Mamaearth Onion Hair Oil | Mamaearth | Hair oil | 150 ml, 250 ml | bottle | 5% | 33059011 |
| Sesa Ayurvedic Hair Oil | Sesa | Ayurvedic hair oil | 90 ml, 200 ml ? | bottle | 5% | 33059019 |
| Himalaya Anti-Hair Fall Hair Oil | Himalaya | Hair oil | 100 ml, 200 ml | bottle | 5% | 33059011 |
| Emami 7 Oils in One | Emami | Hair oil | 50 ml, 100 ml, 200 ml, 300 ml | bottle | 5% | 33059011 |
| Castor Oil (hair use) | Generic | Hair oil | 100 ml, 200 ml, 500 ml | bottle | 5% ? | 1515 / 33059019 ? |

### Styling: gels, wax, cream, spray (18%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Set Wet Hair Gel Wet Look | Set Wet | Hair gel | 50 ml, 100 ml, 250 ml | bottle | 18% | 33059050 ? |
| Set Wet Hair Gel Cool Hold | Set Wet | Hair gel | 50 ml, 100 ml, 250 ml | bottle | 18% | 33059050 ? |
| Set Wet Hair Wax | Set Wet | Hair wax | 60 g ? | box | 18% | 33059050 ? |
| Set Wet Hair Spray | Set Wet | Hair spray | 200 ml | bottle | 18% | 33053000 |
| Gatsby Moving Rubber Hair Wax | Gatsby | Hair wax | 25 g, 80 g | box | 18% | 33059050 ? |
| Gatsby Water Gloss Hair Gel | Gatsby | Hair gel | 150 g ?, 300 g ? | box | 18% | 33059050 ? |
| Gatsby Set & Keep Hair Spray | Gatsby | Hair spray | 250 ml | bottle | 18% | 33053000 |
| Brylcreem Original Hair Cream | Brylcreem | Hair cream | 75 g ?, 210 ml ? | box | 18% | 33059030 ? |
| Brylcreem Hair Gel | Brylcreem | Hair gel | 150 ml ? | bottle | 18% | 33059050 ? |
| Parachute After Shower Hair Cream | Parachute | Hair cream | 50 g, 100 g, 140 g | bottle | 18% | 33059030 ? |
| Ustraa Hair Wax | Ustraa | Hair wax | 100 g | box | 18% | 33059050 ? |
| Beardo Hair Wax | Beardo | Hair wax | 75 g | box | 18% | 33059050 ? |
| Streax Professional Hair Spray | Streax Professional | Hair spray | 200 ml, 420 ml ? | bottle | 18% | 33053000 |
| Schwarzkopf Silhouette Hair Spray | Schwarzkopf Professional | Hair spray | 300 ml, 500 ml | bottle | 18% | 33053000 |
| L'Oréal Professionnel Infinium Hair Spray | L'Oréal Professionnel | Hair spray | 300 ml, 500 ml | bottle | 18% | 33053000 |
| Heat Protection Spray (salon brands) | Various | Heat protect | 125 ml ?, 150 ml ? | bottle | 18% | 33059090 |

### Retail hair colour, dye and henna (dye 18%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Godrej Expert Rich Crème Hair Colour | Godrej | Hair colour | 20 g+20 ml, 40 g+40 ml ? | box | 18% | 33059040 ? |
| Godrej Expert Easy 5 Minute Shampoo Hair Colour | Godrej | Shampoo hair colour | 20 ml sachet, 5 sachet pack | packet | 18% | 33059040 ? |
| Godrej Hair Dye Powder Natural Black | Godrej | Dye powder | 3 g sachet, sachet strip | packet | 18% | 33059040 ? |
| Godrej Nupur Henna | Godrej | Henna powder | 120 g ?, 400 g ? | packet | 5% ? | 1404 / 33059040 ? |
| Garnier Color Naturals Crème | Garnier | Hair colour | small pack, standard pack ? | box | 18% | 33059040 ? |
| Garnier Black Naturals | Garnier | Hair colour | 20 ml+20 g ? | box | 18% | 33059040 ? |
| L'Oréal Paris Excellence Crème Hair Colour | L'Oréal Paris | Hair colour | 1 kit (72 ml+100 g ?) | box | 18% | 33059040 ? |
| L'Oréal Paris Casting Crème Gloss | L'Oréal Paris | Hair colour | 1 kit | box | 18% | 33059040 ? |
| Streax Insta Shampoo Hair Colour | Streax | Shampoo hair colour | 25 ml sachet, 4 sachet pack ? | packet | 18% | 33059040 ? |
| Streax Cream Hair Colour | Streax | Hair colour | 25 ml+25 g ?, 120 ml ? | box | 18% | 33059040 ? |
| BBLUNT Salon Secret Crème Hair Colour | BBLUNT | Hair colour | 100 g ? | box | 18% | 33059040 ? |
| Indica Easy 10 Minute Hair Colour | Indica | Shampoo hair colour | 18 ml sachet ?, 5 sachet pack | packet | 18% | 33059040 ? |
| Indus Valley Bio Organic Hair Colour | Indus Valley | Herbal hair colour | 110 g ? | box | 18% | 33059040 ? |
| Black Rose Kali Mehndi | Black Rose | Black henna dye | 10 g sachet, sachet strip | packet | 18% | 33059040 ? |
| Kali Mehndi (generic) | Generic | Black henna dye | 10 g, 15 g | packet | 18% | 33059040 ? |
| Bigen Speedy Hair Colour Cream | Bigen | Hair colour | 40 g+40 g ? | box | 18% | 33059040 ? |
| Bigen Permanent Powder Hair Colour | Bigen | Dye powder | 6 g | packet | 18% | 33059040 ? |
| Super Vasmol 33 Kesh Kala Oil | Vasmol | Dye oil | 50 ml, 100 ml | bottle | 18% | 33059040 ? |
| Nisha Natural Henna Based Hair Colour | Nisha | Henna hair colour | 15 g sachet, box of sachets | packet | 18% ? | 33059040 ? |
| Nisha Creme Hair Colour | Nisha | Hair colour | 1 kit ? | box | 18% | 33059040 ? |
| Sojat Henna (Mehndi) Powder | Generic | Henna powder | 100 g, 250 g, 500 g, 1 kg | packet | 5% ? | 14049090 ? |
| Patanjali Mehandi Powder | Patanjali | Henna powder | 100 g ? | packet | 5% ? | 1404 ? |
| Mehendi Cone | Generic | Mehendi cone | 25 g cone, 12 cone box | box | 5% | 3305 / 1404 ? |
| Indigo Powder | Generic | Herbal dye | 100 g, 250 g | packet | 5% / 18% ? | 1404 / 3204 ? |
| Amla / Reetha / Shikakai Powder | Generic | Herbal hair wash | 100 g, 200 g | packet | 5% ? | 1211 / 3305 ? |

### Salon professional: colour, developer, bleach (18%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| L'Oréal Professionnel Majirel Hair Colour | L'Oréal Professionnel | Pro colour tube | 50 g ?, 100 g ? | piece | 18% | 33059040 ? |
| L'Oréal Professionnel INOA Hair Colour | L'Oréal Professionnel | Pro colour tube | 60 g | piece | 18% | 33059040 ? |
| L'Oréal Professionnel Dia Richesse | L'Oréal Professionnel | Demi colour | 50 ml | piece | 18% | 33059040 ? |
| L'Oréal Professionnel Oxydant 20 vol | L'Oréal Professionnel | Developer | 75 ml ?, 1 L | bottle | 18% | 33059090 ? |
| L'Oréal Professionnel Oxydant 30 vol | L'Oréal Professionnel | Developer | 1 L | bottle | 18% | 33059090 ? |
| L'Oréal Professionnel Oxydant 40 vol | L'Oréal Professionnel | Developer | 1 L | bottle | 18% | 33059090 ? |
| L'Oréal Professionnel Blond Studio Bleach | L'Oréal Professionnel | Bleach powder | 500 g ? | box | 18% | 33059090 |
| Schwarzkopf Igora Royal Hair Colour | Schwarzkopf Professional | Pro colour tube | 60 ml | piece | 18% | 33059040 ? |
| Schwarzkopf Igora Royal Developer 6/9/12% | Schwarzkopf Professional | Developer | 60 ml ?, 1 L | bottle | 18% | 33059090 ? |
| Schwarzkopf BlondMe Bleach | Schwarzkopf Professional | Bleach powder | 450 g ? | box | 18% | 33059090 |
| Schwarzkopf Essensity Hair Colour | Schwarzkopf Professional | Pro colour tube | 60 ml | piece | 18% | 33059040 ? |
| Matrix SoColor Hair Colour | Matrix | Pro colour tube | 90 ml ? | piece | 18% | 33059040 ? |
| Matrix Color Sync | Matrix | Demi colour | 90 ml ? | piece | 18% | 33059040 ? |
| Matrix Cream Developer 20/30/40 vol | Matrix | Developer | 1 L | bottle | 18% | 33059090 ? |
| Matrix Light Master Bleach | Matrix | Bleach powder | 500 g ? | box | 18% | 33059090 |
| Wella Koleston Perfect Hair Colour | Wella Professionals | Pro colour tube | 60 ml | piece | 18% | 33059040 ? |
| Wella Welloxon Developer 6/9/12% | Wella Professionals | Developer | 1 L | bottle | 18% | 33059090 ? |
| Wella Blondor Bleach | Wella Professionals | Bleach powder | 400 g ?, 800 g ? | box | 18% | 33059090 |
| Streax Professional Argan Secrets Hair Colour | Streax Professional | Pro colour tube | 60 g ? | piece | 18% | 33059040 ? |
| Streax Professional Developer 20/30/40 vol | Streax Professional | Developer | 60 ml ?, 1 L | bottle | 18% | 33059090 ? |
| Streax Professional Bleach Powder | Streax Professional | Bleach powder | 500 g ? | packet | 18% | 33059090 |
| Godrej Professional Hair Colour ? | Godrej Professional | Pro colour tube | 100 g ? | piece | 18% | 33059040 ? |
| Godrej Professional Developer ? | Godrej Professional | Developer | 1 L | bottle | 18% | 33059090 ? |
| Hydrogen Peroxide Developer (generic) | Generic | Developer | 100 ml, 500 ml, 1 L | bottle | 18% | 2847 / 33059090 ? |

### Salon professional: care, spa, smoothening (shampoo 5%, rest 18%)
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| L'Oréal Professionnel Absolut Repair Shampoo | L'Oréal Professionnel | Pro shampoo | 300 ml, 1.5 L | bottle | 5% | 33051090 |
| L'Oréal Professionnel Absolut Repair Mask | L'Oréal Professionnel | Pro hair mask | 250 ml, 500 ml | box | 18% | 33059090 |
| L'Oréal Professionnel Absolut Repair Conditioner | L'Oréal Professionnel | Pro conditioner | 200 ml, 750 ml | bottle | 18% | 33059090 |
| L'Oréal Professionnel Vitamino Color Shampoo | L'Oréal Professionnel | Pro shampoo | 300 ml, 1.5 L | bottle | 5% | 33051090 |
| L'Oréal Professionnel Xtenso Straightening Cream | L'Oréal Professionnel | Rebonding cream | 250 ml ?, 400 ml ? | box | 18% | 33052000 ? |
| L'Oréal Professionnel Liss Unlimited Serum | L'Oréal Professionnel | Pro serum | 125 ml | bottle | 18% | 33059090 |
| Schwarzkopf BC Bonacure Repair Shampoo | Schwarzkopf Professional | Pro shampoo | 250 ml, 1 L | bottle | 5% | 33051090 |
| Schwarzkopf BC Bonacure Repair Mask | Schwarzkopf Professional | Pro hair mask | 200 ml, 500 ml ? | box | 18% | 33059090 |
| Schwarzkopf Keratin Smooth Perfect Shampoo | Schwarzkopf Professional | Pro shampoo | 250 ml ?, 1 L | bottle | 5% | 33051090 |
| Schwarzkopf Strait Styling Glatt Kit | Schwarzkopf Professional | Rebonding cream | kit ? | box | 18% | 33052000 ? |
| Schwarzkopf Spa Essence Hair Mask ? | Schwarzkopf Professional | Hair spa cream | 500 ml ? | box | 18% | 33059090 |
| Matrix Opti.Care Shampoo | Matrix | Pro shampoo | 200 ml, 350 ml, 1 L | bottle | 5% | 33051090 |
| Matrix Opti.Care Mask | Matrix | Pro hair mask | 196 g, 490 g | box | 18% | 33059090 |
| Matrix Opti.Smooth Straightening Cream | Matrix | Rebonding cream | 250 ml ? | box | 18% | 33052000 ? |
| Biolage Smoothproof Shampoo | Biolage | Pro shampoo | 200 ml, 400 ml, 1 L | bottle | 5% | 33051090 |
| Biolage HydraSource Shampoo | Biolage | Pro shampoo | 200 ml, 400 ml, 1 L | bottle | 5% | 33051090 |
| Biolage Smoothproof Serum | Biolage | Pro serum | 100 ml | bottle | 18% | 33059090 |
| Biolage Deep Treatment Hair Spa Pack | Biolage | Hair spa cream | 100 g ?, 490 g ? | box | 18% | 33059090 |
| Wella Invigo Shampoo | Wella Professionals | Pro shampoo | 250 ml, 1 L | bottle | 5% | 33051090 |
| Wella Invigo Nutri-Enrich Mask | Wella Professionals | Pro hair mask | 150 ml ?, 500 ml | box | 18% | 33059090 |
| Streax Professional Canvoline Straightening Cream | Streax Professional | Rebonding cream | kit ? | box | 18% | 33052000 ? |
| Streax Professional Canvoline Shampoo | Streax Professional | Pro shampoo | 300 ml, 1.5 L ? | bottle | 5% | 33051090 |
| Streax Professional Hair Spa Cream | Streax Professional | Hair spa cream | 200 g ?, 500 g ? | box | 18% | 33059090 |
| Streax Professional Vitariche Gloss Serum | Streax Professional | Pro serum | 45 ml ?, 115 ml ? | bottle | 18% | 33059090 |
| Godrej Professional Hair Spa Cream ? | Godrej Professional | Hair spa cream | 500 g ? | box | 18% | 33059090 |
| Keratin Smoothening Treatment (generic salon) | Various | Keratin kit | 100 ml, 500 ml, 1 L | bottle | 18% | 33059090 |
| Hair Botox / Nanoplastia Cream | Various | Hair treatment | 500 g, 1 kg | box | 18% | 33059090 |
| Rebonding Neutraliser (generic) | Various | Rebonding cream | 400 ml, 1 L | bottle | 18% | 33052000 ? |
| Perming Lotion & Neutraliser Kit | Various | Perm kit | 1 kit | box | 18% | 33052000 |
| Olaplex No.3 Hair Perfector | Olaplex | Bond treatment | 100 ml, 250 ml | bottle | 18% | 33059090 |
| K18 Leave-in Repair Mask | K18 | Bond treatment | 50 ml | bottle | 18% | 33059090 |

### Hair fall and dandruff treatments
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Mintop Minoxidil 5% Solution | Dr. Reddy's | Hair regrowth (drug) | 60 ml | bottle | 5% ? | 3004 ? |
| Tugain 5% Solution | Cipla | Hair regrowth (drug) | 60 ml | bottle | 5% ? | 3004 ? |
| Himalaya Anti-Dandruff Hair Cream | Himalaya | Hair cream | 50 ml, 100 ml | bottle | 18% | 33059030 ? |
| Mediker Anti-Lice Shampoo | Mediker | Anti-lice | 50 ml, 100 ml, sachet | bottle | 5% ? | 3004 / 3305 ? |
| Scalp Scrub (Mamaearth / Plum) | Various | Scalp treatment | 100 ml, 200 ml | bottle | 18% | 33059090 |
| Rosemary Water Hair Spray | Various | Hair tonic | 100 ml, 200 ml | bottle | 18% | 33059090 |
| Biotin Hair Gummies | Various | Nutraceutical | 30, 60 gummies | bottle | 18% ? | 2106 ? |
