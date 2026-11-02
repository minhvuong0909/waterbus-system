import SearchForm from '../../booking/components/SearchForm'
import { useSearchForm } from '../../booking/hooks/useTripSearch'

/**
 * Form tìm chuyến ngay trong hero trang chủ. Khi đang tải hiện khung xương cùng chiều cao; khi lỗi
 * hoặc chưa có bến thì ẩn hẳn (thông báo lỗi + nút thử lại đã nằm ngay bên dưới hero), thay vì để
 * một ô trắng trống khiến người dùng tưởng form bị hỏng.
 */
export default function HeroSearch() {
  const { piers, dates, loading, error } = useSearchForm()
  if (loading) {
    return (
      <div aria-hidden className="min-h-[84px] animate-pulse rounded-2xl bg-white/80 shadow-lg motion-reduce:animate-none" />
    )
  }
  if (error || !piers || !dates || piers.length === 0 || dates.length === 0) return null
  return <SearchForm piers={piers} dates={dates} variant="bar" />
}
