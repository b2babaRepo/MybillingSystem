# Salon consumables, bridal items and beauty tools: beauty shop master list seed

Scope note: Only two web searches were run, both about GST. Product names, brands and pack sizes come from the researcher's own knowledge of Indian salon-supply catalogues and have not been checked against live listings. Treat packs as typical sizes, not an exact catalogue. Where the GST rate after 22 Sept 2025 (GST 2.0) is not confirmed, it is marked "?". Check rates against the CBIC Notification 9/2025-CT(Rate) schedule before going live.

## GST and HSN after 22 Sept 2025

### Takeaway
GST 2.0 moved hair oil, shampoo, soap, toothpaste, talcum powder and heading 9615 (combs, hair pins, hair slides, clips) to 5%. Make-up and skin-care preparations (3304), wax/depilatories (3404/3307) and electric hair tools (8516) appear to stay at 18%. Bindi, kumkum, sindoor and alta were already GST-exempt before the reform, and that exemption was not re-confirmed this round.

### Cited Findings
- Hair oil, soap and toothpaste (also toothbrush, shaving cream) cut from 18% to 5% from 22 Sept 2025 — [Upstox](https://upstox.com/news/market-news/stocks/gst-2-0-gst-cuts-elude-these-key-items-analysts-and-fmcg-players-say-it-is-surprising/article-180835); [Dhanam](https://english.dhanamonline.com/news/new-gst-rates-what-gets-cheaper-and-costlier-from-september-22-9001836)
- Heading 9615 (combs, hair-slides, hairpins, curling pins) moved from 12% to 5% — [MS Associates](https://www.msassociates.pro/gst/gst-personal-care-and-hygiene.php); combs at 5% also in [Tribune](https://www.tribuneindia.com/news/business/gst-overhaul-whats-cheaper-whats-costlier) and [Tax Garden](https://taxgarden.in/blog/income-tax-gst-for-cosmetics-beauty-products-shop-owners-india-ay-2026-27). One page ([IncorpX](https://www.incorpx.io/hsn-codes/heading-9615)) still shows 12%, which looks out of date.
- Talcum powder reported cut to 5% — [Corpzo](https://www.corpzo.com/new-gst-reforms-20) (secondary source)
- Mouthwash left unchanged — [Upstox](https://upstox.com/news/personal-finance/tax/salon-gym-yoga-centre-services-to-get-cheaper-after-gst-slashed-to-5/article-180712)
- Salon services moved to 5% (no ITC). This is the service rate, not the goods rate — [Upstox](https://upstox.com/news/personal-finance/tax/salon-gym-yoga-centre-services-to-get-cheaper-after-gst-slashed-to-5/article-180712)
- Hair dryer 8516 31 00: sources conflict (18% on most HSN pages, while one aggregator shows 5%/18%) — [CitizenNest](https://www.citizennest.com/hsn/8516); [Credlix](https://www.credlix.com/hsn-code/search/hair-dryer)

### Inferences
- 3304 (creams, face packs, bleach, facial kits, makeup, nail polish, face powder) stays at 18%. GST 2.0 cuts named only talc, hair oil, shampoo, soap and toothpaste.
- Wax: 3404 (artificial waxes) and 3307 90 (depilatories) stay at 18%. Paraffin wax 2712 is likely 18%?
- Mehendi powder 1404 90 40 is 5%. Ready henna cones are a paste, classed under 3305 90 or 3304?, and are commonly billed at 5%?. Treat as uncertain.
- Bindi, kumkum, sindoor, alta were Nil under the 2017 exemption list (from the researcher's own knowledge, not re-verified). Shown as 0 in the tables.
- Imitation jewellery 7117 is 3%. Glass bangles 7018 are Nil. Hair extensions/wigs 6704 are 18%?. Absorbent cotton 3005 and 5601 are 5%?. Nail cutters/manicure sets 8214 are 5%? (reportedly cut from 12%, not verified). Tweezers 8203 are 5%?. Mirrors 7009 are 18%. Hair brushes 9603 29 are 5%? (toothbrush 9603 21 is confirmed 5%; other brushes not confirmed).

### Gaps
- No row-level check of pack sizes against IndiaMART/Nykaa listings (too few tool calls).
- The rates for 8214, 8203, 9603 29, 4015 gloves, 6704 and henna cones have not been checked against the official notification.

## Seed rows

### Waxing
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Rica Liposoluble Wax (White Chocolate) | Rica | Wax | 800 | ml | 18 | 3404 |
| Rica Liposoluble Wax (Brazilian / Argan Oil) | Rica | Wax | 800 | ml | 18 | 3404 |
| Rica Roll-on Wax Cartridge | Rica | Wax | 100 | ml | 18 | 3404 |
| Rica Stripless Hard Wax Beads | Rica | Wax | 400, 1000 | g | 18 | 3404 |
| Rica Pre-wax Oil | Rica | Wax care | 250 | ml | 18 | 3304 |
| Rica Post-wax Lotion | Rica | Wax care | 250 | ml | 18 | 3304 |
| Bombini Liposoluble Wax | Bombini | Wax | 800 | g | 18 | 3404 |
| Bombini Chocolate Wax | Bombini | Wax | 800 | g | 18 | 3404 |
| Waxness Liposoluble Wax | Waxness | Wax | 800 | ml | 18 | 3404 |
| Waxness Roll-on Cartridge Wax | Waxness | Wax | 100 | ml | 18 | 3404 |
| Waxness Hard Wax Beans | Waxness | Wax | 1000 | g | 18 | 3404 |
| Sara Liposoluble Wax | Sara | Wax | 800 | g | 18 | 3404 |
| Sara Honey Wax | Sara | Wax | 600, 1200 | g | 18 | 3404 |
| Avon Chocolate Wax? | Avon? | Wax | 600 | g | 18 | 3404 |
| Honey Wax (loose) | Generic | Wax | 500, 1000 | g | 18 | 3404 |
| Chocolate Wax | Generic | Wax | 500, 1000 | g | 18 | 3404 |
| Hot Wax Jar | Generic | Wax | 500, 800 | g | 18 | 3404 |
| Veet Ready Wax Strips | Veet | Wax | 8, 20 | piece | 18 | 3307 |
| Veet Hair Removal Cream | Veet | Hair removal | 25, 50, 100 | g | 18 | 3307 |
| Anne French Hair Removal Cream | Anne French | Hair removal | 25, 40, 50 | g | 18 | 3307 |
| Wax Strip Roll (non-woven) | Generic | Wax accessory | 1 | roll | 18? | 5603 |
| Wax Strips Cut | Generic | Wax accessory | 100 | packet | 18? | 5603 |
| Wax Heater Single | Generic | Wax equipment | 1 | piece | 18 | 8516 |
| Wax Heater Double | Generic | Wax equipment | 1 | piece | 18 | 8516 |
| Roll-on Wax Heater | Generic | Wax equipment | 1 | piece | 18 | 8516 |
| Wooden Wax Spatula | Generic | Wax accessory | 50, 100 | packet | 5? | 4421 |
| Metal Wax Knife | Generic | Wax accessory | 1 | piece | 18? | 8215 |
| Talc for Waxing | Generic | Wax care | 100, 400 | g | 5 | 3304 |
| Aloe Vera Gel | Generic | Wax care | 100, 500 | g | 18 | 3304 |

### Threading and facial
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Threading Thread Roll | Generic | Threading | 1, 6, 12 | roll | 5? | 5204 |
| Threading Thread Box | Generic | Threading | 1 | box | 5? | 5204 |
| O3+ Bridal Facial Kit | O3+ | Facial kit | 1 | box | 18 | 3304 |
| O3+ Whitening Facial Kit | O3+ | Facial kit | 1 | box | 18 | 3304 |
| O3+ Agelock Facial Kit | O3+ | Facial kit | 1 | box | 18 | 3304 |
| O3+ D-Tan Pack | O3+ | De-tan | 300 | g | 18 | 3304 |
| VLCC Gold Facial Kit | VLCC | Facial kit | 50, 300 | g | 18 | 3304 |
| VLCC Diamond Facial Kit | VLCC | Facial kit | 50, 300 | g | 18 | 3304 |
| VLCC Insta Glow Facial Kit | VLCC | Facial kit | 60, 300 | g | 18 | 3304 |
| Lotus Professional Phyto-Rx Facial Kit | Lotus Professional | Facial kit | 1 | box | 18 | 3304 |
| Lotus Professional Radiant Gold Facial Kit | Lotus Professional | Facial kit | 1 | box | 18 | 3304 |
| Aroma Magic Diamond Facial Kit | Aroma Magic | Facial kit | 1 | box | 18 | 3304 |
| Aroma Magic Fruit Facial Kit | Aroma Magic | Facial kit | 1 | box | 18 | 3304 |
| Richfeel Gold Facial Kit | Richfeel | Facial kit | 1 | box | 18 | 3304 |
| Nature's Essence Gold Facial Kit | Nature's Essence | Facial kit | 50, 300 | g | 18 | 3304 |
| Nature's Essence Diamond Facial Kit | Nature's Essence | Facial kit | 50, 300 | g | 18 | 3304 |
| Shahnaz Husain Gold Facial Kit | Shahnaz Husain | Facial kit | 40 | g | 18 | 3304 |
| Jovees Facial Kit | Jovees | Facial kit | 1 | box | 18 | 3304 |
| Fruit Facial Kit | Generic | Facial kit | 1 | box | 18 | 3304 |
| Clean-up Kit | Generic | Clean-up | 1 | box | 18 | 3304 |
| VLCC Clean-up Kit | VLCC | Clean-up | 1 | box | 18 | 3304 |
| Raaga Professional De-Tan Pack | Raaga | De-tan | 500, 1000 | g | 18 | 3304 |
| Raaga Professional De-Tan Sachet Kit | Raaga | De-tan | 6, 12 | packet | 18 | 3304 |
| Massage Cream | Generic | Facial | 500, 1000 | g | 18 | 3304 |
| VLCC Massage Cream | VLCC | Facial | 500 | g | 18 | 3304 |
| Lotus Massage Cream | Lotus | Facial | 250, 500 | g | 18 | 3304 |
| Cleansing Milk | Generic | Facial | 100, 500 | ml | 18 | 3304 |
| Lotus Cleansing Milk | Lotus | Facial | 80, 500 | ml | 18 | 3304 |
| Face Scrub | Generic | Facial | 100, 500 | g | 18 | 3304 |
| Face Pack | Generic | Facial | 100, 500 | g | 18 | 3304 |
| Peel-off Mask (charcoal / gold) | Generic | Facial | 100, 500 | g | 18 | 3304 |
| Galvanic Gel | Generic | Facial | 250, 500 | g | 18 | 3304 |
| Ultrasonic Gel | Generic | Facial | 250, 500 | g | 18 | 3304 |
| Rose Water | Generic | Facial | 100, 500 | ml | 18 | 3304 |
| Multani Mitti | Generic | Facial | 100, 500 | g | 5? | 2508 |
| Toner | Generic | Facial | 100, 500 | ml | 18 | 3304 |
| Vitamin C Serum | Generic | Facial | 30, 50 | ml | 18 | 3304 |
| Oxy Life Professional Bleach | Oxy Life | Bleach | 50, 250 | g | 18 | 3304 |
| Oxy Life Bleach Salon Pack | Oxy Life | Bleach | 1 | box | 18 | 3304 |
| Fem Fairness Bleach | Fem | Bleach | 8, 24, 64 | g | 18 | 3304 |
| Jolen Creme Bleach | Jolen | Bleach | 30, 56 | g | 18 | 3304 |
| VLCC Oxy Bleach | VLCC | Bleach | 30, 300 | g | 18 | 3304 |
| Blackhead Remover Strips | Generic | Facial | 3, 6 | piece | 18 | 3304 |
| Sheet Mask | Generic | Facial | 1 | piece | 18 | 3304 |

### Manicure and pedicure
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Pedicure Kit | Generic | Mani-pedi | 1 | box | 18 | 3304 |
| VLCC Pedicure Kit | VLCC | Mani-pedi | 1 | box | 18 | 3304 |
| Manicure Kit | Generic | Mani-pedi | 1 | box | 18 | 3304 |
| Foot Soak Salt | Generic | Mani-pedi | 200, 500, 1000 | g | 18 | 3307 |
| Foot Scrub | Generic | Mani-pedi | 200, 500 | g | 18 | 3304 |
| Foot Crack Cream | Generic | Mani-pedi | 50, 100 | g | 18 | 3304 |
| Pumice Stone | Generic | Mani-pedi | 1 | piece | 5? | 2513 |
| Foot File / Scraper | Generic | Mani-pedi | 1 | piece | 5? | 8214 |
| Nail Cutter Small | Generic | Nail tools | 1 | piece | 5? | 8214 |
| Nail Cutter Large | Generic | Nail tools | 1 | piece | 5? | 8214 |
| Trim Nail Cutter | Trim | Nail tools | 1 | piece | 5? | 8214 |
| Manicure Set (pouch) | Generic | Nail tools | 1 | piece | 5? | 8214 |
| Nail File (emery board) | Generic | Nail tools | 1, 10 | piece | 18? | 6805 |
| Nail Buffer Block | Generic | Nail tools | 1 | piece | 18? | 6805 |
| Cuticle Pusher | Generic | Nail tools | 1 | piece | 5? | 8214 |
| Cuticle Nipper | Generic | Nail tools | 1 | piece | 5? | 8214 |
| Cuticle Oil | Generic | Nail care | 15 | ml | 18 | 3304 |
| Cuticle Cream | Generic | Nail care | 15, 50 | g | 18 | 3304 |
| Nail Polish Remover | Generic | Nail care | 30, 100 | ml | 18 | 3304 |
| Nail Polish | Lakme / Elle18 / generic | Nail care | 5, 9 | ml | 18 | 3304 |
| Nail Art Stickers | Generic | Nail care | 1 | piece | 18? | 4911 |
| Artificial Nails Set | Generic | Nail care | 24 | packet | 18 | 3926 |
| Nail Glue | Generic | Nail care | 2, 3 | g | 18 | 3506 |
| Paraffin Wax Block | Generic | Mani-pedi | 450, 1000 | g | 18? | 2712 |
| Paraffin Wax Heater | Generic | Mani-pedi | 1 | piece | 18 | 8516 |
| Pedicure Tub | Generic | Mani-pedi | 1 | piece | 18 | 3924 |
| Toe Separator | Generic | Mani-pedi | 2, 10 | packet | 18 | 3926 |

### Mehendi, bindi, sindoor, bridal
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Neha Mehendi Cone | Neha | Mehendi | 1, 12 | piece | 5? | 3305 |
| Neha Herbal Mehendi Powder | Neha | Mehendi | 50, 140, 500 | g | 5 | 1404 |
| Kaveri Mehendi Cone | Kaveri | Mehendi | 1, 12 | piece | 5? | 3305 |
| Kaveri Mehendi Powder | Kaveri | Mehendi | 100, 250 | g | 5 | 1404 |
| Sonam Mehendi Cone | Sonam | Mehendi | 1, 12 | piece | 5? | 3305 |
| Ajanta Mehendi Cone? | Ajanta | Mehendi | 1, 12 | piece | 5? | 3305 |
| Ajanta Mehendi Powder | Ajanta | Mehendi | 100, 500 | g | 5 | 1404 |
| Nail Mehendi Cone | Generic | Mehendi | 1, 12 | piece | 5? | 3305 |
| Glitter / Red Cone | Generic | Mehendi | 1, 12 | piece | 18? | 3304 |
| Mehendi Oil (Nilgiri) | Generic | Mehendi | 10, 30 | ml | 18? | 3301 |
| Bindi Packet (velvet) | Generic | Bindi | 1, 12 | packet | 0 | 3304 |
| Shilpa Bindi | Shilpa | Bindi | 1, 12 | packet | 0 | 3304 |
| Bridal Stone Bindi | Generic | Bindi | 1 | packet | 0 | 3304 |
| Sindoor Powder | Generic | Sindoor | 10, 25 | g | 0 | 3304 |
| Liquid Sindoor | Generic | Sindoor | 5, 10 | ml | 0 | 3304 |
| Kumkum | Generic | Sindoor | 10, 25, 50 | g | 0 | 3304 |
| Alta (liquid) | Generic | Bridal | 30, 60 | ml | 0 | 3304 |
| Kajal | Lakme / Maybelline / generic | Makeup | 0.35, 3 | g | 18 | 3304 |
| Hair Extension (clip-in) | Generic | Bridal hair | 1 | piece | 18? | 6704 |
| Paranda / Choti | Generic | Bridal hair | 1 | piece | 18? | 6704 |
| Jooda Maker (bun donut) | Generic | Bridal hair | 1 | piece | 18? | 3926 |
| Artificial Jooda | Generic | Bridal hair | 1 | piece | 18? | 6704 |
| Artificial Gajra | Generic | Bridal hair | 1 | piece | 18? | 6702 |
| Artificial Hair Flowers | Generic | Bridal hair | 1, 12 | piece | 18? | 6702 |
| Fresh Gajra | Generic | Bridal hair | 1 | piece | 0 | 0603 |
| Hair Pins (U pins) | Generic | Hair accessory | 1 | box | 5 | 9615 |
| Bobby Pins | Generic | Hair accessory | 1, 50 | packet | 5 | 9615 |
| Fancy Stone Hair Pin | Generic | Hair accessory | 1, 12 | piece | 5 | 9615 |
| Maang Tikka | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| Nose Pin (artificial) | Generic | Artificial jewellery | 1, 12 | piece | 3 | 7117 |
| Press Nose Ring | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| Artificial Earrings | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| Artificial Necklace Set | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| Glass Bangles | Generic | Bangles | 12, 24 | box | 0 | 7018 |
| Metal / Lac Bangles | Generic | Bangles | 12 | box | 3 | 7117 |
| Artificial Payal | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| Artificial Bichhiya | Generic | Artificial jewellery | 1 | piece | 3 | 7117 |
| False Eyelashes | Generic | Bridal makeup | 1 | piece | 18? | 6704 |
| Eyelash Glue | Generic | Bridal makeup | 5 | g | 18 | 3506 |
| Body Glitter | Generic | Bridal makeup | 10 | g | 18 | 3304 |

### Tools and accessories
| Name | Brand | Category | Packs | Unit | GST | HSN |
|---|---|---|---|---|---|---|
| Pocket Comb | Generic | Comb | 1, 12 | piece | 5 | 9615 |
| Wide Tooth Comb | Generic | Comb | 1 | piece | 5 | 9615 |
| Tail Comb | Generic | Comb | 1 | piece | 5 | 9615 |
| Cutting Comb | Generic | Comb | 1 | piece | 5 | 9615 |
| Lice Comb | Generic | Comb | 1 | piece | 5 | 9615 |
| Wooden Comb | Generic | Comb | 1 | piece | 5 | 9615 |
| Paddle Brush | Generic | Hair brush | 1 | piece | 5? | 9603 |
| Round Brush | Generic | Hair brush | 1 | piece | 5? | 9603 |
| Vega Hair Brush | Vega | Hair brush | 1 | piece | 5? | 9603 |
| Claw Hair Clip | Generic | Hair accessory | 1, 12 | piece | 5 | 9615 |
| Tic-Tac Clip | Generic | Hair accessory | 2, 12 | packet | 5 | 9615 |
| Section Clips | Generic | Hair accessory | 6, 12 | packet | 5 | 9615 |
| Hair Band | Generic | Hair accessory | 1 | piece | 5? | 9615 |
| Hair Rubber Band | Generic | Hair accessory | 1 | packet | 5? | 9615 |
| Scrunchie | Generic | Hair accessory | 1, 6 | piece | 5? | 6117 |
| Philips Hair Dryer | Philips | Electric tool | 1 | piece | 18 | 8516 |
| Vega Hair Dryer | Vega | Electric tool | 1 | piece | 18 | 8516 |
| Havells Hair Dryer | Havells | Electric tool | 1 | piece | 18 | 8516 |
| Ikonic Professional Hair Dryer | Ikonic | Electric tool | 1 | piece | 18 | 8516 |
| Philips Hair Straightener | Philips | Electric tool | 1 | piece | 18 | 8516 |
| Vega Hair Straightener | Vega | Electric tool | 1 | piece | 18 | 8516 |
| Havells Hair Straightener | Havells | Electric tool | 1 | piece | 18 | 8516 |
| Ikonic Hair Straightener | Ikonic | Electric tool | 1 | piece | 18 | 8516 |
| Hair Curler / Curling Tong | Vega / Ikonic | Electric tool | 1 | piece | 18 | 8516 |
| Hair Crimper | Vega / Ikonic | Electric tool | 1 | piece | 18 | 8516 |
| Hair Trimmer | Philips / Havells | Electric tool | 1 | piece | 18 | 8510 |
| Facial Steamer | Generic | Equipment | 1 | piece | 18 | 8516 |
| Hand Mirror | Generic | Mirror | 1 | piece | 18 | 7009 |
| Table Mirror | Generic | Mirror | 1 | piece | 18 | 7009 |
| Tweezer | Generic | Tools | 1 | piece | 5? | 8203 |
| Eyelash Curler | Generic | Tools | 1 | piece | 18? | 8214 |
| Eyebrow Razor | Generic | Tools | 1, 3 | piece | 18 | 8212 |
| Makeup Brush Set | Generic | Tools | 1 | packet | 18? | 9603 |
| Makeup Sponge / Blender | Generic | Tools | 1 | piece | 18 | 3926 |
| Makeup Pouch | Generic | Accessory | 1 | piece | 18? | 4202 |
| Disposable Towel Roll | Generic | Disposable | 1 | roll | 18? | 4818 |
| Disposable Face Towel | Generic | Disposable | 50, 100 | packet | 18? | 5603 |
| Disposable Gloves | Generic | Disposable | 100 | box | 5? | 4015 |
| Cotton Roll | Generic | Disposable | 50, 100, 500 | g | 5? | 3005 |
| Cotton Pads | Generic | Disposable | 50, 100 | packet | 5? | 5601 |
| Cotton Ear Buds | Generic | Disposable | 100, 200 | packet | 5? | 5601 |
| Facial Bowl | Generic | Tools | 1 | piece | 18 | 3924 |
| Face Pack Brush | Generic | Tools | 1 | piece | 5? | 9603 |
| Tint Bowl and Brush Set | Generic | Tools | 1 | piece | 18 | 3924 |
| Spray Bottle | Generic | Tools | 1 | piece | 18 | 3923 |
| Cutting Cape | Generic | Accessory | 1 | piece | 5? | 6211 |
| Salon Apron | Generic | Accessory | 1 | piece | 5? | 6211 |
| Hair Cutting Scissor | Generic | Tools | 1 | piece | 18? | 8213 |
| Shower Cap | Generic | Disposable | 1, 100 | packet | 18 | 3926 |
| Neck Paper Roll | Generic | Disposable | 1 | roll | 18? | 4818 |
