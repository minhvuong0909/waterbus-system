import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

/** The steps of a booking, in order. The labels are English source strings. */
export const BOOKING_STEPS = ['Trip', 'Seat', 'Passengers', 'Checkout', 'Payment'] as const

export type BookingStepName = (typeof BOOKING_STEPS)[number]

/** Where the traveler is in the booking: earlier steps are ticked, later ones are muted. */
export default function BookingProgress({ current }: { current: BookingStepName }) {
  const currentIndex = BOOKING_STEPS.indexOf(current)
  return (
    <ol className="flex items-center gap-2 sm:gap-3">
      {BOOKING_STEPS.map((step, i) => {
        const done = i < currentIndex
        const active = i === currentIndex
        return (
          <li
            key={step}
            aria-current={active ? 'step' : undefined}
            className={`flex items-center gap-2 sm:gap-3 ${i > 0 ? 'flex-1' : ''}`}
          >
            {i > 0 && (
              <span className={`h-px flex-1 ${done || active ? 'bg-teal-flow' : 'bg-outline-variant'}`} />
            )}
            <span
              className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold ${
                done
                  ? 'bg-teal-flow text-on-primary'
                  : active
                    ? 'bg-deep-river text-on-primary'
                    : 'bg-surface-container text-outline'
              }`}
            >
              {done ? <Icon name="check" className="text-base" /> : i + 1}
            </span>
            <span
              className={`whitespace-nowrap text-sm ${
                active
                  ? 'font-semibold text-deep-river'
                  : `hidden sm:inline ${done ? 'text-deep-river' : 'text-on-surface-variant'}`
              }`}
            >
              {t(step)}
            </span>
          </li>
        )
      })}
    </ol>
  )
}
