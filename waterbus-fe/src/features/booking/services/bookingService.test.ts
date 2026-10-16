import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { buildCheckoutRequest } from '../utils'
import { createCheckout, getCheckout } from './bookingService'

const GUID = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`
const fetchMock = vi.fn()

const request = (seatIds: string[], tripId = GUID(1)) =>
  buildCheckoutRequest({
    tripId,
    originId: GUID(2),
    destinationId: GUID(3),
    seatIds,
    purchaser: { fullName: 'An', phoneCode: '+84', phone: '', email: 'an@example.com' },
    passengers: Object.fromEntries(seatIds.map((id) => [id, { fullName: 'P', email: '', phone: '' }])),
  })

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

describe('createCheckout', () => {
  it('posts to /checkouts when trip, stations and seats are backend ids', async () => {
    const body = { orderId: GUID(9), quotedTotal: 1, accessToken: 'tok', bookings: [] }
    fetchMock.mockResolvedValue(new Response(JSON.stringify(body), { status: 201 }))
    const result = await createCheckout(request([GUID(4)]), 1)
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/api\/v1\/checkouts$/)
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body).legs[0].seatIds).toEqual([GUID(4)])
    expect(result.accessToken).toBe('tok')
  })

  it('returns a mock order (no request) while seats are mock codes', async () => {
    const result = await createCheckout(request(['S01']), 250000)
    expect(fetchMock).not.toHaveBeenCalled()
    expect(result.accessToken).toBeNull()
    expect(result.quotedTotal).toBe(250000)
    expect(Date.parse(result.expiresAt ?? '')).toBeGreaterThan(Date.now())
  })
})

describe('getCheckout', () => {
  it('sends the guest token and returns the backend status', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ orderId: GUID(9), status: 'PendingPayment' })))
    const detail = await getCheckout(GUID(9), 'tok')
    expect(fetchMock.mock.calls[0][1].headers['X-Guest-Token']).toBe('tok')
    expect(detail.status).toBe('PendingPayment')
  })
})
