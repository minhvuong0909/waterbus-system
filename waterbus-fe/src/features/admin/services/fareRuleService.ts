import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { FareRule, FareRuleInput } from '../types'
import { fetchAllPages } from '../../../services/paging'
import { setActive } from './catalogRequests'

/** `GET /admin/fares`: mọi quy tắc giá, kể cả giá đã qua và giá đã tắt. */
export const getFareRules = () => fetchAllPages<FareRule>(API_ENDPOINTS.admin.fares)

/** `POST /admin/fares`: giá chỉ tính bằng VND (BR-FARE-01). */
export const createFareRule = (input: FareRuleInput) =>
  apiRequest<string>(API_ENDPOINTS.admin.fares, { method: 'POST', body: { ...input, currency: 'VND' } })

/**
 * `PUT /admin/fares/{id}`. Giá đã có hiệu lực chỉ nhận ngày kết thúc sớm hơn; giá mới cần một quy tắc giá mới (nếu
 * không sẽ 409).
 */
export const updateFareRule = (
  id: string,
  input: Pick<FareRuleInput, 'price' | 'effectiveFrom' | 'effectiveTo'>,
) => apiRequest<void>(API_ENDPOINTS.admin.fare(id), { method: 'PUT', body: input })

/** Quy tắc giá tắt được nhưng không bật lại được. */
export const deactivateFareRule = (id: string) => setActive(API_ENDPOINTS.admin.fare(id), false)
