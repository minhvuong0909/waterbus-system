import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat } from '../../operations/types'

export interface CaptainAttendance {
  tripId: string
  boatId: string | null
  captainId: string | null
  state: 'Pending' | 'Missing' | 'Present' | 'Late' | 'NotConfigured'
  requiredAt: string | null
  attendance: { recordedAt: string; isLate: boolean } | null
}

export const getCaptainAttendance = (tripId: string) =>
  apiRequest<CaptainAttendance>(API_ENDPOINTS.trips.captainAttendance(tripId))

export const getCaptainBoat = (boatId: string) => apiRequest<AdminBoat>(API_ENDPOINTS.boat(boatId))
