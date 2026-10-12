import { Icon } from '../../../components/ui'
import type { BookingStep } from '../types'
import { t } from '../../../i18n'

interface BookingProgressProps {
  steps: BookingStep[]
  /** Zero-based index of the active step. Earlier steps are complete. */
  current: number
  variant: 'circles' | 'bars'
  /** Circles only: show "01 Trip" above the detail instead of "Step 1". */
  numbered?: boolean
}

/** Five-step booking tracker. `circles` is used on passenger details, `bars` on checkout. */
export default function BookingProgress({
  steps,
  current,
  variant,
  numbered,
}: BookingProgressProps) {
  if (variant === 'bars') {
    return (
      <div className="grid grid-cols-2 gap-space-md rounded-2xl bg-surface-container-lowest p-space-md shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:grid-cols-5">
        {steps.map((step, i) => {
          const done = i < current
          const active = i === current
          return (
            <div key={step.label}>
              <div className="mb-2 flex items-center justify-between text-xs font-semibold">
                <span className={done || active ? 'text-teal-flow' : 'text-outline'}>
                  {String(i + 1).padStart(2, '0')} {t(step.label)}
                </span>
                {done && <Icon name="check_circle" className="text-[16px] text-teal-flow" />}
                {active && (
                  <Icon name="radio_button_checked" className="text-[16px] text-teal-flow" />
                )}
                {!done && !active && <Icon name="schedule" className="text-[16px] text-outline" />}
              </div>
              <div className="h-1 overflow-hidden rounded-full bg-surface-container">
                <div
                  className={`h-full rounded-full bg-teal-flow ${
                    done ? 'w-full' : active ? 'w-3/4' : 'w-0'
                  }`}
                />
              </div>
              <div className="mt-2 text-xs text-on-surface-variant">{t(step.detail)}</div>
            </div>
          )
        })}
      </div>
    )
  }

  return (
    <div className="grid grid-cols-2 gap-space-md rounded-2xl bg-surface-container-lowest p-space-md shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:grid-cols-5">
      {steps.map((step, i) => {
        const done = i < current
        const active = i === current
        return (
          <div key={step.label} className="flex items-center gap-space-sm">
            <span
              className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-bold ${
                done
                  ? 'bg-teal-flow text-on-primary'
                  : active
                  ? 'bg-secondary-container text-teal-flow'
                  : 'bg-surface-container text-outline'
              }`}
            >
              {done ? <Icon name="check" className="text-[18px]" /> : i + 1}
            </span>
            <div className="min-w-0">
              <div
                className={`flex items-center gap-1 text-[11px] font-semibold uppercase tracking-wider ${
                  active ? 'text-teal-flow' : 'text-on-surface-variant'
                }`}
              >
                {numbered
                  ? `${String(i + 1).padStart(2, '0')} ${t(step.label)}`
                  : active
                  ? t('Active')
                  : t('Step {n}', { n: i + 1 })}
                {active && <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />}
              </div>
              <div
                className={`truncate text-sm ${
                  active ? 'font-semibold text-deep-river' : 'text-on-surface-variant'
                }`}
              >
                {t(step.detail)}
              </div>
            </div>
          </div>
        )
      })}
    </div>
  )
}
