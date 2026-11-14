import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { FareRule, FareRuleInput } from '../types'
import { fetchAllPages, setActive } from './catalogRequests'

/** `GET /admin/fares`: every fare rule, past and switched-off ones included. */
export const getFareRules = () => fetchAllPages<FareRule>(API_ENDPOINTS.admin.fares)

/** `POST /admin/fares`: fares are in VND only (BR-FARE-01). */
export const createFareRule = (input: FareRuleInput) =>
  apiRequest<string>(API_ENDPOINTS.admin.fares, { method: 'POST', body: { ...input, currency: 'VND' } })

/**
 * `PUT /admin/fares/{id}`. A fare that has already taken effect only accepts an earlier end; a new
 * price needs a new fare rule (409 otherwise).
 */
export const updateFareRule = (
  id: string,
  input: Pick<FareRuleInput, 'price' | 'effectiveFrom' | 'effectiveTo'>,
) => apiRequest<void>(API_ENDPOINTS.admin.fare(id), { method: 'PUT', body: input })

/** A fare rule can be switched off but not back on. */
export const deactivateFareRule = (id: string) => setActive(API_ENDPOINTS.admin.fare(id), false)
