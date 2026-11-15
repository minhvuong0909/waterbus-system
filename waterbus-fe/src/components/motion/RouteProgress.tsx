import { useLocation } from 'react-router-dom'

/**
 * Vạch tiến trình mảnh ở mép trên mỗi khi đổi trang: báo cho khách biết web đã nhận thao tác. Đổi `key` theo đường
 * dẫn để mỗi lần chuyển trang vạch chạy lại từ đầu; chỉ là hiệu ứng ngắn nên không gắn với tiến độ tải thật.
 */
export default function RouteProgress() {
  const { pathname } = useLocation()
  return <div key={pathname} aria-hidden className="route-progress" />
}
