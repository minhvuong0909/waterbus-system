import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { fetchAllPages } from '../../../services/paging'
import type { AdminBoat } from '../types'

/**
 * Đọc đủ các trang tàu; trang Admin truyền false để quản lý cả tàu ngừng hoạt động. Không gửi `isActiveOnly=false`:
 * backend hiểu là "chỉ tàu đã ngừng" (lọc `IsActive == false`), muốn lấy tất cả phải bỏ hẳn tham số.
 */
export const getBoats = (isActiveOnly = true): Promise<AdminBoat[]> =>
  fetchAllPages<AdminBoat>(API_ENDPOINTS.boats, isActiveOnly ? { isActiveOnly: true } : {})
