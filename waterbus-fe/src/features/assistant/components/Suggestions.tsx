import { t } from '../../../i18n'

/** Questions offered before the first message. The label is shown, the question is what is asked. */
const SUGGESTIONS = [
  { label: 'Upcoming departures', question: 'Hiện có những chuyến tàu nào sắp khởi hành?' },
  {
    label: 'Bạch Đằng → Linh Đông tomorrow',
    question: 'Tìm chuyến từ Bến Bạch Đằng đến Bến Linh Đông ngày mai',
  },
  { label: 'Trips this evening', question: 'Chiều tối nay còn chuyến nào không?' },
] as const

/** Starter questions, shown until the traveler sends something. */
export default function Suggestions({ onPick, disabled }: { onPick: (label: string, question: string) => void; disabled: boolean }) {
  return (
    <div className="pl-9">
      <p className="mb-1.5 text-xs font-semibold text-on-surface-variant">{t('You can ask:')}</p>
      <div className="flex flex-wrap gap-1.5">
        {SUGGESTIONS.map((s) => (
          <button
            key={s.label}
            type="button"
            disabled={disabled}
            onClick={() => onPick(t(s.label), s.question)}
            className="min-h-[36px] rounded-full border border-teal-flow/30 bg-white px-3 text-xs font-medium text-deep-river transition-colors hover:border-teal-flow hover:bg-teal-flow/5 disabled:opacity-50"
          >
            {t(s.label)}
          </button>
        ))}
      </div>
    </div>
  )
}
