import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { pressKey } from './keypad'
import { uuidv7 } from './ids'
import { mergeCustomers, mergeStatement } from './local'
import { enqueue, flush, listOps, type OutboxOp } from './outbox'
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

  it('stops on a server error and keeps the op to retry later', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))
    await enqueue({ id: 'x', shopId, kind: 'khata', path: '/v1/khata-entries', body: { id: 'x' }, label: 'x' })

    fetchMock.mockReset()
    fetchMock.mockResolvedValue(new Response('', { status: 503 }))
    await flush(shopId)

    expect(await listOps(shopId)).toMatchObject([{ id: 'x', status: 'pending', attempts: 1 }])
  })
})
