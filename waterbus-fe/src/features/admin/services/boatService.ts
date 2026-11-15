import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat, NewBoat } from '../types'

/** `POST /boats` chưa có thuyền trưởng, rồi đọc lại tàu vừa lưu. */
export async function createBoat(input: NewBoat): Promise<AdminBoat> {
  const id = await apiRequest<string>(API_ENDPOINTS.boats, { method: 'POST', body: { ...input, captainUserId: null } })
  return apiRequest<AdminBoat>(API_ENDPOINTS.boat(id))
}

/**
 * `PUT /boats/{id}`: đổi thông tin tàu. Mã tàu không đổi được. Đọc lại tàu hiện tại để giữ nguyên thuyền trưởng và
 * trạng thái hoạt động (backend ghi đè cả hai theo body).
 */
export async function updateBoat(id: string, input: Omit<NewBoat, 'code'>): Promise<void> {
  const current = await apiRequest<AdminBoat>(API_ENDPOINTS.boat(id))
  await apiRequest(API_ENDPOINTS.boat(id), {
    method: 'PUT',
    body: { ...input, isActive: current.isActive, captainUserId: current.captainUserId },
  })
}

/** Xoá mềm: backend không có DELETE nên tàu được ngừng hoạt động bằng `PUT`. */
export async function deleteBoat(id: string): Promise<void> {
  await setBoatActive(id, false)
}

/** Bật/tắt tàu bằng `PUT` (backend không có route activate/deactivate riêng cho tàu). */
export async function setBoatActive(id: string, active: boolean): Promise<void> {
  const boat = await apiRequest<AdminBoat>(API_ENDPOINTS.boat(id))
  await apiRequest(API_ENDPOINTS.boat(id), { method: 'PUT', body: { ...boat, isActive: active } })
}
