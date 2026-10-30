import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { buildHoldRequest } from '../utils'
import { createHold, getBookingQr, getCheckout, getSeatMap, submitCheckout } from './bookingService'

const GUID = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, '0')}`
const fetchMock = vi.fn()

const holdRequest = (seatIds: string[], tripId = GUID(1)) =>
  buildHoldRequest({ tripId, originId: GUID(2), destinationId: GUID(3), seatIds })

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const stop = (callId: string, stationId: string, visitOrder: number) => ({
  callId,
  stationId,
  stationName: stationId,
  visitOrder,
  canBoard: true,
  canAlight: true,
  status: 'Scheduled',
})

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

describe('getSeatMap', () => {
  it('reads the trip stops, then the seats of that segment', async () => {
    fetchMock
      .mockResolvedValueOnce(
        json({
          tripId: GUID(1),
          canBook: true,
          salesState: 'open',
          boat: { boatId: GUID(8), code: 'SWB-01', name: 'Boat' },
          stops: [stop(GUID(11), GUID(2), 1), stop(GUID(12), GUID(3), 2)],
        }),
      )
      .mockResolvedValueOnce(
        json({
          tripId: GUID(1),
          boatId: GUID(8),
          boardingCallId: GUID(11),
          disembarkingCallId: GUID(12),
          boardingVisitOrder: 1,
          disembarkingVisitOrder: 2,
          canBook: true,
          salesState: 'open',
          availableSeatsCount: 0,
          asOf: '',
          seats: [],
        }),
      )
    const map = await getSeatMap(GUID(1), GUID(2), GUID(3))
    expect(fetchMock.mock.calls[1][0]).toContain(
      `boardingCallId=${GUID(11)}&disembarkingCallId=${GUID(12)}`,
    )
    expect(map.journey?.boardingCallId).toBe(GUID(11))
  })

  it('rejects a trip that does not stop at both piers', async () => {
    fetchMock.mockResolvedValueOnce(json({ tripId: GUID(1), boat: null, stops: [] }))
    await expect(getSeatMap(GUID(1), GUID(2), GUID(3))).rejects.toMatchObject({ status: 409 })
  })

  it('rejects demo identifiers in live mode instead of displaying a fake seat map', async () => {
    await expect(getSeatMap('trip-1', 'bach-dang', 'linh-dong')).rejects.toMatchObject({ status: 400 })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('createHold', () => {
  it('posts to /checkouts/holds for backend ids', async () => {
    fetchMock.mockResolvedValue(
      json({ orderId: GUID(9), quotedTotal: 1, accessToken: 'tok', bookings: [] }, 201),
    )
    const result = await createHold(holdRequest([GUID(4)]))
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toMatch(/\/api\/v1\/checkouts\/holds$/)
    expect(init.method).toBe('POST')
    expect(result.accessToken).toBe('tok')
  })

  it('rejects demo trips in live mode instead of reporting a fake order', async () => {
    await expect(createHold(holdRequest(['S01'], 'trip-1'))).rejects.toMatchObject({ status: 400 })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('submitCheckout, getCheckout and getBookingQr', () => {
  it('reads an authenticated order without a guest token from the backend', async () => {
    fetchMock.mockResolvedValue(json({ orderId: GUID(9), status: 'PendingPayment', bookings: [], payments: [] }))
    expect((await getCheckout(GUID(9), null)).status).toBe('PendingPayment')
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(fetchMock.mock.calls[0][1].headers['X-Guest-Token']).toBeUndefined()
  })
  it('fetches real signed QR credentials without a guest token', async () => {
    fetchMock.mockResolvedValue(json({ payload: 'signed.qr.value', algorithm: 'RS256' }))
    expect((await getBookingQr(GUID(9), GUID(8), null)).payload).toBe('signed.qr.value')
    expect(fetchMock).toHaveBeenCalledOnce()
  })
  it('submit posts the buyer with the guest token', async () => {
    fetchMock.mockResolvedValue(json(true))
    await submitCheckout(GUID(9), 'tok', {
      customerName: 'A',
      customerEmail: 'a@x.vn',
      passengers: [],
    })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toMatch(new RegExp(`/api/v1/checkouts/${GUID(9)}/submit$`))
    expect(init.headers['X-Guest-Token']).toBe('tok')
  })

  it('reads the order status with the guest token', async () => {
    fetchMock.mockResolvedValue(json({ orderId: GUID(9), status: 'PendingPayment' }))
    const detail = await getCheckout(GUID(9), 'tok')
    expect(fetchMock.mock.calls[0][1].headers['X-Guest-Token']).toBe('tok')
    expect(detail.status).toBe('PendingPayment')
  })

  it('reads the booking QR credential', async () => {
    fetchMock.mockResolvedValue(
      json({ payload: 'a.b.c', algorithm: 'RS256', keyId: 'k', credentialVersion: 1 }),
    )
    const qr = await getBookingQr(GUID(9), GUID(5), 'tok')
    expect(fetchMock.mock.calls[0][0]).toMatch(
      new RegExp(`/checkouts/${GUID(9)}/bookings/${GUID(5)}/qr$`),
    )
    expect(qr.payload).toBe('a.b.c')
  })
})
