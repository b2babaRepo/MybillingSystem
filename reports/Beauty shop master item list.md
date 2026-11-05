# Seed about 800 beauty rows, mostly at 18%

Grocery Khata should seed a `beauty-master.csv` of roughly **800 rows in 11 groups**, built from the five researcher lists. Those lists hold about 1,000 raw rows, and roughly 200 of them repeat across files: facial kits, bleach, Veet, talc, lip balms, micellar water, nail polish, combs and brushes. The tax picture after GST 2.0 (in force from **22 Sept 2025**) is simpler than it looks. A short, named list of everyday items moved to **5%**: shampoo, hair oil, mehendi cones, toilet soap bars, talcum and face powder, shaving cream and after-shave lotion, and combs, hair pins and clips under heading 9615. Almost everything else a beauty shop sells **stayed at 18%**. That includes creams, serums, sunscreens, face wash, conditioner, hair colour, makeup, nail polish, perfume, deodorant, body wash, wax and electric tools. A small group is nil or near-nil: bindi, sindoor, kumkum, alta, glass bangles, fresh gajra and sanitary pads, plus imitation jewellery at 3%. About a fifth of the rows carry a doubtful rate. The four biggest doubts are pressed compacts, shaving foam and gel, medicated products filed under chapter 30, and the many non-cosmetic tools. None of the pack sizes were checked against live listings. The `MasterItem.GstRate` field is nullable and documented as "null when not checked", so seed only the confirmed rates and leave the doubtful ones null until a CA signs off. Before seeding, the app also needs two schema decisions: shade variants, and units such as "bottle" and "roll" that the `Unit` enum lacks.

## GST 2.0 moved a short named list to 5%, and nothing else

The rate cuts were item-specific, not chapter-wide, and that fact settles most of the rows. Several sources agree that **shampoo, hair oil, toilet soap, talcum powder and face powder fell from 18% to 5%** ([Upstox](https://upstox.com/news/personal-finance/tax/new-gst-rates-full-list-of-items-with-5-goods-and-services-tax/article-180697); [YourStory](https://yourstory.com/2025/09/gst-council-overhaul-cuts-tax-everyday-essentials)). **Shaving cream and after-shave lotion** fell to 5% as well ([Business Standard](https://www.business-standard.com/economy/news/new-gst-rate-cut-what-gets-cheaper-expensive-5-18-percent-125092200215_1.html); [The Week](https://www.theweek.in/news/biz-tech/2025/09/04/gst-2-0-toothpaste-shampoos-shaving-creams-to-become-cheaper-but-not-mouthwash-like-listerine-and-colgate-plax-why.html)). A tax-practice table splits HSN 3304 cleanly: talc and face powder are 18% before 22.09.2025 and 5% from that date ([MS Associates](https://www.msassociates.pro/gst/gst-personal-care-and-hygiene.php)). The same table moves **heading 9615 (combs, hair slides, hair pins) from 12% to 5%**, and two more sources confirm the comb rate ([Tribune](https://www.tribuneindia.com/news/business/gst-overhaul-whats-cheaper-whats-costlier); [Tax Garden](https://taxgarden.in/blog/income-tax-gst-for-cosmetics-beauty-products-shop-owners-india-ay-2026-27)).

Heading 3305 shows how narrow the cut is. Under Notification 9/2025-CT(Rate), only **hair oil, shampoo and mehendi paste in cones** are 5%. Conditioner, serum, hair colour and gel are named as 18% ([Tax Garden](https://taxgarden.in/blog/income-tax-gst-for-cosmetics-beauty-products-shop-owners-india-ay-2026-27)). Analysts noted that hair dye, skin care and cosmetics got no cut ([Upstox](https://upstox.com/news/market-news/stocks/gst-2-0-gst-cuts-elude-these-key-items-analysts-and-fmcg-players-say-it-is-surprising/article-180835)). A law-firm review confirms that face cream, sunscreen, moisturising lotion, serum and lip balm are unchanged at 18% ([Khaitan & Co](https://khaitanco.com/thought-leadership/GST-Rate-Changes-and-impact-under-Regulatory-laws)). Liquid soap stays at the higher rate while soap bars drop ([ClearTax](https://cleartax.in/s/new-vs-old-gst-rates-comparison)), so **body wash, hand wash and liquid face wash are 18%**. Some HSN lookup sites still show hair oil and shampoo at 18%, and one still uses the 2017 rate of 28% ([IncorpX](https://www.incorpx.io/hsn-codes/code-33049120); [Busy](https://busy.in/hsn/oil-hsn-code)). Those sites are out of date. Do not use them as a seed source.

One trap: the salon service rate is not the product rate. Beauty services moved to **5% without input tax credit** ([Upstox](https://upstox.com/news/personal-finance/tax/salon-gym-yoga-centre-services-to-get-cheaper-after-gst-slashed-to-5/article-180712)), but a jar of hair spa cream sold over the counter stays at 18%. The table below reconciles the five notes. Where two notes disagreed, the row shows the reading that the cited sources support.

| Rate | What belongs here | HSN | Confidence |
|---|---|---|---|
| **5%** | Shampoo (retail, salon and sachet strips) | 33051090 | Confirmed |
| **5%** | Hair oil, including amla, almond, cool and ayurvedic oils | 33059011 / 33059019 | Confirmed |
| **5%** | Mehendi cones | 3305 | Confirmed by one source; the salon note marked it 5? |
| **5%** | Toilet soap bars and multipacks | 34011190 | Confirmed |
| **5%** | Talcum and prickly-heat powder | 33049120 (the fragrance note used 33049110, which is the face-powder code) | Confirmed |
| **5%** | Loose face powder | 33049110 | Confirmed |
| **5%** | Shaving cream, after-shave lotion | 33071010 / 33071090 | Confirmed |
| **5%** | Combs, hair pins, bobby pins, claw and tic-tac clips | 9615 | Confirmed |
| 5%? | Pressed compacts, shaving foam and gel, after-shave balm, henna powder (1404), cotton wool and buds, nail cutters (8214), tweezers (8203), hair brushes (9603 29), threading thread | various | Unconfirmed |
| 5%? | Medicated products: Nizoral, minoxidil, anti-lice, and possibly Boroline, Boroplus and Vicco if their makers file them as ayurvedic medicine | 3004 | Unconfirmed; check the maker's invoice |
| **18%** | All skin care: creams, serums, sunscreens, toners, face packs, facial kits, bleach, sheet masks, lip balm, body lotion, hand and foot cream | 3304 99 / 3304 10 | Confirmed |
| **18%** | Face wash, body wash, hand wash, intimate wash | 3401 30 / 3304 99 | Confirmed (the 5% cut covers soap bars only) |
| **18%** | Conditioner, hair serum, mask, spa cream, gel, wax, spray, hair colour, developer, bleach, keratin, rebonding and perm products | 3305 20 / 30 / 90 | Confirmed |
| **18%** | All makeup except face powder: lip, eye, foundation, primer, blush; also nail polish and remover | 3304 10 / 20 / 30 / 99 | Confirmed |
| **18%** | Perfume, attar, body mist, deodorant, roll-on | 3303 / 3307 20 | No source lists them among the cuts |
| **18%** | Hair removal cream, salon wax, petroleum jelly, hand sanitizer | 3307 90 / 3404 / 2712 / 3808 94 | Not named in any cut list |
| 18%? | Razors, blades and cartridges (8212), trimmers (8510), hair dryers and straighteners (8516), makeup brushes and sponges, false lashes, glue, artificial nails, wax strips, loofahs, mirrors | various | Unconfirmed |
| **3%** | Imitation jewellery: maang tikka, nose pins, artificial earrings, payal, bichhiya, metal or lac bangles | 7117 | From the researcher's knowledge |
| **0%** | Bindi, sindoor, kumkum, alta, glass bangles, fresh gajra | 3304 / 7018 / 0603 | 2017 exemption; not re-verified for GST 2.0 |
| 0%? | Sanitary pads and panty liners | 96190010 | Exempt since 2018 per the fragrance note; sources inconsistent ([India TV](https://www.indiatvnews.com/business/news-new-gst-rate-cuts-announced-here-are-the-list-of-items-that-might-get-cheaper-gst-council-meeting-new-delhi-piyush-goyal-announcements-key-rate-cuts-453950)) |

The notes disagree in four places that matter. First, **face powder**: the makeup and skin notes cite sources putting 3304 91 at 5%, but the salon note grouped face powder with 18% makeup. The sources favour 5%. Second, **pressed compacts** (Lakme, Maybelline Fit Me, Sugar and others) are probably "face powder" under 33049110 at 5%, but brands may still bill them at 18% under 3304 99. Third, **hair removal cream** appears as 3304 99 in one note and 3307 in another. The rate is 18% either way, but the HSN should be the 3307 90 depilatory code. Fourth, **beard oil** splits three ways across rows: 5% as hair oil, 18% under 3305 90, or 18% under 3304. Keep it null until a maker's invoice is checked.

## Eleven groups cover the shelf, with salon backbar kept separate

The prior salon plan found that salons hold retail stock and backbar stock separately, and that today's `Beauty` starter list has only nine items, two of them with no GST rate. The master list should mirror that split. Keeping salon-professional SKUs (1 L developers, 800 ml wax tins, 500 g spa cream) in their own groups also stops a cosmetics counter from scrolling past them. The table proposes groups and row counts after removing duplicates.

| Group | Main categories | Example brands | Rows | Dominant rate |
|---|---|---|---|---|
| Skin care | Face wash, cleanser, moisturiser, serum, sunscreen, toner, night cream, under-eye, sheet mask, petroleum jelly | Himalaya, Pond's, Garnier, Nivea, Lakme, Mamaearth, Minimalist, Derma Co, Cetaphil, Biotique, Lotus, Glow & Lovely, Vaseline | 170 | 18 |
| Facial, bleach and clean-up | Facial kits, bleach, de-tan, massage cream, galvanic gel, peel-off | VLCC, O3+, Oxy Life, Fem, Jolen, Aroma Magic, Nature's Essence, Shahnaz Husain, Raaga | 55 | 18 |
| Hair care retail | Shampoo and sachets, conditioner, serum, mask, hair oil, styling gel, wax and cream | Clinic Plus, Sunsilk, Dove, Pantene, H&S, TRESemmé, L'Oréal Paris, Parachute, Dabur, Bajaj, Navratna, Set Wet, Gatsby | 125 | Mixed 5 and 18 |
| Hair colour and henna | Crème and shampoo colour, dye powder, kali mehndi, henna, indigo, cones | Godrej Expert, Garnier, Streax, Indica, Bigen, Vasmol, Nisha, Neha, Kaveri | 35 | 18; henna and cones 5 |
| Salon professional hair | Pro colour tubes, developers, bleach powder, pro shampoo and mask, keratin, rebonding, perm, bond treatments | L'Oréal Professionnel, Schwarzkopf, Matrix, Wella, Biolage, Streax Professional, Olaplex | 60 | 18; pro shampoo 5 |
| Makeup | Foundation, BB/CC, compact, loose powder, primer, concealer, blush, highlighter, kajal, liner, mascara, eyeshadow, brow, lipstick, gloss, liner | Lakme, Maybelline, L'Oréal Paris, Sugar, Swiss Beauty, Faces Canada, Insight, Colorbar, Blue Heaven, Elle 18, MAC | 140 | 18; powders 5 |
| Nails, mani-pedi | Polish, remover, gel polish, cuticle care, press-on nails, glue, pedicure and manicure kits, paraffin | Lakme, Maybelline, Elle 18, Colorbar, VLCC | 45 | 18 |
| Fragrance and talc | Deodorant, roll-on, perfume, attar, body mist, talc | Fogg, Wild Stone, Axe, Engage, Park Avenue, Nivea, Bella Vita, Skinn, Ajmal, Pond's, Navratna | 55 | 18; talc 5 |
| Bath, body and personal care | Soap bars, body wash, hand wash, body scrub, hair removal, wax strips, intimate wash, sanitary pads, cotton | Lux, Dove, Lifebuoy, Santoor, Medimix, Dettol, Veet, Anne French, Whisper, Stayfree | 65 | Soap 5; rest 18 |
| Men's grooming | Shaving cream and foam, razors and blades, aftershave, beard oil and wash, trimmers, men's face wash | Gillette, Old Spice, Park Avenue, Godrej, Beardo, Ustraa, Bombay Shaving Co, Philips | 45 | Mixed |
| Salon consumables, bridal and tools | Salon wax and heaters, threading, disposables, mehendi, bindi, sindoor, bridal hair, imitation jewellery, combs, clips, dryers, straighteners, mirrors | Rica, Bombini, Waxness, Sara, Vega, Ikonic, Havells, Shilpa | 105 | Mixed 0, 3, 5 and 18 |

That totals about **800 rows**. For comparison, the existing trade lists run from about 320 rows (hardware) to about 960 (kirana). Beauty sits in the upper half because of brand breadth, not depth. The skin and makeup counts already assume **shade is a variant, not a row**: one "Lakme Eyeconic Kajal" row, not twelve. If the app cannot model shade yet, keep one row per line and let shopkeepers add shades, rather than seeding thousands of shade rows. Keep sachets and strips as their own rows (Clinic Plus ₹1 sachet, Pond's cold cream 8 ml, Glow & Lovely 9 g), because they sell in a different unit and at a different velocity from the bottle.

## The CSV format needs units and rate handling decided first

The existing seed files (`backend/src/GroceryKhata.Api/Catalogue/Data/paint-master.csv` and its siblings) use the columns `name,name_hi,category,unit,gst,hsn,packs`. The researcher rows map onto these directly, with three exceptions. First, the notes use units such as **"bottle", "roll" and "set"**, which the `Unit` enum in `Catalogue/Product.cs` does not define. Map bottles to `Ml` or `Piece`, rolls to `Piece`, and gift sets and kits to `Box`. Use `G` for powders and creams sold by weight. Second, the convention in `MasterItem.GstRate` already handles doubt: it is null when not checked, and the shop sets it. **Seed the confirmed 5%, 18%, 3% and 0% rates. Leave every "?" row null.** That is about 160 to 180 rows, concentrated in tools, men's shaving and medicated items. A wrong default is costly here because tax is copied onto each bill line when the bill is saved, so an error stays on old bills permanently. Third, use one HSN length consistently. The notes mix 4-digit and 8-digit codes. Shops above ₹5 crore turnover need six digits on invoices, so seed 8-digit codes where a note gives one and a source supports it ([Credlix 330491](https://www.credlix.com/hsn-code/330491); [Credlix 330430](https://www.credlix.com/hsn-code/330430)). Use the 4-digit heading everywhere else.

Mixed kits need one rule. A shampoo-plus-conditioner combo, a party makeup kit or a bridal kit is a mixed supply and takes the **highest rate in the box**, which in practice is 18% unless every item is a 5% item. The hair and makeup notes agree on this. Seed combos at 18%.

## Check these before seeding

The biggest weakness is pack sizes. **All five notes say their packs come from memory and were not checked against live Nykaa, Amazon or distributor listings.** The makeup note leaves dozens of small-brand rows (Insight, Blue Heaven, Mars, Renee, Elle 18) with "?" for both name and size. Spot-check the top 150 sellers against a distributor price list. For the small brands, drop the uncertain rows rather than guess. Several names also carry "?" because the product line may be discontinued or renamed, for example Lakme Absolute Perfect Radiance BB Cream and Dabur Anmol Jasmine. Remove those unless a listing confirms them.

On tax, a CA should read Notification 9/2025-CT(Rate) Schedules I and II directly. None of the researchers fetched the official text. These questions remain open:

- Does liquid face wash under 3401 30 fall outside the soap-bar cut?
- Do shaving foam and gel count as "shaving cream"?
- Are pressed compacts "face powder"?
- Do 8214 nail cutters, 8203 tweezers and 9603 29 hair brushes now sit at 5%?
- Does the 2017 nil list for bindi, sindoor, kumkum and alta still stand?
- Are sanitary pads still exempt?

Hair dryers under 8516 also show conflicting rates across lookup sites ([CitizenNest](https://www.citizennest.com/hsn/8516); [Credlix](https://www.credlix.com/hsn-code/search/hair-dryer)). For the ayurvedic creams (Boroline, Boroplus, Vicco) and the drug-licensed hair products (minoxidil, Nizoral), the GST rate printed on the supplier's invoice is the practical tie-breaker. Some of those products may also belong in the medical master rather than beauty.

Finally, keep two rows out of beauty, or flag them for cross-listing. **Hand sanitizer, sanitary pads and cotton** already overlap with the kirana and medical lists. The `MasterItem` model expects a separate row in each trade, so do not create one shared row.

## Conclusion

The cost of the 22 Sept 2025 reform for a beauty shop is not the rate itself but a split inside each brand. The same Dove shelf now holds a 5% shampoo, an 18% conditioner, a 5% soap bar and an 18% body wash, often bought from one distributor on one invoice. That makes per-item seeded rates far more valuable for this trade than for paint or hardware, where a whole category shares one rate. It also makes leaving the doubtful rates null the right call: a shopkeeper who sets one rate on a row is better served than one who inherits a wrong default. The second lesson comes from the salon plan. Because salon services are 5% with no input credit while the products sold beside them are mostly 18%, a beauty shop is two tax businesses in one. Keeping salon-professional and backbar items in their own groups prepares the list for per-service consumption tracking when that feature arrives.
