// Jewellery shop types and small helpers shared by the jewellery screens, the tag printer and the printed bill.

export type Metal = 'gold' | 'silver'
export type MakingType = 'per_gram' | 'percent' | 'per_piece'

export const GOLD_PURITIES = [999, 995, 916, 750, 585] as const
export const SILVER_PURITIES = [999, 925] as const

export interface MetalRate { metal: Metal; purity: number; sellPerGram: number; buyPerGram: number | null; setAt: string; derived: boolean }
export interface MetalRates { current: MetalRate[]; today: MetalRate[] }

export interface JewelleryPiece {
  id: string; tagNo: number; name: string; category: string | null; metal: Metal; purity: number; gross: number; stone: number; net: number
  wastagePercent: number; makingType: MakingType; makingValue: number; stoneValue: number; hallmarkCharge: number; huid: string | null
  fixedPrice: number | null; sold: boolean; soldBillId: string | null; createdAt: string; onApproval: boolean
}

export interface JewelleryLine {
  lineNo: number; pieceId: string | null; tagNo: number | null; name: string; metal: Metal; purity: number; gross: number; stone: number; net: number
  ratePerGram: number; metalValue: number; wastagePercent: number; wastageValue: number; makingType: MakingType; makingValue: number; makingAmount: number
  stoneValue: number; hallmarkCharge: number; huid: string | null; fixedPrice: boolean; taxable: number; gst: number
}

export interface OldMetal { description: string | null; metal: Metal; gross: number; reading: number; fine: number; ratePerGram: number; deductionPercent: number; deduction: number; value: number }

export interface JewelleryQuote {
  lines: JewelleryLine[]; taxable: number; gst: number; roundOff: number; total: number; oldMetal: OldMetal[]; oldMetalValue: number; toPay: number
  panNeeded: boolean; cashLimit: number; problems: string[]; schemeDiscount: number; khataAdvance: number
}

export interface JewelleryBill { billId: string; pan: string | null; moneyPaid: number; mode: string; oldMetalValue: number; lines: JewelleryLine[]; oldMetal: OldMetal[] }

/** 916 → "22K"; silver shows its fineness (925). */
export function karat(metal: Metal, purity: number) {
  if (metal !== 'gold') return String(purity)
  return ({ 999: '24K', 995: '24K', 916: '22K', 750: '18K', 585: '14K' } as Record<number, string>)[purity] ?? String(purity)
}

/** Grams to 3 decimals, as a jewellery bill shows them. */
export const grams = (g: number) => `${g.toFixed(3)} g`

/** The tag's code: J12 (Code 128 on the tag); the bill's search reads it back. */
export const tagCode = (tagNo: number) => `J${tagNo}`
