import { t } from '../../../i18n'
import { WEEK_DAYS, dayName } from '../scheduleDays'

/** The weekdays a schedule runs on, as seven toggles. */
export default function DayPicker({
  days,
  error,
  onChange,
}: {
  days: number[]
  error?: string
  onChange: (days: number[]) => void
}) {
  return (
    <fieldset>
      <legend className="mb-1 text-xs font-semibold text-on-surface-variant">{t('Operating days')}</legend>
      <div className="flex flex-wrap gap-1.5">
        {WEEK_DAYS.map((day) => {
          const on = days.includes(day)
          return (
            <button
              key={day}
              type="button"
              aria-pressed={on}
              onClick={() => onChange(on ? days.filter((d) => d !== day) : [...days, day])}
              className={`min-h-[36px] min-w-[3rem] rounded-full border px-3 text-xs font-semibold transition-colors ${
                on
                  ? 'border-teal-flow bg-teal-flow text-white'
                  : 'border-outline-variant/70 bg-white text-on-surface-variant hover:border-teal-flow hover:text-teal-flow'
              }`}
            >
              {dayName(day)}
            </button>
          )
        })}
      </div>
      {error && (
        <p role="alert" className="mt-1 text-2xs text-coral-glow">
          {error}
        </p>
      )}
    </fieldset>
  )
}
