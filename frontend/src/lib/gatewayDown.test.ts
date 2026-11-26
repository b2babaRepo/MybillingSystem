import { describe, expect, it } from 'vitest'
import { isGatewayDown } from './api'

const answer = (status: number, contentType: string | null) => ({ status, headers: new Headers(contentType ? { 'content-type': contentType } : {}) })

describe('isGatewayDown', () => {
  it('treats the proxy\'s own 502/503/504 pages as no connection', () => {
    expect(isGatewayDown(answer(502, 'text/html'))).toBe(true)
    expect(isGatewayDown(answer(503, 'text/html; charset=utf-8'))).toBe(true)
    expect(isGatewayDown(answer(504, null))).toBe(true)
  })

  it('shows the API\'s own 503 (SMS or AI switched off) instead of "No connection"', () => {
    expect(isGatewayDown(answer(503, 'application/problem+json'))).toBe(false)
    expect(isGatewayDown(answer(503, 'application/json; charset=utf-8'))).toBe(false)
  })

  it('leaves other answers alone', () => {
    expect(isGatewayDown(answer(200, 'application/json'))).toBe(false)
    expect(isGatewayDown(answer(500, 'text/html'))).toBe(false)
  })
})
