import { afterEach, describe, expect, it, vi } from 'vitest'
import { createPaymentUrl, verifyVnPayReturn } from './paymentService'

const fetchMock = vi.fn()
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })
describe('live payment requests', () => {
  it('requests the gateway URL from the backend when a signed-in order has no guest token', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ paymentUrl: 'https://sandbox.example/payment' })))
    expect(await createPaymentUrl('order-id', null)).toEqual({ url: 'https://sandbox.example/payment', expiresAt: null })
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/v1\/payments\/create-vnpay-url$/)
  })
  it('returns the payment deadline the backend set on the order', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify({
      paymentUrl: 'https://sandbox.example/payment', expiresAt: '2026-10-22T06:30:00+00:00' })))
    expect((await createPaymentUrl('order-id', 'token')).expiresAt).toBe(Date.parse('2026-10-22T06:30:00Z'))
  })
  it('does not trust a mock success query parameter in live mode', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ success: false, responseCode: '97' })))
    expect((await verifyVnPayReturn('?mock=1&vnp_ResponseCode=00')).success).toBe(false)
    expect(fetchMock).toHaveBeenCalledOnce()
  })
})
