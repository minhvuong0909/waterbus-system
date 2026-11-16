/**
 * Địa danh giới thiệu trên chuyến tham quan (chỉ phục vụ vẽ mốc trên bản đồ).
 *
 * Toạ độ là điểm trên đường sông demo (`riverPaths.ts`) gần địa danh thật nhất: nơi tàu đi ngang, nghe giới thiệu và cũng là điểm
 * kích hoạt audio, nên mốc luôn nằm sát đường tàu chạy. Mốc vẽ bằng icon tượng trưng theo `kind`. Toạ độ địa danh thật (nằm trong
 * đất liền, cách đường tàu tới hơn 1 km với Bình Quới) ghi ở comment của `ApplicationDbContextInitializer.SightseeingPois`.
 * BE giữ bản gốc ở bảng PointsOfInterest; chưa có API công khai trả địa danh, khi có thì lấy từ API và xoá file này.
 */
export type SightseeingPoiKind = 'tower' | 'bridge' | 'houses' | 'park'

export interface SightseeingPoi {
  name: string
  latitude: number
  longitude: number
  kind: SightseeingPoiKind
}

export const SIGHTSEEING_POIS: SightseeingPoi[] = [
  { name: 'Landmark 81', latitude: 10.792276, longitude: 106.725583, kind: 'tower' },
  { name: 'Cầu Sài Gòn', latitude: 10.798951, longitude: 106.727697, kind: 'bridge' },
  { name: 'Khu vực Thảo Điền ven sông', latitude: 10.80187, longitude: 106.727569, kind: 'houses' },
  { name: 'Bán đảo Thanh Đa', latitude: 10.819606, longitude: 106.716637, kind: 'park' },
  { name: 'Khu vực Bình Quới ven sông', latitude: 10.823449, longitude: 106.7296, kind: 'park' },
]
