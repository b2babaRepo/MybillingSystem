# Hardware shop core master list (India, 2026)

Note on method: Only 2 web searches were run. GST/HSN facts below are partly sourced. Item names, brands, units and price bands come from general trade knowledge and are NOT individually sourced. Treat every price as an indicative retail (MRP-ish) band and mark it "?" until checked against a local dealer list. Electrical goods are left out.

## GST rates after the September 2025 rate changes (hardware HSN chapters)

### Takeaway
GST 2.0 came into force on 22 Sep 2025 with two main slabs (5% / 18%) plus a 40% demerit slab. Plastic pipes and fittings (3917) are confirmed at 18%. Most base-metal hardware (7317, 7318, 8301, 8302, 8203–8205) appears to have stayed at 18%, but no official heading-level confirmation was found. Treat those as "18% ?".

### Cited Findings
- New rates in force from 22 Sep 2025; Council decision on 3 Sep 2025 — [Tally](https://tallysolutions.com/gst/new-gst-rates-september-2025-business-impact/); [Hans India](https://www.thehansindia.com/news/national/gst-tax-cuts-explained-from-september-22-2025-1003367)
- Two slabs (5% and 18%) plus 40% for sin/luxury goods; about 295 goods moved from 12% to 5%/nil — [Deccan Herald](https://www.deccanherald.com/business/economy/revised-gst-rates-full-list-of-items-that-will-get-cheaper-or-costlier-3711489)
- Farm tools are reported at 5% (a general statement, not by heading) — [Hans India](https://www.thehansindia.com/news/national/gst-tax-cuts-explained-from-september-22-2025-1003367)
- HSN 3917 PVC/UPVC/CPVC pipes and fittings stay at 18%. Rigid pipe is 3917 23 90 and fittings are 3917 40 00 — [Tally HSN 3917](https://tallysolutions.com/gst/hsn-code-3917-product-classification-gst-rate-business-filing-guide/); [GimBooks](https://www.gimbooks.com/blog/pvc-pipe-hsn-code-3917-gst-rates/amp/)
- HSN 8481 (taps, cocks, valves) is listed at 18% on a lookup site, so this is weakly sourced — [incorpx 8481](https://www.incorpx.io/hsn-codes/heading-8481)

### Inferences
- Working table for the app's seed data. Verify every line on the CBIC rate schedule before go-live:

| HSN | Goods | GST (seed) |
|---|---|---|
| 7317 | Nails, tacks, staples, drawing pins | 18% ? |
| 7318 | Screws, bolts, nuts, washers, rivets, anchors, hooks | 18% ? |
| 7326 | Other iron articles (wire hooks, S-hooks, chains 7315) | 18% ? |
| 8201 | Agricultural hand tools (spade/phawda, khurpi, axe, sickle) | 5% ? (was 12%) |
| 8202 | Hacksaw blades, hand saws, circular/cutting saw blades | 18% ? |
| 8203 | Pliers, pincers, files, tin snips | 18% ? |
| 8204 | Spanners, wrenches, sockets | 18% ? |
| 8205 | Hammers, screwdrivers, chisels, spirit level, trowels, vices | 18% ? |
| 8207 | Drill bits, router bits, interchangeable tool bits | 18% ? |
| 8467 / 8465 | Hand power tools (drill, grinder) | 18% ? |
| 6804 | Cutting/grinding wheels | 18% ? |
| 6805 | Sandpaper/emery | 18% ? |
| 9017 | Measuring tapes | 18% ? |
| 8301 | Padlocks, mortise locks, night latches, keys | 18% ? |
| 8302 | Hinges, tower bolts, handles, aldrops, channels, door closers, castors | 18% ? |
| 3506 | Glues/adhesives (Fevicol, Fevikwik, Araldite) | 18% ? |
| 3214 | Sealants, putty, M-Seal, silicone | 18% ? |
| 3917 | Plastic pipes, hoses, fittings | 18% (sourced) |
| 3919 | Teflon/PTFE tape, self-adhesive tape | 18% ? |
| 3922 | Plastic sanitaryware (cisterns, seat covers) | 18% ? |
| 6910 | Ceramic WC/wash basin | 18% ? |
| 7324 | Steel sinks, sanitary fittings of steel | 18% ? |
| 8481 | Taps, cocks, valves, ball valves | 18% (weak) |
| 4016 | Rubber washers | 18% ? |

### Gaps
- No CBIC notification text (e.g. 9/2025-CT(Rate)) was fetched, so the heading-level rates for 7317/7318/8205/8301/8302 are not confirmed. The 8201→5% move needs checking.

## Master item rows

Columns: Item (shopkeeper name) | variants | brands | unit | price band ₹ (indicative ?) | HSN | GST

### Takeaway
Below are about 210 seed rows across 9 categories. All prices, brands and units are unsourced trade knowledge and should be treated as "?".

### Cited Findings
- No item-level prices were sourced in this pass. See Gaps.

### Inferences (seed rows)

#### A. Fasteners
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Wood screw (CSK, Phillips) | 1/2", 3/4", 1", 1.25", 1.5", 2", 2.5", 3" × gauge 6/8/10 | Nettlefolds/GKW, Atul, local | box 100 / piece / kg | 40–250 per box | 7318 | 18% ? |
| Chipboard / Pozi screw | 16–75 mm | Hettich, Hafele, Ebco, local | box 100/200 | 60–300 | 7318 | 18% ? |
| Self-tapping screw | 1/2"–2", pan/CSK | local, Unbrako | box / kg | 50–250 | 7318 | 18% ? |
| Drywall screw (black) | 25, 32, 38, 50, 75 mm | local, Hilti | box / kg | 150–220 per kg | 7318 | 18% ? |
| Roofing / SDS self-drilling screw | 25–100 mm with EPDM washer | local | box 100 | 150–500 | 7318 | 18% ? |
| Machine screw with nut | M3–M6 × 10–50 mm | local | box / piece | 1–5 each | 7318 | 18% ? |
| Allen bolt (socket head) | M5–M16 | Unbrako, TVS | piece / box | 3–60 | 7318 | 18% ? |
| Hex nut-bolt (MS/GI) | M6, M8, M10, M12, M16 × 25–150 mm | local, TVS, Nettlefolds | piece / kg | 90–150 per kg | 7318 | 18% ? |
| SS nut-bolt | M6–M12 | local | piece / kg | 3–40 each | 7318 | 18% ? |
| Carriage bolt / J-bolt / U-bolt | M8–M16 | local | piece | 5–60 | 7318 | 18% ? |
| Eye bolt / hook bolt | M6–M12 | local | piece | 10–60 | 7318 | 18% ? |
| Hex nut only | M5–M20 | local | piece / kg | 0.5–10 | 7318 | 18% ? |
| Wing nut / lock nut (nylock) | M6–M12 | local | piece | 2–10 | 7318 | 18% ? |
| Plain washer (MS/SS) | 6–20 mm | local | kg / packet | 120–300 per kg | 7318 | 18% ? |
| Spring washer | 6–20 mm | local | packet | 30–100 | 7318 | 18% ? |
| Anchor fastener (sleeve / pin type) | M6/M8/M10/M12 × 50–150 mm | Hilti, Fischer, local | piece / box | 8–60 | 7318 | 18% ? |
| Wedge anchor / drop-in anchor | M8–M12 | Hilti, Fischer | piece | 15–60 | 7318 | 18% ? |
| Chemical anchor | 300 ml cartridge | Hilti, Fischer, Pidilite | piece | 600–1500 | 3214 | 18% ? |
| Rawl plug / gitti (plastic) | 6, 7, 8, 10 mm; 25–50 mm long | Fischer, Rawlplug, local | packet 100 / dozen | 20–80 per 100 | 3926 | 18% ? |
| Screw + gitti combo pack | 6/8 mm | Fischer, local | packet | 20–60 | 7318 | 18% ? |
| Nylon frame fixing | 8/10 × 80–160 mm | Fischer | piece / box | 8–30 | 3926 | 18% ? |
| Pop rivet (aluminium) | 3.2, 4, 4.8 mm × 6–16 mm | local, Taparia | box 100/500 | 60–250 | 7616 / 7318 | 18% ? |
| Rivet gun | hand / heavy | Taparia, Stanley | piece | 250–900 | 8205 | 18% ? |
| Wire nail (keel) | 1", 1.5", 2", 2.5", 3", 4", 5", 6" | Tata Wiron, local | kg | 80–120 | 7317 | 18% ? |
| Concrete nail (steel, hardened) | 1"–4" | local | box / kg | 150–300 per kg | 7317 | 18% ? |
| Panel pin / tack (kabza keel) | 1/2"–1" | local | box / packet | 20–60 | 7317 | 18% ? |
| Roofing nail with umbrella head | 2.5"–4" | local | kg | 100–150 | 7317 | 18% ? |
| Gun staple / stapler pins | 6–14 mm | Kangaro, Stanley | box | 40–150 | 7317 | 18% ? |
| Drawing pin / board pin | standard | local | box | 10–30 | 7317 | 18% ? |
| Cup hook / screw hook | 1/2"–2" brass/SS | local | piece / dozen | 2–15 | 7318 | 18% ? |
| S-hook / J-hook | small–large | local | piece | 5–40 | 7326 | 18% ? |
| Wall hook / cloth hook (khunti) | 1–5 prong SS/brass | Doyours, local | piece | 30–300 | 8302 | 18% ? |
| Adhesive wall hook | 1–5 kg | 3M Command, local | packet | 50–300 | 3926 | 18% ? |
| Ceiling fan hook / J hook | standard | local | piece | 20–60 | 7326 | 18% ? |
| Threaded rod (studding) | M8–M12 × 1 m/3 m | local | piece / metre | 60–300 | 7318 | 18% ? |
| Chain (GI/SS link) | 3–8 mm | local | metre / kg | 40–200 per m | 7315 | 18% ? |
| Wire rope / clamps | 3–12 mm | local | metre / piece | 15–80 | 7312 | 18% ? |
| GI binding wire | 18/20/22 gauge | Tata, local | kg | 80–110 | 7217 | 18% ? |

#### B. Door, window and furniture hardware
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Butt hinge (SS) | 3", 4", 5" × 10/12/14 gauge | Godrej, Dorset, Europa, Ozone, Hafele, local | pair / piece | 60–400 per pair | 8302 | 18% ? |
| Butt hinge (MS/brass) | 2"–5" | local, Link | pair | 25–300 | 8302 | 18% ? |
| Bearing hinge (SS) | 4", 5" | Dorset, Ozone, Godrej, Hafele | piece | 150–600 | 8302 | 18% ? |
| Piano hinge | 1 m, 2 m (1"–2" wide) | local | piece / foot | 120–500 | 8302 | 18% ? |
| Parliament hinge / tee hinge / spring hinge | 3"–6" | local | pair | 80–400 | 8302 | 18% ? |
| Auto (concealed) hinge | 0°, 8 mm crank, 16 mm crank, soft-close | Hettich, Ebco, Hafele, Blum, local | piece / pair | 40–400 | 8302 | 18% ? |
| Hydraulic/soft-close hinge | clip-on 3D | Hettich, Hafele, Ebco | piece | 150–500 | 8302 | 18% ? |
| Floor spring | for 80–120 kg doors | Dorma, Hafele, Godrej, Ozone | piece | 3000–9000 | 8302 | 18% ? |
| Door closer (hydraulic) | 40–80 kg, 3-speed | Godrej, Dorma, Hafele, Ozone, Everite | piece | 800–4000 | 8302 | 18% ? |
| Tower bolt (SS/aluminium/brass) | 4", 6", 8", 10", 12" | Godrej, Europa, local | piece | 30–300 | 8302 | 18% ? |
| Baby latch / door latch | 3"–6" | local | piece | 25–150 | 8302 | 18% ? |
| Aldrop (SS/brass/MS) | 8", 10", 12", 14", 18" | Godrej, Europa, local | piece | 120–800 | 8302 | 18% ? |
| Kadi / kunda-kadi (hasp & staple) | 4"–8" | local | piece / set | 20–150 | 8302 | 18% ? |
| Door handle (pull handle) | 4", 6", 8", 10", 12", D-type | Dorset, Godrej, Ozone, Doyours, local | piece / pair | 60–1500 | 8302 | 18% ? |
| Door handle set on rose/plate (lever) | with lock body | Dorset, Godrej, Hafele, Europa, Yale | set | 900–5000 | 8301 | 18% ? |
| Cabinet / drawer handle | 96, 128, 160, 224, 288 mm; knob | Hettich, Hafele, Ebco, Doyours, Plaza, local | piece | 20–500 | 8302 | 18% ? |
| Profile handle (aluminium) | 1 m–3 m | Hafele, local | piece | 200–1200 | 7610 / 8302 | 18% ? |
| Knob (cabinet) | brass/zinc/ceramic | local | piece | 15–200 | 8302 | 18% ? |
| Door stopper (floor, magnetic, rubber) | SS, zinc | Godrej, Dorset, Europa, local | piece | 50–600 | 8302 | 18% ? |
| Door magnet / magnetic catch | small/big | Ebco, local | piece | 10–80 | 8505 / 8302 | 18% ? |
| Door eye viewer (magic eye) | 180°, 200° | Godrej, Dorset, local | piece | 60–400 | 9013 / 8302 | 18% ? |
| Door chain / safety guard | SS/brass | Godrej, local | piece | 80–400 | 8302 | 18% ? |
| Drawer channel (telescopic ball-bearing) | 10", 12", 14", 16", 18", 20", 22", 24" | Hettich, Ebco, Hafele, Godrej, Ozone, local | pair | 150–900 | 8302 | 18% ? |
| Drawer channel (soft close/undermount) | 300–550 mm | Hettich, Hafele, Ebco, Blum | pair | 800–3000 | 8302 | 18% ? |
| Drawer channel (powder-coated roller) | 10"–20" | local | pair | 60–200 | 8302 | 18% ? |
| Tandem / tandembox drawer system | 450/500 mm | Hettich, Blum, Hafele | set | 2500–8000 | 8302 | 18% ? |
| Gas lift / gas strut | 60N–150N | Hettich, Ebco, local | piece | 120–600 | 8302 | 18% ? |
| Wardrobe sliding fitting | 2-door/3-door | Ebco, Hettich, Hafele | set | 800–4000 | 8302 | 18% ? |
| Shelf support / bracket (L, angle) | 4"–12" | local, Ebco | piece / pair | 10–200 | 8302 | 18% ? |
| Corner bracket / L-angle (furniture) | 1"–4" | local | piece / dozen | 3–30 | 8302 | 18% ? |
| Minifix / cam fitting / connector bolt | 15 mm cam | Hettich, Hafele, Ebco | set / box | 8–25 per set | 8302 | 18% ? |
| Castor wheel | 1.5", 2", 3", 4" plain/with brake | local, Hafele | piece / set 4 | 30–400 | 8302 | 18% ? |
| Sofa leg / table leg | 4"–6" | local | piece / set | 50–400 | 8302 | 18% ? |
| Curtain bracket / curtain rod | 1"/1.25", SS/aluminium | local | piece / set / feet | 30–200 | 8302 | 18% ? |
| Window stay / fastener / handle (casement) | 8"–12" | local, Godrej | piece | 50–300 | 8302 | 18% ? |
| Sliding window roller / lock | aluminium section | local | piece | 15–150 | 8302 | 18% ? |
| Tube rail / wardrobe rail (oval) | 1 m–2.4 m | local | piece | 100–400 | 7306 | 18% ? |
| Kitchen basket / pull-out (SS) | 15"–24" | Ebco, Hettich, local | piece | 800–5000 | 7323 / 9403 | 18% ? |
| Towel rod / towel ring / soap dish (SS) | 18"/24" | Doyours, Parryware, Jaquar, local | piece | 150–1500 | 7324 | 18% ? |

#### C. Locks
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Padlock (brass/iron) | 25, 30, 40, 50, 65, 75 mm | Godrej, Link, Harrison, Europa, Plaza, local | piece | 80–1200 | 8301 | 18% ? |
| Padlock (laminated/shackle hardened) | 40–70 mm | Godrej, Harrison | piece | 250–1200 | 8301 | 18% ? |
| Number / combination lock | 3/4 digit | Godrej, Harrison, local | piece | 120–500 | 8301 | 18% ? |
| Shutter lock / long-shackle | 65–100 mm | Godrej, Link, Harrison | piece | 400–2000 | 8301 | 18% ? |
| Mortise lock body | 6-lever, 8-lever, 65/75 mm, with/without handle | Godrej, Dorset, Europa, Link, Plaza | piece / set | 600–4000 | 8301 | 18% ? |
| Cylinder (mortise) | 60, 70, 80 mm key-key / key-knob | Godrej, Europa, Dorset | piece | 300–2000 | 8301 | 18% ? |
| Rim lock / night latch | 1-way, 2-way, ultra | Godrej (Ultra), Link, Harrison, Europa | piece | 700–3500 | 8301 | 18% ? |
| Main door cylindrical lock / tubular | knob | Godrej, Dorset, Link | piece | 400–1500 | 8301 | 18% ? |
| Cupboard / furniture lock | 20–32 mm | Godrej, Ebco, Hettich, local | piece | 60–400 | 8301 | 18% ? |
| Drawer lock / cam lock | 16–30 mm | Ebco, local | piece | 50–250 | 8301 | 18% ? |
| Glass door lock | showcase | local | piece | 80–300 | 8301 | 18% ? |
| Digital / smart door lock | fingerprint/PIN | Godrej, Yale, Hafele, Dorset | piece | 6000–25000 | 8301 | 18% ? |
| Duplicate key blank / key cutting | per key | local | piece | 20–100 | 8301 | 18% ? |

#### D. Hand tools
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Claw hammer | 250, 450, 570 g | Taparia, Stanley, Jhalani, local | piece | 150–600 | 8205 | 18% ? |
| Ball pein hammer | 100 g–1 kg | Taparia, Jhalani, Stanley | piece | 150–600 | 8205 | 18% ? |
| Sledge hammer (ghan) | 2, 3, 5 kg | Taparia, local | piece | 400–1200 | 8205 | 18% ? |
| Rubber / nylon mallet | small–large | Taparia, Stanley | piece | 150–500 | 8205 | 18% ? |
| Screwdriver (flat/Phillips) | 100–300 mm | Taparia, Stanley, Jhalani, Bosch | piece | 40–250 | 8205 | 18% ? |
| Screwdriver set / bit set | 6–32 pc | Taparia, Stanley, Bosch | set | 150–1500 | 8205 | 18% ? |
| Line tester | 100–500 V | Taparia | piece | 20–80 | 8205 | 18% ? |
| Combination plier | 6", 7", 8" | Taparia, Stanley, Jhalani, Freemans | piece | 150–600 | 8203 | 18% ? |
| Long-nose plier / side cutter / wire stripper | 6"–8" | Taparia, Stanley, Jhalani | piece | 150–500 | 8203 | 18% ? |
| Pincer (pakkad, nail puller) | 6"–10" | Taparia, local | piece | 100–300 | 8203 | 18% ? |
| Circlip plier / locking (vise grip) plier | 7"–10" | Taparia, Stanley | piece | 200–800 | 8203 | 18% ? |
| Double open-end spanner | 6×7 up to 30×32 mm | Taparia, Jhalani, Stanley, Venus | piece / set (8/12 pc) | 40–200 each; 500–2000 set | 8204 | 18% ? |
| Ring spanner / combination spanner | 6–32 mm | Taparia, Jhalani, Stanley | piece / set | 60–300 each | 8204 | 18% ? |
| Adjustable wrench | 6", 8", 10", 12" | Taparia, Stanley, Jhalani, Bahco | piece | 250–1200 | 8204 | 18% ? |
| Pipe wrench | 10", 14", 18", 24" | Taparia, Stanley, Jhalani | piece | 400–2000 | 8204 | 18% ? |
| Socket set | 1/4", 1/2" drive, 8–40 pc | Taparia, Stanley, Jhalani | set | 800–6000 | 8204 | 18% ? |
| Allen key set | 1.5–10 mm; 9 pc | Taparia, Stanley, Unbrako | set / piece | 100–600 | 8204 | 18% ? |
| Measuring tape | 3 m, 5 m, 7.5 m, 10 m | Stanley, Freemans, Taparia, Komelon | piece | 80–700 | 9017 | 18% ? |
| Measuring tape (fibre/steel long) | 15, 30, 50 m | Freemans, Stanley | piece | 300–2000 | 9017 | 18% ? |
| Spirit level | 12", 18", 24", 48" | Stanley, Taparia, local | piece | 150–1200 | 9015 | 18% ? |
| Try square / steel square | 6"–24" | Taparia, Stanley | piece | 100–500 | 9017 | 18% ? |
| Steel scale / foot ruler | 6", 12", 24", 1 m | local | piece | 30–250 | 9017 | 18% ? |
| Plumb bob (sahul) | 200–500 g | local | piece | 60–200 | 9015 | 18% ? |
| Hacksaw frame | 12" adjustable | Taparia, Stanley, Jhalani | piece | 150–600 | 8205 | 18% ? |
| Hacksaw blade | 12" all-hard/flexible, 18/24/32 TPI | Miranda, Kobe, Stanley, Bahco, Taparia | piece / pack 10/100 | 15–60 each | 8202 | 18% ? |
| Hand saw (wood) | 12", 18", 24" | Stanley, Taparia, local | piece | 200–800 | 8202 | 18% ? |
| Keyhole saw / junior hacksaw | small | Stanley, local | piece | 80–300 | 8202 | 18% ? |
| Wood chisel | 6, 12, 18, 25 mm | Taparia, Stanley, local | piece / set | 100–400 | 8205 | 18% ? |
| Cold chisel / pointed chisel (masonry) | 6"–12" | Taparia, local | piece | 80–300 | 8205 | 18% ? |
| File (flat/half-round/round/triangle) | 6", 8", 10", 12" bastard/smooth | JK Files, Bahco, Taparia | piece | 100–500 | 8203 | 18% ? |
| Rasp (wood) | 8"–10" | JK, local | piece | 150–400 | 8203 | 18% ? |
| Wood plane (randa) | wooden / metal No. 4–5 | Stanley, local | piece | 300–2500 | 8205 | 18% ? |
| Tin snips / sheet cutter | 8"–12" | Taparia, Stanley | piece | 250–800 | 8203 | 18% ? |
| Bolt cutter | 14"–36" | Taparia, Jhalani | piece | 600–3000 | 8203 | 18% ? |
| Utility knife / cutter + blades | 9/18 mm | Stanley, Olfa, Taparia | piece / pack | 40–400 | 8211 / 8205 | 18% ? |
| Glass cutter | diamond/wheel | Toyo, local | piece | 60–500 | 8205 | 18% ? |
| Hand drill (manual) / bench vice | 3"–6" vice | Taparia, local | piece | 300–3000 | 8205 | 18% ? |
| C-clamp / G-clamp / F-clamp | 2"–12" | Taparia, local | piece | 100–600 | 8205 | 18% ? |
| Masonry trowel (karni) / plastering float (gurmala) | 6"–10" | local, Stanley | piece | 60–300 | 8205 | 18% ? |
| Putty knife / scraper / spatula | 1"–6" | local, Stanley | piece | 20–200 | 8205 | 18% ? |
| Caulking gun (silicone gun) | standard/heavy | Taparia, Stanley, local | piece | 120–500 | 8205 | 18% ? |
| Grease gun | 300–500 cc | local | piece | 300–900 | 8205 | 18% ? |
| Spade / phawda / kassi | standard | Tata Agrico, Falcon | piece | 250–700 | 8201 | 5% ? |
| Pickaxe (gaiti), crowbar (sabbal) | 2–5 kg / 4–6 ft | Tata Agrico, local | piece | 300–1200 | 8201 / 8205 | 5% / 18% ? |
| Khurpi / sickle / axe / garden shear | standard | Tata Agrico, Falcon | piece | 80–800 | 8201 | 5% ? |
| Tool box (plastic/metal) | 12"–20" | Taparia, Stanley | piece | 250–2000 | 3926 / 7326 | 18% ? |
| Tool kit set (household) | 25–108 pc | Stanley, Bosch, Black+Decker | set | 800–5000 | 8206 | 18% ? |
| Safety gloves / goggles / mask | cotton/latex/cut-resistant | local, 3M, Karam | pair / piece | 20–400 | 6116 / 9004 | 5–18% ? |

#### E. Power tools and accessories
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Impact drill | 10 mm, 13 mm; 500–750 W | Bosch, Dewalt, Black+Decker, Makita, Stanley | piece | 1800–6000 | 8467 | 18% ? |
| Rotary hammer | 2–4 kg SDS | Bosch, Dewalt, Makita | piece | 5000–15000 | 8467 | 18% ? |
| Cordless drill/driver | 12 V/18 V | Bosch, Dewalt, Makita, Black+Decker | piece | 3000–15000 | 8467 | 18% ? |
| Angle grinder | 4" (100 mm), 5", 7" | Bosch, Dewalt, Makita, Stanley | piece | 2000–8000 | 8467 | 18% ? |
| Marble/tile cutter / circular saw / jigsaw | 4"–7" | Bosch, Makita, Dewalt | piece | 3000–10000 | 8467 | 18% ? |
| Heat gun / blower | 1800–2000 W | Bosch, Black+Decker | piece | 1500–6000 | 8516 / 8467 | 18% ? |
| HSS drill bit (metal) | 1–13 mm | Bosch, Addison, Totem, Miranda | piece / set | 15–300 | 8207 | 18% ? |
| Masonry drill bit | 5, 6, 8, 10, 12 mm | Bosch, Dewalt, local | piece | 40–250 | 8207 | 18% ? |
| SDS bit / chisel | 6–25 mm | Bosch, Dewalt | piece | 150–1200 | 8207 | 18% ? |
| Wood bit / spade bit / hole saw | 10–100 mm | Bosch, local | piece / set | 80–1500 | 8207 | 18% ? |
| Screwdriver bits (PH2 etc.) | 25/50/65 mm | Bosch, Taparia | piece / pack | 20–200 | 8207 | 18% ? |
| Cutting wheel (metal) | 4" (105 mm), 14" (355 mm) | Bosch, Norton, Carborundum Universal (CUMI), Dewalt | piece / box 25 | 15–60 (4"); 150–400 (14") | 6804 | 18% ? |
| Grinding wheel | 4", 5", 7" | Norton, CUMI, Bosch | piece | 40–200 | 6804 | 18% ? |
| Diamond cutting blade (marble/tile) | 4", 5", 7" | Bosch, local | piece | 150–1200 | 8202 / 6804 | 18% ? |
| Flap disc | 4" grit 40–120 | Norton, Bosch | piece | 40–150 | 6805 | 18% ? |
| Wood cutting blade (TCT) | 4", 7", 10" | Bosch, local | piece | 200–2000 | 8202 | 18% ? |
| Emery paper / sandpaper (regmal) | grit 60–1500 | Norton, John Oakey, 3M | sheet / metre | 8–40 per sheet | 6805 | 18% ? |
| Wire brush / cup brush | hand / 4" grinder | local, Taparia | piece | 40–250 | 9603 | 18% ? |
| Carbon brush (spare) | for drill/grinder | Bosch, local | pair | 50–250 | 8545 | 18% ? |

#### F. Adhesives, sealants, tapes
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| Fevicol SH / MR (white glue) | 50 g, 125 g, 250 g, 500 g, 1 kg, 2 kg, 5 kg, 10 kg, 20 kg+ | Pidilite Fevicol, Bondtite | piece | 10–3000 | 3506 | 18% ? |
| Fevicol Marine / Heatx | 1 kg–5 kg | Pidilite | piece | 450–2500 | 3506 | 18% ? |
| Synthetic rubber adhesive (SR 998 / Fevibond) | 50 ml–5 L | Pidilite, Vamicol | piece | 30–1800 | 3506 | 18% ? |
| Fevikwik / instant glue | 0.5 g, 3 g, 20 g | Pidilite, Feviquick | piece | 5–150 | 3506 | 18% ? |
| Araldite (epoxy) | standard / Klear, 13 g–180 g | Huntsman Araldite, Pidilite Fevitite | piece / set | 50–600 | 3506 | 18% ? |
| M-Seal (epoxy putty) | 15 g, 25 g, 50 g, 100 g, 250 g, 500 g | Pidilite M-Seal | piece | 15–300 | 3214 | 18% ? |
| Silicone sealant (acetic / neutral) | 280–300 ml clear/white/black | Asian Paints SmartCare, Pidilite Fevicol, Anabond, Dowsil, Wacker | piece | 150–500 | 3214 | 18% ? |
| Acrylic sealant / gap filler | 280 ml | Pidilite, Asian | piece | 120–300 | 3214 | 18% ? |
| PU foam spray | 500–750 ml | Pidilite, local | piece | 400–900 | 3909 / 3214 | 18% ? |
| PVC solvent cement | 25, 50, 100, 237, 500 ml | Supreme, Astral, Finolex, Pidilite | piece | 30–500 | 3506 | 18% ? |
| CPVC solvent cement | 29, 59, 118, 237 ml | Astral, Ashirvad, Supreme | piece | 60–700 | 3506 | 18% ? |
| UPVC solvent cement | 50–237 ml | Astral, Supreme | piece | 60–500 | 3506 | 18% ? |
| Thread sealant (liquid) / Loctite | 10–50 ml | Henkel Loctite, Anabond | piece | 80–500 | 3506 | 18% ? |
| Tile adhesive / grout (small bags) | 1 kg–20 kg | Roff, MYK Laticrete, Pidilite | packet / bag | 50–800 | 3214 | 18% ? |
| Wall putty (small) | 1 kg–5 kg | Birla, JK, Asian | packet | 60–300 | 3214 | 18% ? |
| Wood filler / Fevicol putty | 100 g–1 kg | Pidilite | piece | 50–400 | 3214 | 18% ? |
| Teflon tape (PTFE) | 1/2" × 10 m | Astral, local, Unique | piece / box | 5–25 | 3920 / 3919 | 18% ? |
| Insulation / masking / duct / double-sided tape | 1/2"–2" | Anchor, 3M, Tesa, local | roll | 10–250 | 3919 | 18% ? |
| WD-40 / rust remover spray | 100–420 ml | WD-40, Pidilite | piece | 150–450 | 3403 | 18% ? |
| Grease / machine oil | 100 g–1 kg; 100 ml | Servo, Castrol, local | piece | 30–300 | 2710 | 18% ? |

#### G. Plumbing basics
| Item | Variants | Brands | Unit | Price ₹ ? | HSN | GST |
|---|---|---|---|---|---|---|
| PVC pipe (agri/pressure) | 1/2"–4"; 2.5/4/6 kg-f/cm² | Supreme, Finolex, Prince, Astral, Ashirvad | length 3 m / 6 m / feet | 60–1500 per length | 3917 23 90 | 18% |
| UPVC pipe (SCH 40/80) | 1/2"–2" | Supreme, Astral, Finolex, Prince, Ashirvad | length 3 m | 150–900 | 3917 | 18% |
| CPVC pipe (SDR 11/13.5) | 1/2", 3/4", 1", 1.25", 1.5", 2" | Astral, Ashirvad, Supreme, Finolex, Prince | length 3 m | 200–1500 | 3917 | 18% |
| SWR pipe (drainage) | 75, 110, 160 mm; type A/B | Supreme, Prince, Astral, Finolex | length 3 m/6 m | 400–2500 | 3917 | 18% |
| Elbow 90°/45° (PVC/UPVC/CPVC) | 1/2"–2" | same pipe brands | piece | 5–150 | 3917 40 00 | 18% |
| Tee / equal tee / reducer tee | 1/2"–2" | same | piece | 8–200 | 3917 40 00 | 18% |
| Coupler / socket / reducer | 1/2"–2" | same | piece | 4–100 | 3917 40 00 | 18% |
| MTA / FTA (male/female thread adapter) | 1/2"–2" | same | piece | 8–150 | 3917 40 00 | 18% |
| Brass MTA/FTA (CPVC) | 1/2"–1" | Astral, Ashirvad | piece | 60–400 | 3917 / 7412 | 18% |
| End cap / plug | 1/2"–4" | same | piece | 3–80 | 3917 40 00 | 18% |
| Union | 1/2"–2" | same | piece | 25–300 | 3917 40 00 | 18% |
| SWR bend / tee / Y / door elbow / P-trap / nahani trap | 75/110 mm | Supreme, Prince, Astral | piece | 60–600 | 3917 40 00 | 18% |
| Pipe clip / clamp / saddle | 1/2"–4" | local, Astral | piece / packet | 2–40 | 3926 / 7326 | 18% ? |
| GI pipe fittings (elbow, tee, socket, nipple) | 1/2"–2" | Unik, Tata, local | piece | 15–300 | 7307 | 18% ? |
| Ball valve (PVC/UPVC/CPVC) | 1/2"–2" | Astral, Supreme, Ashirvad | piece | 60–800 | 8481 | 18% |
| Ball valve (brass) | 1/2"–2" | Zoloto, Leader, Sant, local | piece | 200–1500 | 8481 | 18% |
| Gate valve / non-return valve / foot valve | 1/2"–3" | Zoloto, Leader, Sant | piece | 250–3000 | 8481 | 18% |
| Bib cock / pillar cock (tap) | basic chrome / PTMT | Parryware, Jaquar (Continental), Cera, Hindware, local | piece | 150–2500 | 8481 | 18% |
| Angle valve / stop cock | 1/2" | Parryware, Jaquar, Cera, local | piece | 150–1500 | 8481 | 18% |
| PTMT / plastic tap | 1/2" short/long body | Prayag, local | piece | 50–250 | 8481 | 18% |
| Sink cock / swan neck / mixer basic | wall/deck | Parryware, Jaquar, Cera, Hindware | piece | 600–4000 | 8481 | 18% |
| Health faucet with tube + holder | ABS/chrome | Jaquar, Parryware, Cera, local | set | 250–2000 | 8481 | 18% |
| Shower head / hand shower / arm | 4"–8" | Jaquar, Parryware, local | piece | 150–3000 | 8481 | 18% |
| Connection pipe (braided flexible) | 12", 15", 18", 24" | Parryware, Jaquar, local | piece | 60–400 | 3917 / 8307 | 18% ? |
| Washbasin waste coupling / sink waste | 32 mm/40 mm | Parryware, local | piece | 60–600 | 3922 / 7324 | 18% ? |
| Waste pipe (flexible/PVC) | 32 mm × 1 m/1.5 m | local | piece | 50–200 | 3917 | 18% |
| Bottle trap | PVC/chrome | Parryware, Jaquar, local | piece | 150–1500 | 3922 / 7324 | 18% ? |
| Cistern flush tank (PVC) + fittings kit | dual flush | Parryware, Hindware, local | piece / set | 700–2500 | 3922 | 18% ? |
| Toilet seat cover | PVC/soft-close | Parryware, Hindware, local | piece | 300–3000 | 3922 | 18% ? |
| Floor drain / jali (SS/cockroach trap) | 4"–6" | local, Jaquar | piece | 50–600 | 7324 | 18% ? |
| Tap washer / rubber washer / O-ring | 1/2", 3/4" | local | packet / piece | 1–30 | 4016 | 18% ? |
| Tap spindle / cartridge (spare) | 1/2" quarter-turn | local, Jaquar | piece | 80–600 | 8481 | 18% ? |
| Garden hose pipe | 1/2", 3/4"; per metre or 15/30 m roll | local, Prince | metre / roll | 15–60 per m | 3917 | 18% |
| Hose clamp / jubilee clip | 1/2"–2" | local | piece | 5–40 | 7326 | 18% ? |
| Water tank fittings (connector, float valve/ball cock) | 1/2"–1" | Sintex, local | piece | 60–500 | 8481 | 18% |
| Kitchen sink (SS) | 18×16" up to 45×20" | Nirali, Futura, Carysil, local | piece | 1500–10000 | 7324 | 18% ? |

### Gaps
- No item prices, brand lists or units were checked against IndiaMART, Moglix, Industrybuying or brand catalogues, because of the tool-call budget. Every price above is an unsourced estimate ("?").
- Heading-level GST for 7317/7318/8203–8205/8301/8302/8481 after Sept 2025 is not confirmed against the CBIC notification.
- Paint and electrical are out of scope here.
