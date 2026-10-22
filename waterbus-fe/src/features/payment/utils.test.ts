import { describe, expect, it } from 'vitest'
import { isPaidAfterExpiry, vnPayFailureMessage } from './utils'

describe('vnPayFailureMessage', () => {
  it('explains a cancelled payment', () => {
    expect(vnPayFailureMessage('24')).toBe('You cancelled the payment on the VNPay page.')
  })
  it('treats an unverified success code as an unverified result, not a success', () => {
    expect(vnPayFailureMessage('00')).toBe('We could not verify the result sent back by VNPay.')
  })
  it('falls back to a generic message for an unknown code', () => {
    expect(vnPayFailureMessage('42')).toBe('The payment was not completed.')
  })
})

describe('isPaidAfterExpiry', () => {
  const payment = (status: string, requiresReconciliation: boolean) => ({
    transactionId: 'p', type: 'Payment', status, amount: 15000, requiresReconciliation,
  })
  it('flags an expired order whose payment still succeeded', () => {
    expect(isPaidAfterExpiry({ status: 'Expired', payments: [payment('Succeeded', true)] })).toBe(true)
  })
  it('does not flag an expired order whose payment failed', () => {
    expect(isPaidAfterExpiry({ status: 'Expired', payments: [payment('Failed', false)] })).toBe(false)
  })
  it('does not flag a confirmed order', () => {
    expect(isPaidAfterExpiry({ status: 'Confirmed', payments: [payment('Succeeded', false)] })).toBe(false)
  })
})
