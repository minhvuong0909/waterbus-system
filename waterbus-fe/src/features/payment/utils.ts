import type { CheckoutDetail } from '../booking/types'

/** What `vnp_ResponseCode` means for the traveler (VNPay 2.1.0 return/IPN codes). English keys, translated by `t`. */
const VNPAY_RESPONSE_MESSAGES: Record<string, string> = {
  '07': 'The bank flagged this transaction for review. If money was taken, we will confirm it with the bank.',
  '09': 'Your card or account is not registered for internet banking.',
  '10': 'The card or account details were entered incorrectly too many times.',
  '11': 'The payment window expired before the payment was completed.',
  '12': 'Your card or account is locked.',
  '13': 'The one-time password (OTP) was incorrect.',
  '24': 'You cancelled the payment on the VNPay page.',
  '51': 'Your account does not have enough balance for this payment.',
  '65': 'Your account has passed its daily transaction limit.',
  '75': 'The bank is under maintenance. Please try again later.',
  '79': 'The payment password was entered incorrectly too many times.',
}

/**
 * Message shown when VNPay sends the traveler back without a successful payment. The backend's own
 * `message` is meant for developers, so it is never shown. `success` false with code "00" means the
 * redirect could not be verified (bad signature): the order status still decides.
 */
export function vnPayFailureMessage(responseCode: string): string {
  if (responseCode === '00') return 'We could not verify the result sent back by VNPay.'
  return VNPAY_RESPONSE_MESSAGES[responseCode] ?? 'The payment was not completed.'
}

/**
 * True when the order is closed but a payment was still taken for it (BE `RequiresReconciliation`):
 * the money arrived after the hold expired, so no ticket was issued and an admin must refund it.
 */
export function isPaidAfterExpiry(detail: Pick<CheckoutDetail, 'status' | 'payments'>): boolean {
  return (
    (detail.status === 'Expired' || detail.status === 'Cancelled') &&
    detail.payments.some((p) => p.type === 'Payment' && p.status === 'Succeeded' && p.requiresReconciliation)
  )
}
