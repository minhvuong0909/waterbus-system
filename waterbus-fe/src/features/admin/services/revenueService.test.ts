import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getRevenueReport } from './revenueService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('revenueService', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('asks for the revenue of a Vietnam date range at the chosen granularity', async () => {
    const report = { from: '2026-11-01', to: '2026-11-30', granularity: 'Day', totals: {}, points: [] }
    vi.mocked(apiRequest).mockResolvedValue(report)

    await expect(getRevenueReport('2026-11-01', '2026-11-30', 'Day')).resolves.toBe(report)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.admin.revenueReport, {
      query: { from: '2026-11-01', to: '2026-11-30', granularity: 'Day' },
    })
  })
})
