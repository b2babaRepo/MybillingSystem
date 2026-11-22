/**
 * An e-way bill file for one bill, in the JSON the e-way bill portal (ewaybillgst.gov.in, "Generate Bulk") takes. Needed
 * for goods over ₹50,000 moving by vehicle. The shop uploads it there; the portal checks it and gives the EWB number.
 */
import type { BillView, Unit } from './types'

/** What the bill doesn't know: where the goods go from and to, how far, and how. */
export interface EwayTrip {
  fromPincode: string
  toPincode: string
  distanceKm: number
  vehicleNo: string
  /** Who the goods go to, when the buyer has no GSTIN. */
  buyerName: string
}

const UNIT_CODES: Partial<Record<Unit, string>> = {
  piece: 'NOS', kg: 'KGS', g: 'GMS', litre: 'LTR', ml: 'MLT', packet: 'PAC', pair: 'PRS', dozen: 'DOZ', box: 'BOX',
  bag: 'BAG', tonne: 'TON', metre: 'MTR', sq_ft: 'SQF',
}

const r2 = (n: number) => Math.round(n * 100) / 100
const ddmmyyyy = (iso: string) => {
  const d = new Date(Date.parse(iso) + 19_800_000) // India time
  return `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}/${d.getUTCFullYear()}`
}

/** The e-way bill JSON for a GST bill, or the reasons it can't be made yet. */
export function ewayBillJson(shopName: string, bill: BillView['bill'], trip: EwayTrip): { json: object } | { problems: string[] } {
  const problems: string[] = []
  const gstin = bill.gstin ?? ''
  const fromState = Number(gstin.slice(0, 2))
  const toState = Number((bill.buyerGstin ?? '').slice(0, 2) || bill.placeOfSupply || fromState)
  if (!gstin) problems.push('The shop has no GSTIN.')
  if (!/^\d{6}$/.test(trip.fromPincode)) problems.push('Enter the shop’s 6-digit pincode.')
  if (!/^\d{6}$/.test(trip.toPincode)) problems.push('Enter the delivery 6-digit pincode.')
  if (!(trip.distanceKm >= 1 && trip.distanceKm <= 4000)) problems.push('Enter the distance in km (1 to 4000).')
  if (!/^[A-Z]{2}\d{1,2}[A-Z]{0,3}\d{4}$/.test(trip.vehicleNo.replace(/[\s-]/g, '').toUpperCase())) problems.push('Enter the vehicle number, like GJ01AB1234.')
  const missingHsn = bill.lines.filter((l) => !l.hsnCode)
  if (missingHsn.length) problems.push(`Add the HSN code to: ${missingHsn.map((l) => l.name).join(', ')}.`)
  if (problems.length) return { problems }

  const inter = bill.lines.some((l) => (l.igst ?? 0) > 0) || fromState !== toState
  // The tax the server kept on each line; older bills: take GST back out of the amount (prices include it).
  const items = bill.lines.map((l, i) => {
    const rate = l.gstRate ?? 0
    const taxable = l.taxableValue != null ? r2(l.taxableValue) : r2(l.amount / (1 + rate / 100))
    return {
      itemNo: i + 1, productName: (l.printName ?? l.name).slice(0, 100), productDesc: (l.printName ?? l.name).slice(0, 100),
      hsnCode: Number(l.hsnCode), quantity: l.quantity, qtyUnit: UNIT_CODES[l.unit] ?? 'OTH', taxableAmount: taxable,
      sgstRate: inter ? 0 : rate / 2, cgstRate: inter ? 0 : rate / 2, igstRate: inter ? rate : 0, cessRate: 0, tax: r2(l.amount - taxable),
    }
  })
  const taxable = r2(items.reduce((s, i) => s + i.taxableAmount, 0))
  const tax = r2(items.reduce((s, i) => s + i.tax, 0))
  const half = r2(tax / 2)
  return {
    json: {
      version: '1.0.0621',
      billLists: [{
        userGstin: gstin, supplyType: 'O', subSupplyType: 1, subSupplyDesc: '', docType: 'INV',
        docNo: bill.billNo ?? bill.id.slice(0, 16), docDate: ddmmyyyy(bill.occurredAt), transType: 1,
        fromGstin: gstin, fromTrdName: shopName, fromAddr1: (bill.shopAddress ?? '').slice(0, 120), fromAddr2: '', fromPlace: '',
        fromPincode: Number(trip.fromPincode), fromStateCode: fromState, actualFromStateCode: fromState,
        toGstin: bill.buyerGstin || 'URP', toTrdName: trip.buyerName.slice(0, 100), toAddr1: (bill.buyerAddress ?? '').slice(0, 120), toAddr2: '', toPlace: '',
        toPincode: Number(trip.toPincode), toStateCode: toState, actualToStateCode: toState,
        totalValue: taxable, cgstValue: inter ? 0 : half, sgstValue: inter ? 0 : r2(tax - half), igstValue: inter ? tax : 0, cessValue: 0,
        cessNonAdvolValue: 0, otherValue: r2(bill.total - taxable - tax), totInvValue: bill.total,
        transMode: 1, transDistance: String(Math.round(trip.distanceKm)), transporterId: '', transporterName: '', transDocNo: '', transDocDate: '',
        vehicleNo: trip.vehicleNo.replace(/[\s-]/g, '').toUpperCase(), vehicleType: 'R', mainHsnCode: items.reduce((a, b) => (b.taxableAmount > a.taxableAmount ? b : a)).hsnCode,
        itemList: items.map(({ tax: _tax, ...item }) => item),
      }],
    },
  }
}

/** A 6-digit pincode written inside an address, if there is one. */
export const pincodeIn = (text: string | null | undefined) => text?.match(/\b\d{6}\b/)?.[0] ?? ''
