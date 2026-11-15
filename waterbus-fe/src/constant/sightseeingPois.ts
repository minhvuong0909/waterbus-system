/**
 * Địa danh giới thiệu trên chuyến tham quan (chỉ phục vụ vẽ mốc trên bản đồ).
 *
 * Toạ độ là điểm trên đường sông demo (`riverPaths.ts`) gần địa danh thật nhất: nơi tàu đi ngang và nghe giới thiệu.
 * BE giữ bản gốc ở `ApplicationDbContextInitializer.SightseeingPois` (bảng PointsOfInterest); chưa có API công khai
 * trả toạ độ địa danh, khi có thì lấy từ API và xoá file này.
 */
export interface SightseeingPoi {
  name: string
  latitude: number
  longitude: number
}

export const SIGHTSEEING_POIS: SightseeingPoi[] = [
  { name: 'Landmark 81', latitude: 10.792276, longitude: 106.725583 },
  { name: 'Cầu Sài Gòn', latitude: 10.798951, longitude: 106.727697 },
  { name: 'Khu vực Thảo Điền ven sông', latitude: 10.80187, longitude: 106.727569 },
  { name: 'Bán đảo Thanh Đa', latitude: 10.819606, longitude: 106.716637 },
  { name: 'Khu vực Bình Quới ven sông', latitude: 10.823449, longitude: 106.7296 },
]
