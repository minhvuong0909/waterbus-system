import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { SeatClass, SeatClassInput } from '../types'
import { setActive } from './catalogRequests'

/** `GET /seat-classes`: every seat class, switched-off ones included unless `activeOnly`. */
export const getSeatClasses = (activeOnly = false) =>
  apiRequest<SeatClass[]>(API_ENDPOINTS.seatClasses, { query: { includeInactive: !activeOnly } })

export const createSeatClass = (input: SeatClassInput) =>
  apiRequest<string>(API_ENDPOINTS.seatClasses, { method: 'POST', body: input })

/** `PUT /seat-classes/{id}`: the code cannot change, fares and history refer to the class. */
export const updateSeatClass = (id: string, input: Omit<SeatClassInput, 'code'>) =>
  apiRequest<void>(API_ENDPOINTS.seatClass(id), { method: 'PUT', body: input })

export const setSeatClassActive = (id: string, active: boolean) =>
  setActive(API_ENDPOINTS.seatClass(id), active)
