/**
 * Prints to a Bluetooth (BLE) thermal printer straight from the browser (Chrome on Android, Windows, Mac; not iPhone).
 * The shop picks the printer once; after that the same printer is used until it fails, then they pick again.
 */
import { DRAWER_KICK, dotsFor, drawReceipt, rasterBytes, type ReceiptRow } from './escpos'

// The services cheap ESC/POS printers (Goojprt, Xprinter, MTP, Rongta, Epson TM-m…) offer for writing.
const SERVICES = [
  '000018f0-0000-1000-8000-00805f9b34fb',
  'e7810a71-73ae-499d-8c15-faa9aef0c3f2',
  '49535343-fe7d-4ae5-8fa9-9fafd205e455',
  '0000ff00-0000-1000-8000-00805f9b34fb',
  '0000fee7-0000-1000-8000-00805f9b34fb',
  '0000ae30-0000-1000-8000-00805f9b34fb',
]

/* Web Bluetooth isn't in TypeScript's DOM types yet: the small part used here. */
interface Characteristic { properties: { write: boolean; writeWithoutResponse: boolean }; writeValue(data: BufferSource): Promise<void>; writeValueWithoutResponse?(data: BufferSource): Promise<void> }
interface Service { getCharacteristics(): Promise<Characteristic[]> }
interface Server { connected: boolean; connect(): Promise<Server>; getPrimaryServices(): Promise<Service[]> }
interface Device { id: string; name?: string; gatt?: Server }
interface Bluetooth { requestDevice(options: object): Promise<Device>; getDevices?(): Promise<Device[]> }

const bluetooth = () => (navigator as Navigator & { bluetooth?: Bluetooth }).bluetooth
export const canBluetoothPrint = () => typeof navigator !== 'undefined' && !!bluetooth()

const KEY = 'gk-bt-printer'
let device: Device | null = null
let channel: Characteristic | null = null

async function pick(): Promise<Device> {
  // Many printers don't advertise their service, so list everything nearby and ask for the printer services.
  const chosen = await bluetooth()!.requestDevice({ acceptAllDevices: true, optionalServices: SERVICES })
  try { localStorage.setItem(KEY, chosen.id) } catch { /* no storage */ }
  return chosen
}

async function remembered(): Promise<Device | null> {
  try {
    const id = localStorage.getItem(KEY)
    if (!id || !bluetooth()!.getDevices) return null
    return (await bluetooth()!.getDevices!()).find((d) => d.id === id) ?? null
  } catch { return null }
}

async function connect(choose: boolean): Promise<Characteristic> {
  if (choose || !device) device = (!choose && (await remembered())) || (await pick())
  const server = device.gatt!.connected ? device.gatt! : await device.gatt!.connect()
  if (channel && server.connected) return channel
  for (const service of await server.getPrimaryServices()) {
    for (const c of await service.getCharacteristics()) {
      if (c.properties.write || c.properties.writeWithoutResponse) return (channel = c)
    }
  }
  throw new Error('This Bluetooth device is not a printer the app can use.')
}

async function send(c: Characteristic, bytes: Uint8Array) {
  // BLE takes small pieces; a short pause stops cheap printers dropping data.
  const size = 180
  for (let i = 0; i < bytes.length; i += size) {
    const part = bytes.slice(i, i + size)
    if (c.properties.writeWithoutResponse && c.writeValueWithoutResponse) {
      await c.writeValueWithoutResponse(part)
      await new Promise((r) => setTimeout(r, 20))
    } else {
      await c.writeValue(part)
    }
  }
}

/** Open the cash drawer on this device, if it opens drawers (see drawerOnCashBill). */
const DRAWER_KEY = 'gk-cash-drawer'
export function drawerOnCashBill(): boolean {
  try { return localStorage.getItem(DRAWER_KEY) === '1' } catch { return false }
}
export function setDrawerOnCashBill(on: boolean) {
  try { if (on) localStorage.setItem(DRAWER_KEY, '1'); else localStorage.removeItem(DRAWER_KEY) } catch { /* no storage */ }
}

/** Open the cash drawer through the Bluetooth printer it plugs into, with nothing printed. Returns the printer's name. */
export async function openDrawer(choose = false): Promise<string> {
  return sendBytes(new Uint8Array(DRAWER_KICK), choose)
}

/** Prints the receipt (and opens the cash drawer after it, if asked); `choose` asks for the printer again. Returns the printer's name. */
export async function bluetoothPrint(rows: ReceiptRow[], paper: string, choose = false, withDrawer = false): Promise<string> {
  return sendBytes(rasterBytes(drawReceipt(rows, dotsFor(paper)), 96, withDrawer), choose)
}

async function sendBytes(bytes: Uint8Array, choose: boolean): Promise<string> {
  try {
    await send(await connect(choose), bytes)
  } catch (e) {
    // A printer that was switched off or swapped: forget it, so the next try asks again.
    channel = null
    if (!choose && device) { device = null; try { localStorage.removeItem(KEY) } catch { /* no storage */ } }
    throw e
  }
  return device?.name ?? 'printer'
}
