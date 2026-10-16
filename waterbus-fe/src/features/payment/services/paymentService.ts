import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { EWALLETS, NEXT_STEPS, PAYMENT_METHODS } from '../../../mocks/payment'
import { withDelay } from '../../../mocks/delay'
import { ROUTES } from '../../../routes/routes'
import { apiRequest } from '../../../services/apiClient'
import { guestHeaders } from '../../booking/services/bookingService'
import type { Ewallet, NextStep, PaymentMethod, VnPayReturnResult } from '../types'

export function getPaymentMethods(): Promise<PaymentMethod[]> {
  return withDelay(PAYMENT_METHODS)
}

export function getEwallets(): Promise<Ewallet[]> {
  return withDelay(EWALLETS)
}

export function getNextSteps(): Promise<NextStep[]> {
  return withDelay(NEXT_STEPS)
}

interface VnPayUrlResponse {
  paymentUrl: string
  orderId: string
  merchantReference: string
  amount: number
  expiresAt: string
}

/**
 * `POST /payments/create-vnpay-url` (BR-PAY-04: pay per order). Returns the VNPay URL to send the
 * traveler to. Paying is confirmed by the backend from VNPay's IPN, never by this response.
 *
 * MOCK (a mock order has no token): the "gateway" is the app's own callback page, reporting success.
 */
export async function createPaymentUrl(
  orderId: string,
  accessToken: string | null,
): Promise<string> {
  if (!accessToken) {
    const query = new URLSearchParams({ vnp_ResponseCode: '00', vnp_TxnRef: 'MOCK', mock: '1' })
    return `${ROUTES.paymentCallback}?${query.toString()}`
  }
  const response = await apiRequest<VnPayUrlResponse>(API_ENDPOINTS.payments.createVnPayUrl, {
    method: 'POST',
    body: { orderId },
    headers: guestHeaders(accessToken),
  })
  return response.paymentUrl
}

/**
 * `GET /payments/vnpay-return`: asks the backend to check the signature VNPay put on the return
 * URL. `search` is the query string of the callback page, passed on exactly as VNPay sent it.
 * It only tells what the browser was redirected with; the order status (IPN) is what counts.
 */
export function verifyVnPayReturn(search: string): Promise<VnPayReturnResult> {
  const params = Object.fromEntries(new URLSearchParams(search))
  if (USE_MOCK || params.mock === '1') {
    const success = params.vnp_ResponseCode === '00'
    return withDelay({
      success,
      bookingCode: params.vnp_TxnRef ?? '',
      responseCode: params.vnp_ResponseCode ?? '',
      message: success ? 'Thanh toán thành công' : 'Giao dịch không thành công hoặc bị hủy',
    })
  }
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
