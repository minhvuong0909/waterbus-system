import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { EWALLETS, NEXT_STEPS, PAYMENT_METHODS } from '../../../mocks/payment'
import { withDelay } from '../../../mocks/delay'
import { apiRequest } from '../../../services/apiClient'
import type { Ewallet, NextStep, PaymentMethod, VnPayReturnResult } from '../types'

// Mock service layer: no real payment gateway. Everything resolves successfully.
export function getPaymentMethods(): Promise<PaymentMethod[]> {
  return withDelay(PAYMENT_METHODS)
}

export function getEwallets(): Promise<Ewallet[]> {
  return withDelay(EWALLETS)
}

export function getNextSteps(): Promise<NextStep[]> {
  return withDelay(NEXT_STEPS)
}

/** Pretends to check the payment. Always reports success so the flow never gets stuck. */
export function confirmPayment(): Promise<{ status: 'paid' }> {
  return withDelay({ status: 'paid' as const }, 400)
}

/**
 * `GET /payments/vnpay-return`: asks the backend to check the signature VNPay put on the return
 * URL. `search` is the query string of the callback page, passed on exactly as VNPay sent it.
 * The booking itself is confirmed by VNPay's server-to-server call (IPN), not by this check.
 */
export function verifyVnPayReturn(search: string): Promise<VnPayReturnResult> {
  const params = Object.fromEntries(new URLSearchParams(search))
  if (USE_MOCK) {
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
