import { Icon } from '../ui'
import { useScrolledPast, usePrefersReducedMotion } from '../../hooks'
import { t } from '../../i18n'

/** Nút về đầu trang: chỉ hiện khi đã cuộn quá khoảng một màn hình, đặt ngay trên nút trợ lý. */
export default function BackToTop() {
  const visible = useScrolledPast(700)
  const reducedMotion = usePrefersReducedMotion()

  return (
    <button
      type="button"
      aria-label={t('Back to top')}
      tabIndex={visible ? 0 : -1}
      aria-hidden={!visible}
      onClick={() => window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' })}
      className={`fixed bottom-24 right-5 z-30 flex h-11 w-11 items-center justify-center rounded-full border border-deep-river/10 bg-white text-deep-river shadow-card transition-all duration-300 hover:bg-sand-light/60 print:hidden ${
        visible ? 'translate-y-0 opacity-100' : 'pointer-events-none translate-y-3 opacity-0'
      }`}
    >
      {/* `expand_more` đã có trong font con; xoay lại thành mũi tên lên. */}
      <Icon name="expand_more" className="rotate-180 text-2xl" />
    </button>
  )
}
