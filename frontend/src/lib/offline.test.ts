import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { pressKey } from './keypad'
import { uuidv7 } from './ids'
import { parseCsv } from './catalogue'
import { buildReminder } from './reports'
import { mergeCustomers, mergeProducts, mergeStatement, mergeToday } from './local'
import { mergeLists } from './planner'
import { enqueue, flush, listOps, type OutboxOp } from './outbox'
import { setSession } from './session'
import type { Statement } from './types'

const shopId = 'shop-1'

function op(partial: Partial<OutboxOp>): OutboxOp {
  return {
    id: uuidv7(),
    shopId,
    kind: 'khata',
    path: '/v1/khata-entries',
    body: {},
    label: '',
    createdAt: new Date().toISOString(),
    attempts: 0,
    status: 'pending',
    ...partial,
  }
}

describe('uuidv7', () => {
  it('is a version 7 UUID and sorts by time', () => {
    const earlier = uuidv7(1_000)
    const later = uuidv7(2_000)
    expect(earlier).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/)
    expect(earlier < later).toBe(true)
  })
})

describe('keypad', () => {
  it('allows one decimal point and at most two paise digits', () => {
    const typed = ['3', '4', '0', '.', '5', '0', '9', '.'].reduce((value, key) => pressKey(value, key), '')
    expect(typed).toBe('340.50')
    expect(pressKey('', '.')).toBe('0.')
    expect(pressKey('0', '5')).toBe('5')
    expect(pressKey('12', '⌫')).toBe('1')
  })
})

describe('merging unsynced writes into what the shopkeeper sees', () => {
  const server = [{ id: 'ramesh', name: 'Ramesh', phone: null, balance: 100 }]

  it('adds pending khata entries to the balance and shows new customers', () => {
    const merged = mergeCustomers(server, [
      op({ customerId: 'ramesh', delta: 340 }),
      op({ customerId: 'ramesh', delta: -40 }),
      op({ kind: 'customer', id: 'new', body: { id: 'new', name: 'Anil', phone: null } }),
      op({ customerId: 'new', delta: 50 }),
    ])
    expect(merged.find((c) => c.id === 'ramesh')).toMatchObject({ balance: 400, pending: 2, isNew: false })
    expect(merged.find((c) => c.id === 'new')).toMatchObject({ balance: 50, isNew: true })
  })

  it('ignores ops the server refused', () => {
    const merged = mergeCustomers(server, [op({ customerId: 'ramesh', delta: 999, status: 'failed' })])
    expect(merged[0].balance).toBe(100)
  })

  it('keeps paise exact', () => {
    const merged = mergeCustomers([{ ...server[0], balance: 0 }], [op({ customerId: 'ramesh', delta: 0.1 }), op({ customerId: 'ramesh', delta: 0.2 })])
    expect(merged[0].balance).toBe(0.3)
  })

  it('appends pending entries to the statement with a running balance, and strikes out pending reversals', () => {
    const statement: Statement = {
      customer: server[0],
      entries: [{ id: 'e1', kind: 'credit', amount: 100, paymentMode: null, billId: null, reversesEntryId: null, note: null, occurredAt: '2026-10-01T10:00:00Z', runningBalance: 100, isReversed: false }],
      openingBalance: 0,
      closingBalance: 100,
      month: null,
    }
    const [customer] = mergeCustomers(server, [])
    const { entries } = mergeStatement(statement, customer, [
      op({ customerId: 'ramesh', delta: 50 }),
      op({ kind: 'reverse', customerId: 'ramesh', delta: -100, reversesEntryId: 'e1' }),
    ])
    expect(entries.map((e) => e.runningBalance)).toEqual([100, 150, 50])
    expect(entries[0].isReversed).toBe(true)
    expect(entries[2]).toMatchObject({ kind: 'reversal', pending: true })
  })
})

describe('home screen numbers offline', () => {
  it('adds unsynced bills and khata entries to today', () => {
    const server = { date: '2026-10-27', sales: 100, bills: 1, cash: 100, upi: 0, udhaar: 0, outstanding: 0, customersOwing: 0, topDues: [] }
    const today = mergeToday(server, [
      op({ kind: 'bill', body: { customerId: 'ramesh', lines: [{ quantity: 2.5, unitPrice: 44 }], paidAmount: 30, paymentMode: 'upi' } }),
      op({ kind: 'khata', body: { type: 'credit', amount: 340 } }),
      op({ kind: 'khata', body: { type: 'payment', amount: 50, paymentMode: 'cash' } }),
      op({ kind: 'khata', body: { type: 'credit', amount: 999 }, status: 'failed' }),
    ])
    expect(today).toMatchObject({ sales: 210, bills: 2, cash: 150, upi: 30, udhaar: 420 })
  })
})

describe('household lists offline', () => {
  it('applies unsynced adds, edits and deletes, and clears ticks once a list is sent', () => {
    const server = [{ kind: 'monthly' as const, items: [
      { id: 'atta', name: 'Atta', quantity: 10, unit: 'kg' as const, checked: true, note: null },
      { id: 'dal', name: 'Dal', quantity: 2, unit: 'kg' as const, checked: false, note: null },
    ] }]
    const lists = mergeLists(server, [
      op({ kind: 'listItem', id: 'atta', listKind: 'monthly', body: { name: 'Atta', quantity: 5, unit: 'kg', checked: true, note: null } }),
      op({ kind: 'listItemDelete', id: 'dal', listKind: 'monthly' }),
      op({ kind: 'listItem', id: 'milk', listKind: 'weekly', body: { name: 'Milk', quantity: 6, unit: 'litre', checked: false, note: null } }),
      op({ kind: 'order', listKind: 'monthly', body: {} }),
    ])
    expect(lists.find((l) => l.kind === 'monthly')!.items).toEqual([expect.objectContaining({ id: 'atta', quantity: 5, checked: false, pending: true })])
    expect(lists.find((l) => l.kind === 'weekly')!.items.map((i) => i.name)).toEqual(['Milk'])
  })
})

describe('importing items', () => {
  it('reads CSV or Excel paste, skips a header, understands Hindi units and flags unknown ones', () => {
    const { rows, errors } = parseCsv([
      'Name,Unit,Price,Cost,Category,Stock,Reorder',
      '"Aashirvaad Atta, 5kg",pkt,₹245,230,Atta & rice,12,4',
      'चीनी\tकिलो\t48',
      'Oil,barrel,100',
    ].join('\n'))
    expect(rows).toEqual([
      { name: 'Aashirvaad Atta, 5kg', unit: 'packet', sellingPrice: 245, costPrice: 230, category: 'Atta & rice', openingStock: 12, reorderLevel: 4 },
      { name: 'चीनी', unit: 'kg', sellingPrice: 48, costPrice: null, category: null, openingStock: null, reorderLevel: null },
    ])
    expect(errors).toEqual([{ row: 4, unit: 'barrel' }])
  })
})

describe('stock offline', () => {
  it('moves the shelf count for unsynced purchases, adjustments and bills', () => {
    const server = [{ id: 'atta', name: 'Atta', category: null, unit: 'kg' as const, sellingPrice: 44, costPrice: null, stock: 6, reorderLevel: 5, isLow: false }]
    const [atta] = mergeProducts(server, [
      op({ kind: 'stock', body: { productId: 'atta', quantity: 10 } }),
      op({ kind: 'stock', body: { productId: 'atta', quantity: -1.5 } }),
      op({ kind: 'bill', body: { lines: [{ productId: 'atta', quantity: 12 }, { productId: null, quantity: 1 }] } }),
    ])
    expect(atta).toMatchObject({ stock: 2.5, isLow: true, pending: true })
  })
})

describe('payment reminders', () => {
  it('fills the shop template and adds the UPI ID when the template does not mention it', () => {
    expect(buildReminder(null, { name: 'Suresh', amount: 1320, shop: 'Sharma Store', upi: 'sharma@okaxis' }))
      .toBe('Namaste Suresh ji, Sharma Store mein aapka ₹1,320 baaki hai. Suvidha anusaar jama kar dein. Dhanyavaad!\nUPI: sharma@okaxis')
    expect(buildReminder('{name}: ₹{amount}. Pay to {upi}', { name: 'Anita', amount: 99.5, shop: 'X', upi: 'a@ybl' }))
      .toBe('Anita: ₹99.5. Pay to a@ybl')
    expect(buildReminder('{name} ₹{amount}', { name: 'Mohan', amount: 10, shop: 'X', upi: null })).toBe('Mohan ₹10')
  })
})

describe('outbox', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockReset()
  })

  afterEach(async () => {
    // Empty the outbox between tests.
    fetchMock.mockResolvedValue(new Response('{}', { status: 200 }))
    await flush(shopId)
    vi.unstubAllGlobals()
  })

  it('keeps writes on the phone while offline, then sends them in order once, keeping their IDs', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    await enqueue({ id: 'a', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'a' }, label: 'a' })
    await enqueue({ id: 'b', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'b' }, label: 'b' })
    await flush(shopId)
    expect((await listOps(shopId)).map((o) => o.id)).toEqual(['a', 'b'])

    fetchMock.mockReset()
    fetchMock.mockImplementation(async () => new Response('{}', { status: 201 }))
    await flush(shopId)

    expect(await listOps(shopId)).toEqual([])
    const sentIds = fetchMock.mock.calls.map(([, init]) => JSON.parse(init!.body as string).id)
    expect(sentIds).toEqual(['a', 'b'])
  })

  it('marks an op the server refuses as failed and carries on with the rest', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    await enqueue({ id: 'bad', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'bad' }, label: 'bad' })
    await enqueue({ id: 'good', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'good' }, label: 'good' })

    fetchMock.mockReset()
    fetchMock
      .mockResolvedValueOnce(new Response(JSON.stringify({ errors: { Amount: ['Enter an amount above ₹0.'] } }), { status: 400 }))
      .mockResolvedValueOnce(new Response('{}', { status: 201 }))
    await flush(shopId)

    const left = await listOps(shopId)
    expect(left).toHaveLength(1)
    expect(left[0]).toMatchObject({ id: 'bad', status: 'failed', error: 'Enter an amount above ₹0.' })

    // Clean up: the failed op stays until discarded, so drop it directly.
    const { discardFailed } = await import('./outbox')
    await discardFailed(left[0].seq!)
  })

  it('treats a proxy saying the server is down (502/503/504) like being offline', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    await enqueue({ id: 'gw', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'gw' }, label: 'gw' })

    fetchMock.mockReset()
    fetchMock.mockResolvedValue(new Response('', { status: 502 }))
    await flush(shopId)

    expect(await listOps(shopId)).toMatchObject([{ id: 'gw', status: 'pending', attempts: 0 }])
  })

  it('stops on a server error and keeps the op to retry later', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    await enqueue({ id: 'x', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'x' }, label: 'x' })

    fetchMock.mockReset()
    fetchMock.mockResolvedValue(new Response('', { status: 500 }))
    await flush(shopId)

    expect(await listOps(shopId)).toMatchObject([{ id: 'x', status: 'pending', attempts: 1 }])
  })

  it("sends a second shop's writes too when both are flushed at once", async () => {
    fetchMock.mockImplementation(async () => new Response('{}', { status: 201 }))
    await enqueue({ id: 'one', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'one' }, label: 'one' })
    await enqueue({ id: 'two', shopId: 'shop-2', kind: 'khata', path: '/v1/khata-entries', body: { id: 'two' }, label: 'two' })

    await Promise.all([flush(shopId), flush('shop-2')])

    expect([...(await listOps(shopId)), ...(await listOps('shop-2'))]).toEqual([])
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it("never sends one shop's writes while signed in to another", async () => {
    fetchMock.mockImplementation(async () => new Response('{}', { status: 201 }))
    await enqueue({ id: 'held', shopId: 'shop-2', kind: 'khata', path: '/v1/khata-entries', body: { id: 'held' }, label: 'held' })
    setSession({ shopId, accessToken: 't', refreshToken: 'r' } as never)

    await flush('shop-2')

    expect(fetchMock).not.toHaveBeenCalled()
    expect((await listOps('shop-2')).map((o) => o.id)).toEqual(['held'])
    setSession(null)
    await flush('shop-2')
  })
})
