# Hardware, paint and water items: category menu

This adds to section "Hardware and paint" of `reports/Category menu for all shop types.md`. It takes the category list you sent (hardware, paints and finishes, water and plumbing) and maps the app's existing master items into it. A hardware shop's rail would then have **water and plumbing as its own department**, instead of keeping it inside one large "Pipes and fittings" category with glue and tape mixed in.

Item counts below are from `hardware-master.csv` (319 items) and `paint-master.csv` (337 items), grouped by name. A few items will need moving by hand when the tree is built (for example, "Tower bolt" is under Fasteners in the CSV but belongs with hinges and bolts).

## Hardware shop: left rail and tiles

| Rail (Hindi · English) | Tiles inside | Items today | Quick filter chips |
|---|---|---|---|
| ★ रोज़ के · Your usual | Most sold this month | — | — |
| पानी, प्लंबिंग · Water & plumbing | Pipes (CPVC, UPVC, PVC, GI, hose) · Pipe fittings (elbow, tee, reducer, coupler, union, end cap, MTA/FTA) · Valves (ball, gate, NRV, foot, float, angle) · Taps & bathroom (bib cock, health faucet, shower, sink, flush tank) · Drainage & traps (SWR, P-trap, bottle trap, floor jaali, waste pipe) · Solvent & Teflon · Tanks & pumps | ~55 | Pipe type (CPVC/UPVC/PVC/GI) · Size (15 mm (½") … 50 mm (2")) · Hot/cold · Brand |
| पेंच, कील, नट-बोल्ट · Fasteners | Screws (wood, drywall, self-tapping, roofing) · Nails & pins · Nut-bolt & washers · Anchors & gitti · Hooks & chain | ~46 | Size · Material (MS/SS/GI/brass) · Sold by piece/box/kg |
| दरवाज़ा, फ़र्नीचर · Door & furniture | Hinges & tower bolts · Handles, knobs, drawer channels, magnets · Door closers & stoppers · Locks & padlocks · Doors & windows | ~56 | Material · Finish · Length (inch) |
| औज़ार · Tools | Hand tools (hammer, screwdriver, spanner, plier, measuring tape, spirit level) · Power tools (drill, grinder, cutter) · Blades, bits & abrasives (sandpaper, grinding/cutting wheel) · Farm tools | ~84 | Brand · Power tool / hand tool · Size |
| गोंद, सीलेंट · Adhesives & sealants | Wood glue (Fevicol) · Epoxy (Araldite, M-Seal) · Silicone & acrylic sealant · Tapes (masking, insulation, duct) · PU foam, WD-40 | ~14 | Type · Brand |
| सेफ़्टी · Safety | Gloves · Goggles · Helmets · Masks · Safety shoes | 1 | — |
| बिल्डिंग मटीरियल · Building material | Cement · Sariya & steel · Roofing sheets · Binding wire, mesh, barbed wire | ~57 | Size (mm) · Brand · Bag/kg/piece |

## Paint shop: left rail and tiles

| Rail (Hindi · English) | Tiles inside | Items today | Quick filter chips |
|---|---|---|---|
| ★ रोज़ के · Your usual | Most sold, last shade for this customer | — | — |
| अंदर की दीवार · Interior | Emulsion (matt, satin, sheen) · Distemper | 75 | Brand · Range (luxury/premium/economy) · Pack 1/4/10/20 L |
| बाहर की दीवार · Exterior | Weatherproof emulsion · Roof and tile coats | 45 | Brand · Range · Pack |
| इनेमल, लकड़ी · Enamel & wood | Synthetic enamel (metal and wood) · Wood stain, varnish, lacquer, PU · Spray paint | 54 | Finish (gloss/matt) · Pack |
| प्राइमर, पुट्टी · Primer & putty | Cement primer · Metal primer (red oxide, zinc chromate) · Wood primer · Wall putty (acrylic and white cement) · Texture · White cement | 57 | Surface (wall/metal/wood) · Pack (kg/L) |
| वॉटरप्रूफ़िंग · Waterproofing & special | Waterproofing · Damp-proof · Crack fill · Heat-reflective roof paint | 30 | Where (roof/wall/bathroom/tank) |
| ब्रश, थिनर · Tools & thinner | Brushes · Rollers & trays · Scrapers & putty knives · Spray gun · Masking tape, sandpaper, drop cloth · Thinner (turpentine, NC, PU) · Stainers | 59 | Size (1"–4") · Type |

The paint bill line keeps working as it does now: pick product, then the pack chip (1/4/10/20 L), then the tint sheet "base · shade code", with "repeat last shade" for that customer.

A shop that sells both hardware and paint gets the trade switch at the top of the menu (हार्डवेयर | पेंट), as in the main report.

## How the water and plumbing row works

Plumbing items differ mostly by size, so one row shows one item with size chips, not one row per size:

> **Elbow 90° CPVC (Astral)** · 15 · 20 · 25 · 32 · 40 · 50 mm, with stock under each chip and the price changing as you tap a size. Toggle: piece / box.

Sizes show both ways, "20 mm (¾")", because customers ask in inches and the packing says mm. Search understands `3/4`, `¾`, `20mm` and `pauna inch` as the same size.

## Items on your list that the app's master list does not have yet

These would be added to the master CSVs, so a new shop sees them when it starts:

| Department | Missing items |
|---|---|
| Water & plumbing | PPR pipes and fittings · HDPE pipe · Pressure-reducing valve · Overhead water tanks (500/1000/2000 L) · Submersible, monoblock and booster pumps · Water purifiers, RO filter cartridges and sediment filters · Brass threaded inserts |
| Safety | Gloves, goggles, helmets, masks and safety shoes as separate items (today one combined row) |
| Hardware | Grinding/abrasive items are there; wire rope and chains are there; nothing else major missing |
| Paint | Heat-reflective (cool roof) paint · Zinc chromate primer as its own item |

GST rates and HSN codes for the new rows need checking before they are added, because several rates changed in 2025.

## What changes in the build plan

Step 8 of the main plan (hardware and paint, 2.5–3 days) now also covers:
- splitting "Pipes and fittings" into the seven water and plumbing tiles above, and moving glue and tape to Adhesives;
- the size-chip row for pipes and fittings;
- adding the missing items listed above (about half a day, plus checking GST).
