import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import {
  confirmManualRefund,
  confirmRefundsBatch,
  exportPendingRefundsCsv,
  listPendingRefunds,
  listRefundCandidates,
  reconcileRefund,
  requestRefund,
  requestRefundsForTrip,
} from './refundService'

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

  it('lists the refund candidates of a trip with paging', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ items: [], totalCount: 0 })
    await listRefundCandidates('t1', 2, 50)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.candidates, { query: { tripId: 't1', page: 2, pageSize: 50 } })
  })

  it('creates the refund requests of a trip, dropping a blank reason', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ requested: 2, skipped: 0 })
    await requestRefundsForTrip('t1', '  ')
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.batchRequest, { method: 'POST', body: { tripId: 't1', reason: undefined } })

    await requestRefundsForTrip('t1', ' Bão ')
    expect(apiRequest).toHaveBeenLastCalledWith(API_ENDPOINTS.refunds.batchRequest, { method: 'POST', body: { tripId: 't1', reason: 'Bão' } })
  })

  it('lists the pending refunds', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ items: [], totalCount: 0 })
    await listPendingRefunds()
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.pending, { query: { page: 1, pageSize: 100 } })
  })

  it('downloads the CSV as a file response', async () => {
    const file = { blob: new Blob(['x']), fileName: 'hoan-tien.csv', rowCount: 3 }
    vi.mocked(apiRequest).mockResolvedValue(file)

    await expect(exportPendingRefundsCsv('t1')).resolves.toBe(file)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.exportCsv, { method: 'POST', body: { tripId: 't1' }, responseType: 'download' })
  })

  it('confirms several refunds in one request and propagates errors', async () => {
    const items = [{ refundTransactionId: 'r1', evidence: 'e', gatewayReference: null, confirmedAmount: 10 }]
    vi.mocked(apiRequest).mockResolvedValueOnce({ confirmed: 1, failed: 0, items: [] })
    await confirmRefundsBatch(items)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.refunds.confirmManualBatch, { method: 'POST', body: { items } })

    vi.mocked(apiRequest).mockRejectedValueOnce(new Error('down'))
    await expect(confirmRefundsBatch(items)).rejects.toThrow('down')
  })
})
