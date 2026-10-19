import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getTicketWallet } from './ticketService'

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
