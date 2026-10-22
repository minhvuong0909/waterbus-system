import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getTicket, getTicketWallet } from './ticketService'

const fetchMock = vi.fn()
beforeEach(() => { window.sessionStorage.clear(); vi.stubGlobal('fetch', fetchMock) })
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals(); window.sessionStorage.clear() })

describe('live ticket wallet', () => {
  it('shows an empty wallet when no order is known instead of sample paid tickets', async () => {
    expect(await getTicketWallet()).toEqual({ next: undefined, later: [], past: [] })
    expect(fetchMock).not.toHaveBeenCalled()
  })
  it('does not create tickets or a QR for an unpaid order', async () => {
    window.sessionStorage.setItem('waterbus.booking', JSON.stringify({ order: { orderId: 'order-1', accessToken: 'guest-token' } }))
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ status: 'PendingPayment', bookings: [{ tickets: [] }] })))
    expect((await getTicketWallet()).next).toBeUndefined()
    expect(fetchMock).toHaveBeenCalledOnce()
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/checkouts\/order-1$/)
  })
})

describe('live ticket detail', () => {
  const stop = (callId: string, stationId: string, visitOrder: number) => ({
    callId, stationId, stationName: stationId, visitOrder, canBoard: true, canAlight: true, status: 'Planned',
    estimatedDepartureTime: '2026-10-23T01:00:00Z', estimatedArrivalTime: '2026-10-23T01:40:00Z',
  })
  const routes: [RegExp, unknown][] = [
    [/\/checkouts\/order-1\/bookings\/b1\/qr$/, { payload: 'QR-PAYLOAD', algorithm: 'RS256', keyId: 'k', credentialVersion: 1 }],
    [/\/checkouts\/order-1$/, { orderId: 'order-1', status: 'Confirmed', quotedTotal: 15000, expiresAt: null, payments: [],
      bookings: [{ bookingId: 'b1', bookingCode: 'WB1', publicBookingId: 'p1', tripId: 't1', boardingCallId: 'c1',
        disembarkingCallId: 'c2', status: 'Confirmed', quotedTotal: 15000,
        tickets: [{ ticketId: 'ticket-guid', ticketCode: 'TK1', passengerName: 'An', seatCode: 'S01', status: 'Valid',
          boardingStatus: 'NotBoarded', paidFare: 15000 }] }] }],
    [/\/trips\/t1\/seats/, { seats: [], availableSeatsCount: 0 }],
    [/\/trips\/t1$/, { tripId: 't1', routeId: 'r', routeName: 'Route', tripType: 'Commuter', status: 'Scheduled',
      departureTime: '2026-10-23T01:00:00Z', arrivalTime: '2026-10-23T01:45:00Z', canBook: true, salesState: 'open',
      boat: { boatId: 'bt', code: 'SWB-01', name: 'Boat' }, stops: [stop('c1', 'A', 1), stop('c2', 'B', 2)] }],
    [/\/stations/, [{ id: 'A', code: 'ST01', name: 'A', address: 'X, Q1', latitude: 0, longitude: 0, orderIndex: 1, isActive: true }, { id: 'B', code: 'ST02', name: 'B', address: 'Y, Q2', latitude: 0, longitude: 0, orderIndex: 2, isActive: true }]],
  ]

  it('falls back to the first issued ticket when the selected id is a session snapshot id', async () => {
    window.sessionStorage.setItem('waterbus.booking', JSON.stringify({ order: { orderId: 'order-1', accessToken: 'guest-token' } }))
    fetchMock.mockImplementation(async (url: string) => {
      const hit = routes.find(([pattern]) => pattern.test(String(url)))
      return new Response(JSON.stringify(hit ? hit[1] : {}), { status: hit ? 200 : 404 })
    })
    const ticket = await getTicket('tkt-snapshot-id')
    expect(ticket).toMatchObject({ id: 'ticket-guid', seat: 'S01', qrPayload: 'QR-PAYLOAD' })
  })

  it('still reports a missing ticket when the session has no order', async () => {
    await expect(getTicket('anything')).rejects.toMatchObject({ status: 404 })
  })
})
