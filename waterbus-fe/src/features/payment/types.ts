/** Response of `GET /api/v1/payments/vnpay-return`. */
export interface VnPayReturnResult {
  /** True when the signature is valid and VNPay reports success (`vnp_ResponseCode` = "00"). */
  success: boolean
  /** VNPay `vnp_TxnRef`: the reference of the payment attempt, not a booking code. */
  merchantReference: string
  /** Alias of `merchantReference` kept by the backend for older clients. */
  bookingCode: string
  responseCode: string
  message: string
}

export interface NextStep {
  number: string
  title: string
  description: string
  chipIcon: string
  chipLabel: string
}
