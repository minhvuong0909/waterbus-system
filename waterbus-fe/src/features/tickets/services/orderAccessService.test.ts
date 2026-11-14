import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  claimOrder,
  createTripShareLink,
  getAccountOrders,
  getSharedTrip,
  revokeOrderLinks,
  rotateOrderLink,
} from './orderAccessService'

const fetchMock = vi.fn()
const answer = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
const lastCall = () => {
  const [url, init] = fetchMock.mock.calls[fetchMock.mock.calls.length - 1] as [string, RequestInit]
  return { url, method: init.method, headers: init.headers as Record<string, string> }
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

describe('order access', () => {
  it('claims an order with the guest token of that order', async () => {
    fetchMock.mockResolvedValue(answer({ orderId: 'o1', alreadyLinked: false }))
    expect(await claimOrder('o1', 'guest-token')).toMatchObject({ alreadyLinked: false })
    expect(lastCall()).toMatchObject({ method: 'POST', headers: { 'X-Guest-Token': 'guest-token' } })
    expect(lastCall().url).toMatch(/\/checkouts\/o1\/claim$/)
  })

  it('reports an order that already belongs to another account', async () => {
    fetchMock.mockResolvedValue(answer({ title: 'Conflict' }, 409))
    await expect(claimOrder('o1', 'guest-token')).rejects.toMatchObject({ status: 409 })
  })

  it('lets the signed-in owner rotate and revoke without a guest token', async () => {
    fetchMock.mockResolvedValue(answer({ accessToken: 'new-token', expiresAt: '2026-12-01T00:00:00Z' }))
    expect((await rotateOrderLink('o1', null)).accessToken).toBe('new-token')
    expect(lastCall().url).toMatch(/\/checkouts\/o1\/access\/rotate$/)
    expect(lastCall().headers).not.toHaveProperty('X-Guest-Token')

    fetchMock.mockResolvedValue(answer({ revokedCount: 2 }))
    expect(await revokeOrderLinks('o1', null)).toEqual({ revokedCount: 2 })
    expect(lastCall().url).toMatch(/\/checkouts\/o1\/access\/revoke$/)
  })

  it('issues a share link for one booking of the order', async () => {
    fetchMock.mockResolvedValue(answer({ accessToken: 'trip-token', expiresAt: '2026-12-01T00:00:00Z' }))
    await createTripShareLink('o1', 'b1', 'guest-token')
    expect(lastCall().url).toMatch(/\/checkouts\/o1\/bookings\/b1\/trip-access$/)
    expect(lastCall()).toMatchObject({ method: 'POST', headers: { 'X-Guest-Token': 'guest-token' } })
  })

  it('reads a shared journey with the token in a header, never in the URL', async () => {
    fetchMock.mockResolvedValue(answer({ bookingCode: 'WB1', tickets: [] }))
    await getSharedTrip('trip-token')
    expect(lastCall().url).toMatch(/\/tripaccess$/)
    expect(lastCall().headers).toMatchObject({ 'X-Trip-Access-Token': 'trip-token' })
  })

  it('pages the orders of the account', async () => {
    fetchMock.mockResolvedValue(answer({ items: [], pageNumber: 2, totalPages: 2, totalCount: 12 }))
    expect((await getAccountOrders(2)).pageNumber).toBe(2)
    expect(lastCall().url).toMatch(/\/me\/orders\?pageNumber=2&pageSize=10$/)
  })
})
