import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { guestHeaders } from '../../booking/services/bookingService'
import type { VnPayReturnResult } from '../types'

interface VnPayUrlResponse {
  paymentUrl: string
  orderId: string
  merchantReference: string
  amount: number
  expiresAt: string
}

/** Where to send the traveler to pay, and until when the order waits for the payment (epoch ms). */
export interface PaymentRedirect {
  url: string
  expiresAt: number | null
}

/**
 * `POST /payments/create-vnpay-url` (BR-PAY-04: pay per order). Returns the VNPay URL to send the
 * traveler to and the payment deadline the backend set on the order. Paying is confirmed by the
 * backend from VNPay's IPN, never by this response.
 */
export async function createPaymentUrl(
  orderId: string,
  accessToken: string | null,
): Promise<PaymentRedirect> {
  const response = await apiRequest<VnPayUrlResponse>(API_ENDPOINTS.payments.createVnPayUrl, {
    method: 'POST',
    body: { orderId },
    headers: guestHeaders(accessToken),
  })
  const expiresAt = response.expiresAt ? Date.parse(response.expiresAt) : NaN
  return { url: response.paymentUrl, expiresAt: Number.isNaN(expiresAt) ? null : expiresAt }
}

/**
 * `GET /payments/vnpay-return`: asks the backend to check the signature VNPay put on the return
 * URL. `search` is the query string of the callback page, passed on exactly as VNPay sent it.
 * It only tells what the browser was redirected with; the order status (IPN) is what counts.
 */
export function verifyVnPayReturn(search: string): Promise<VnPayReturnResult> {
  const params = Object.fromEntries(new URLSearchParams(search))
  return apiRequest<VnPayReturnResult>(API_ENDPOINTS.payments.vnPayReturn, { query: params })
}

/**
 * `POST /payments/{orderId}/attempts/{paymentId}/reconcile`: asks the backend to query VNPay
 * itself when the IPN has not arrived. The order status is read again afterwards.
 */
export async function reconcilePayment(
  orderId: string,
  paymentId: string,
  accessToken: string | null,
): Promise<void> {
  await apiRequest<unknown>(API_ENDPOINTS.payments.reconcile(orderId, paymentId), {
    method: 'POST',
    headers: guestHeaders(accessToken),
  })
}
