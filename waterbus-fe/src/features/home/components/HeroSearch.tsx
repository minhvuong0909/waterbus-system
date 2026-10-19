import SearchForm from '../../trips/components/SearchForm'
import { useSearchForm } from '../../trips/hooks/useTripSearch'

/**
 * Form tìm chuyến ngay trong hero trang chủ. Nếu danh sách bến chưa tải xong hoặc lỗi, chỉ giữ chỗ
 * trống cùng chiều cao để phần còn lại của trang không bị chặn.
 */
export default function HeroSearch() {
  const { piers, dates, loading, error } = useSearchForm()
  if (error || loading || !piers || !dates || piers.length === 0 || dates.length === 0) {
    return <div aria-hidden className="min-h-[190px] rounded-2xl bg-white/10" />
  }
  return <SearchForm piers={piers} dates={dates} />
}
