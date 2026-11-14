import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import type { RefundRequestResult } from '../types'
import RefundResultPanel from './RefundResultPanel'

const refund: RefundRequestResult = {
  refundTransactionId: 'r1', orderId: 'o1', bookingId: 'b1', amount: 15000, currency: 'VND', status: 'Succeeded',
  gateway: 'Manual', requiresManualAction: false, tickets: [], isDemo: true, notice: 'DEMO: giao dịch mô phỏng',
}

describe('RefundResultPanel', () => {
  afterEach(() => { cleanup(); setActiveLocale('en') })

  it('does not offer the confirmation form again once a demo refund has succeeded, but keeps the DEMO notice', () => {
    render(<RefundResultPanel refund={refund} />)
    expect(screen.queryByRole('button', { name: /Confirm/i })).toBeNull()
    expect(screen.getByText('DEMO: giao dịch mô phỏng')).toBeTruthy()
  })

  it('offers the confirmation form while manual action is still required', () => {
    render(<RefundResultPanel refund={{ ...refund, status: 'Processing', requiresManualAction: true }} />)
    expect(screen.getByRole('button', { name: 'Confirm demo refund' })).toBeTruthy()
  })
})
