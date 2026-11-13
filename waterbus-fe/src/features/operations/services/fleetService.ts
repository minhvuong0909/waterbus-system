import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { AdminBoat } from '../types'

const PAGE_SIZE = 100

/**
 * Đọc đủ các trang tàu. `isActiveOnly` theo đúng hợp đồng BE (`bool?`): bỏ qua (undefined) trả về
 * TẤT CẢ, `true` chỉ tàu đang hoạt động, `false` chỉ tàu đã ngừng - `false` KHÔNG có nghĩa "cả hai".
 * Trang Admin cần quản lý cả tàu ngừng hoạt động nên gọi không truyền gì (lấy tất cả).
 */
export async function getBoats(isActiveOnly?: boolean): Promise<AdminBoat[]> {
  const boats: AdminBoat[] = []
  for (let pageNumber = 1; ; pageNumber++) {
    const page = await apiRequest<{ items: AdminBoat[]; totalCount: number }>(API_ENDPOINTS.boats, {
      query: { pageNumber, pageSize: PAGE_SIZE, isActiveOnly },
    })
    boats.push(...page.items)
    if (!page.items.length || boats.length >= page.totalCount) return boats
  }
}
