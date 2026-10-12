import { EWALLETS, NEXT_STEPS, PAYMENT_METHODS } from '../../../mocks/payment'
import { withDelay } from '../../../mocks/delay'
import type { Ewallet, NextStep, PaymentMethod } from '../types'

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
