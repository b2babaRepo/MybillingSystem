import { describe, expect, it } from 'vitest'
import { exportPrice, landedCost } from './tradeCosting'

describe('exportPrice', () => {
  it('builds ex-works, FOB and CIF, with insurance on the CIF value and RoDTEP on FOB', () => {
    const p = exportPrice({
      goodsCost: 40000, marginPercent: 25, packing: 3000, inlandFreight: 2000, portCharges: 5000,
      oceanFreight: 9000, insurancePercent: 0.5, exchangeRate: 85, rodtepPercent: 0.7,
    })
    expect(p.exWorks).toBe(50000)
    expect(p.fob).toBe(60000)
    expect(p.cif).toBe(69346.73)
    expect(p.insurance).toBe(346.73)
    expect(p.fobForeign).toBe(705.88)
    expect(p.rodtep).toBe(420)
  })
})

describe('landedCost', () => {
  it('stacks duty, surcharge and IGST, and leaves IGST out of the real cost', () => {
    const c = landedCost({ priceForeign: 1000, freightForeign: 100, exchangeRate: 85, basicDutyPercent: 15, surchargePercent: 10, igstPercent: 18, clearing: 5000 })
    expect(c.cif).toBe(93500)
    expect(c.basicDuty).toBe(14025)
    expect(c.surcharge).toBe(1402.5)
    expect(c.igst).toBe(19606.95)
    expect(c.totalPaid).toBe(133534.45)
    expect(c.landedCost).toBe(113927.5)
  })
})
