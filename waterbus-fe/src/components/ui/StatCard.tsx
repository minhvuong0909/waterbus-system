import { Link } from 'react-router-dom'
import { t } from '../../i18n'
import { SURFACE_CLASS } from './styles'

/**
 * Một con số chính của trang (vd số chuyến hôm nay). `to`: bấm vào để tới nơi xem chi tiết.
 * `attention`: số đang cần Admin xử lý (lớn hơn 0) thì tô màu nhấn.
 */
export default function StatCard({
  label,
  value,
  to,
  attention = false,
  hint,
  hintCount,
}: {
  /** Chuỗi nguồn tiếng Anh. */
  label: string
  /** Số đếm, hoặc chuỗi đã định dạng (vd tiền). */
  value: number | string
  to?: string
  attention?: boolean
  /** Dòng phụ nhỏ dưới số (chuỗi nguồn tiếng Anh có thể có `{count}`). */
  hint?: string
  hintCount?: number
}) {
  const highlight = attention && typeof value === 'number' && value > 0
  const body = (
    <>
      <span className="block text-sm text-on-surface-variant">{t(label)}</span>
      <span
        className={`mt-1 block font-display font-bold leading-none tabular-nums ${
          typeof value === 'number' ? 'text-3xl' : 'text-2xl'
        } ${highlight ? 'text-coral-glow' : 'text-deep-river'}`}
      >
        {value}
      </span>
      {hint && <span className="mt-2 block text-xs text-on-surface-variant">{t(hint, hintCount === undefined ? undefined : { count: hintCount })}</span>}
    </>
  )
  const className = `${SURFACE_CLASS} block p-4 sm:p-5`
  return to ? (
    <Link to={to} className={`${className} transition-colors hover:border-teal-flow/50`}>
      {body}
    </Link>
  ) : (
    <div className={className}>{body}</div>
  )
}
