import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { RevenueGranularity, RevenueReport } from '../types'

/**
 * Doanh thu theo tiền thực thu (BR-CFG-02): thực thu, đã hoàn và thuần của từng ngày hoặc tháng.
 * `from`/`to` là ngày Việt Nam `YYYY-MM-DD`, gồm cả hai đầu.
 */
export function getRevenueReport(from: string, to: string, granularity: RevenueGranularity): Promise<RevenueReport> {
  return apiRequest<RevenueReport>(API_ENDPOINTS.admin.revenueReport, { query: { from, to, granularity } })
}
