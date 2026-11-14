import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getManageBooking, getTicket, getTicketWallet, hasOrderToManage } from './ticketService'
import { loadSavedOrders, saveOrder } from '../orderStorage'

const fetchMock = vi.fn()
beforeEach(() => { window.sessionStorage.clear(); window.localStorage.clear(); vi.stubGlobal('fetch', fetchMock) })
afterEach(() => {
  fetchMock.mockReset(); vi.unstubAllGlobals(); window.sessionStorage.clear(); window.localStorage.clear()
  window.history.replaceState(null, '', '/')
})

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

  const serve = () =>
    fetchMock.mockImplementation(async (url: string) => {
      if (/\/checkouts\/gone-order$/.test(String(url))) return new Response('{}', { status: 403 })
      const hit = routes.find(([pattern]) => pattern.test(String(url)))
      return new Response(JSON.stringify(hit ? hit[1] : {}), { status: hit ? 200 : 404 })
    })

  it('remembers a paid session order so My Tickets keeps it after the tab closes', async () => {
    window.sessionStorage.setItem('waterbus.booking', JSON.stringify({ order: { orderId: 'order-1', accessToken: 'guest-token' } }))
    serve()
    const wallet = await getTicketWallet()
    expect(wallet.next).toMatchObject({ id: 'ticket-guid', manageLink: '/manage/guest-token?orderId=order-1' })
    expect(loadSavedOrders().map((x) => x.orderId)).toEqual(['order-1'])

    window.sessionStorage.clear()
    expect((await getTicketWallet()).next?.id).toBe('ticket-guid')
  })

  it('drops an order the backend no longer opens instead of failing the whole wallet', async () => {
    saveOrder('order-1', 'guest-token')
    saveOrder('gone-order', 'old-token')
    serve()
    expect((await getTicketWallet()).next?.id).toBe('ticket-guid')
    expect(loadSavedOrders().map((x) => x.orderId)).toEqual(['order-1'])
  })

  it('opens the order behind a secure link and needs its order id', async () => {
    serve()
    expect(hasOrderToManage('guest-token')).toBe(false)
    await expect(getManageBooking('guest-token')).rejects.toMatchObject({ status: 404 })

    window.history.replaceState(null, '', '/manage/guest-token?orderId=order-1')
    expect(hasOrderToManage('guest-token')).toBe(true)
    const manage = await getManageBooking('guest-token')
    expect(manage).toMatchObject({ bookings: [{ bookingCode: 'WB1' }], orderId: 'order-1' })
    expect(manage.manageUrl).toMatch(/\/manage\/guest-token\?orderId=order-1$/)
    expect(fetchMock.mock.calls.some(([, init]) => (init as RequestInit)?.headers && JSON.stringify((init as RequestInit).headers).includes('guest-token'))).toBe(true)
  })

  it('lists every booking of a round-trip order in travel order, with all its tickets', async () => {
    const ticketOf = (id: string, seatCode: string) => ({
      ticketId: id, ticketCode: id, passengerName: 'An', seatCode, status: 'Valid', boardingStatus: 'NotBoarded', paidFare: 15000,
    })
    const booking = (bookingId: string, bookingCode: string, tripId: string, tickets: unknown[]) => ({
      bookingId, bookingCode, publicBookingId: bookingId, tripId, boardingCallId: 'c1', disembarkingCallId: 'c2',
      status: 'Confirmed', quotedTotal: 15000 * tickets.length, tickets,
    })
    const [, trip] = routes.find(([pattern]) => pattern.test('/trips/t1'))!
    const roundTrip: [RegExp, unknown][] = [
      [/\/checkouts\/order-rt\/bookings\/[\w-]+\/qr$/, { payload: 'QR', algorithm: 'RS256', keyId: 'k', credentialVersion: 1 }],
      // The backend lists the return leg first here; the page must still show the outbound one first.
      [/\/checkouts\/order-rt$/, { orderId: 'order-rt', status: 'Confirmed', quotedTotal: 45000, expiresAt: null, payments: [],
        bookings: [
          booking('b-back', 'WB-BACK', 't2', [ticketOf('t-3', 'S03')]),
          booking('b-out', 'WB-OUT', 't1', [ticketOf('t-1', 'S01'), ticketOf('t-2', 'S02')]),
        ] }],
      [/\/trips\/t2\/seats/, { seats: [], availableSeatsCount: 0 }],
      [/\/trips\/t2$/, { ...(trip as object), tripId: 't2', departureTime: '2026-10-23T09:00:00Z', arrivalTime: '2026-10-23T09:45:00Z',
        stops: [stop('c1', 'A', 1), stop('c2', 'B', 2)].map((s) => ({
          ...s, estimatedDepartureTime: '2026-10-23T09:00:00Z', estimatedArrivalTime: '2026-10-23T09:40:00Z',
        })) }],
    ]
    fetchMock.mockImplementation(async (url: string) => {
      const hit = [...roundTrip, ...routes].find(([pattern]) => pattern.test(String(url)))
      return new Response(JSON.stringify(hit ? hit[1] : {}), { status: hit ? 200 : 404 })
    })
    window.history.replaceState(null, '', '/manage/guest-token?orderId=order-rt')

    const { bookings } = await getManageBooking('guest-token')
    expect(bookings.map((b) => [b.bookingId, b.bookingCode, b.tickets.map((t) => t.seat)])).toEqual([
      ['b-out', 'WB-OUT', ['S01', 'S02']],
      ['b-back', 'WB-BACK', ['S03']],
    ])
  })

  const signIn = (role: string) =>
    window.localStorage.setItem('waterbus.session', JSON.stringify({ token: '', user: { id: 'u1', fullName: 'An', email: 'an@example.test', roles: [role] } }))
  const accountOrders = { items: [
    { orderId: 'order-1', bookings: [{ ticketCount: 1 }] },
    { orderId: 'unpaid-order', bookings: [{ ticketCount: 0 }] },
  ] }
  const serveAccount = () =>
    fetchMock.mockImplementation(async (url: string) => {
      if (/\/me\/orders/.test(String(url))) return new Response(JSON.stringify(accountOrders))
      const hit = routes.find(([pattern]) => pattern.test(String(url)))
      return new Response(JSON.stringify(hit ? hit[1] : {}), { status: hit ? 200 : 404 })
    })
  const sentGuestToken = () =>
    fetchMock.mock.calls.some(([, init]) => JSON.stringify((init as RequestInit)?.headers ?? {}).includes('X-Guest-Token'))

  it('shows a passenger the tickets of the account without any guest token', async () => {
    signIn('Passenger')
    serveAccount()
    const wallet = await getTicketWallet()
    expect(wallet.next).toMatchObject({ id: 'ticket-guid', bookingId: 'b1', manageLink: '/tickets/manage?orderId=order-1' })
    expect(sentGuestToken()).toBe(false)
    expect(fetchMock.mock.calls.some(([url]) => /unpaid-order/.test(String(url)))).toBe(false)
    expect(loadSavedOrders()).toEqual([])
  })

  it('does not ask for account orders unless a passenger is signed in', async () => {
    signIn('Admin')
    serveAccount()
    await getTicketWallet()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('opens an order of the account by its id only for a signed-in passenger', async () => {
    window.history.replaceState(null, '', '/tickets/manage?orderId=order-1')
    expect(hasOrderToManage()).toBe(false)

    signIn('Passenger')
    serveAccount()
    expect(hasOrderToManage()).toBe(true)
    expect(await getManageBooking()).toMatchObject({ orderId: 'order-1', accessToken: null, manageUrl: undefined })
  })

  it('uses the token saved on the device for the order of this tab after its link was replaced', async () => {
    window.sessionStorage.setItem('waterbus.booking', JSON.stringify({ order: { orderId: 'order-1', accessToken: 'old-token' } }))
    saveOrder('order-1', 'new-token')
    serve()
    expect((await getManageBooking()).accessToken).toBe('new-token')
  })
})
