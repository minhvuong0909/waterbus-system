import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { Profile } from '../types'

/** `GET /me/profile`: the signed-in customer's own details. */
export function getProfile(): Promise<Profile> {
  return apiRequest<Profile>(API_ENDPOINTS.me.profile)
}

/** `PUT /me/profile`: name and phone. The email is not editable here. */
export function updateProfile(fullName: string, phoneNumber: string): Promise<Profile> {
  return apiRequest<Profile>(API_ENDPOINTS.me.profile, {
    method: 'PUT',
    body: { fullName: fullName.trim(), phoneNumber: phoneNumber.trim() || null },
  })
}
