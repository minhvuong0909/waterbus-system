import { t } from '../../i18n' /** Khung giữ chỗ trong lúc tải dữ liệu (hoặc chunk code) của trang, để bố cục không bị nhảy. */
export default function PageLoader() {
  return (
    <div
      role="status"
      aria-busy="true"
      aria-label={t('Loading')}
      className="flex min-h-[60vh] items-center justify-center"
    >
      <span className="h-8 w-8 animate-spin rounded-full border-2 border-outline-variant border-t-teal-flow" />
    </div>
  )
}
