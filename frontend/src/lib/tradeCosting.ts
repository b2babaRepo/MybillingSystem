/**
 * Export and import costing for a workshop: worked out on the phone, nothing saved. Rates are starting points found
 * in 2026 (RoDTEP for furniture ≈ 0.7% of FOB; fittings: 15% basic duty, 10% welfare surcharge on it, 18% IGST) and
 * must be checked against DGFT, CBIC and the shop's CA or customs agent before quoting.
 */

const round = (n: number) => Math.round(n * 100) / 100

export interface ExportInput {
  /** What the goods cost the shop to make (or the job's price ex-works), in rupees. */
  goodsCost: number
  /** Margin on the goods cost, in %. */
  marginPercent: number
  /** Export-grade packing and the fumigated (ISPM-15) crate. */
  packing: number
  /** Workshop to port: truck or container haulage. */
  inlandFreight: number
  /** Customs agent (CHA), port and loading charges. */
  portCharges: number
  /** Sea or air freight to the buyer's port: only for a CIF price. */
  oceanFreight: number
  /** Marine insurance, in % of the CIF value (often near 0.5%). */
  insurancePercent: number
  /** Rupees for one unit of the buyer's currency (₹ per US$). */
  exchangeRate: number
  /** RoDTEP refund on the FOB value, in %. */
  rodtepPercent: number
}

export interface ExportPrice {
  exWorks: number
  fob: number
  insurance: number
  cif: number
  fobForeign: number
  cifForeign: number
  /** Money the government pays back on the FOB value: due to the shop, not part of the price. */
  rodtep: number
}

/**
 * Builds the price up the way exporters quote it: ex-works (cost and margin), then FOB (packed, at the port, loaded),
 * then CIF (shipped and insured to the buyer's port). Exported under LUT, so no GST is charged on it.
 */
export function exportPrice(i: ExportInput): ExportPrice {
  const exWorks = round(i.goodsCost * (1 + i.marginPercent / 100))
  const fob = round(exWorks + i.packing + i.inlandFreight + i.portCharges)
  // Insurance is charged on the CIF value itself: CIF = (FOB + freight) ÷ (1 − rate).
  const rate = i.insurancePercent / 100
  const cif = round((fob + i.oceanFreight) / (1 - rate))
  const insurance = round(cif - fob - i.oceanFreight)
  const fx = i.exchangeRate > 0 ? i.exchangeRate : 1
  return { exWorks, fob, insurance, cif, fobForeign: round(fob / fx), cifForeign: round(cif / fx), rodtep: round((fob * i.rodtepPercent) / 100) }
}

export interface ImportInput {
  /** The supplier's price, in their currency. */
  priceForeign: number
  /** Freight and insurance to the Indian port, in the same currency (0 when the price is already CIF). */
  freightForeign: number
  /** Rupees for one unit of that currency, as on the bill of entry. */
  exchangeRate: number
  /** Basic customs duty, in % of the CIF value. */
  basicDutyPercent: number
  /** Social welfare surcharge, in % of the basic duty. */
  surchargePercent: number
  /** IGST, in % of CIF plus the duties. */
  igstPercent: number
  /** Customs agent, port and transport to the workshop, in rupees. */
  clearing: number
}

export interface LandedCost {
  cif: number
  basicDuty: number
  surcharge: number
  igst: number
  /** Everything paid to get the goods into the workshop. */
  totalPaid: number
  /** What the goods really cost the shop: the IGST comes back as input tax credit (when GST-registered). */
  landedCost: number
}

/** Landed cost of imported fittings, veneer or a machine: duties stack on each other, and only IGST is claimed back. */
export function landedCost(i: ImportInput): LandedCost {
  const cif = round((i.priceForeign + i.freightForeign) * i.exchangeRate)
  const basicDuty = round((cif * i.basicDutyPercent) / 100)
  const surcharge = round((basicDuty * i.surchargePercent) / 100)
  const igst = round(((cif + basicDuty + surcharge) * i.igstPercent) / 100)
  const totalPaid = round(cif + basicDuty + surcharge + igst + i.clearing)
  return { cif, basicDuty, surcharge, igst, totalPaid, landedCost: round(totalPaid - igst) }
}
