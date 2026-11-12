import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import type { RefundRequestResult } from '../types'
import ManualRefundForm from './ManualRefundForm'

const refund: RefundRequestResult = {
  refundTransactionId: 'r1', orderId: 'o1', bookingId: 'b1', amount: 15000, currency: 'VND', status: 'Processing',
  gateway: 'Manual', requiresManualAction: true, tickets: [],
}

describe('ManualRefundForm', () => {
  afterEach(() => { cleanup(); setActiveLocale('en') })

  it('tells the Admin NOT to transfer real money for a demo refund', () => {
    render(<ManualRefundForm refund={{ ...refund, isDemo: true, notice: 'DEMO: giao dịch mô phỏng' }} />)
    expect(screen.getByText('DEMO: giao dịch mô phỏng')).toBeTruthy()
    expect(screen.getByText(/do NOT transfer real money/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Confirm demo refund' })).toBeTruthy()
    expect(screen.queryByText(/merchant portal/i)).toBeNull()
  })

  it('keeps the real manual-transfer wording for a non-demo refund', () => {
    render(<ManualRefundForm refund={refund} />)
    expect(screen.getByText(/VNPay merchant portal/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Confirm manual refund' })).toBeTruthy()
    expect(screen.queryByText(/do NOT transfer real money/i)).toBeNull()
  })
})
