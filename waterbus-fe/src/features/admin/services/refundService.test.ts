import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { confirmManualRefund, reconcileRefund, requestRefund } from './refundService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('refundService', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('requests a refund for one booking with the reason', async () => {
    const result = { refundTransactionId: 'r1', requiresManualAction: false }
    vi.mocked(apiRequest).mockResolvedValue(result)

    await expect(requestRefund('b1', 'Trip cancelled')).resolves.toBe(result)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.request, {
      method: 'POST',
      body: { bookingId: 'b1', reason: 'Trip cancelled' },
    })
  })

  it('sends a manual confirmation with its evidence', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ status: 'Succeeded' })
    const input = { refundTransactionId: 'r1', evidence: 'Bank slip 42', gatewayReference: null, confirmedAmount: 0 }

    await confirmManualRefund(input)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.confirmManual, { method: 'POST', body: input })
  })

  it('returns only the status of a reconciliation and propagates errors', async () => {
    vi.mocked(apiRequest).mockResolvedValueOnce({ status: 'Pending' })
    await expect(reconcileRefund('r1')).resolves.toBe('Pending')
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.reconcile('r1'), { method: 'POST' })

    vi.mocked(apiRequest).mockRejectedValueOnce(new Error('down'))
    await expect(reconcileRefund('r1')).rejects.toThrow('down')
  })
})
