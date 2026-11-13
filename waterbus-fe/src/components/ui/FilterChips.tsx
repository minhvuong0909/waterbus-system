import { useId } from 'react'
import { t } from '../../i18n'

/** Bộ lọc ít lựa chọn: luôn nhìn thấy lựa chọn hiện tại, không cần mở dropdown. */
export default function FilterChips({ label, value, onChange, options }: {
  label: string
  value: string
  onChange: (value: string) => void
  options: { value: string; label: string }[]
}) {
  const id = useId()
  return (
    <div className="min-w-0">
      <span id={id} className="mb-1.5 block text-xs font-semibold text-on-surface-variant">{t(label)}</span>
      <div role="group" aria-labelledby={id} className="flex max-w-full flex-wrap gap-2">
        {options.map((option) => (
          <button key={option.value} type="button" aria-pressed={value === option.value} onClick={() => onChange(option.value)}
            className={`min-h-[44px] shrink-0 whitespace-nowrap rounded-xl border px-3 text-sm font-medium transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-flow ${value === option.value ? 'border-teal-flow/40 bg-teal-flow/10 text-teal-flow shadow-sm' : 'border-outline-variant/40 bg-white text-on-surface-variant hover:border-teal-flow/30 hover:bg-mist hover:text-deep-river'}`}>
            {t(option.label)}
          </button>
        ))}
      </div>
    </div>
  )
}
