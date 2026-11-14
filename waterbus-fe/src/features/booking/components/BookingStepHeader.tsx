import type { ReactNode } from 'react'
import BookingProgress from './BookingProgress'
import type { BookingStepName } from './BookingProgress'
import HoldCountdown from './HoldCountdown'

/**
 * Top of every booking step: the progress, the title and one line of guidance, always in the same
 * place and size. On small screens the seat-hold countdown sits here; on large ones it is in the
 * sticky order summary.
 */
export default function BookingStepHeader({
  step,
  title,
  description,
  countdownFallback,
}: {
  step: BookingStepName
  /** Already translated. */
  title: string
  description?: string
  /** Shown instead of the countdown while no seats are held yet. */
  countdownFallback?: ReactNode
}) {
  return (
    <header>
      <BookingProgress current={step} />
      <h1 className="mt-space-lg font-display text-headline-lg tracking-tight text-deep-river">{title}</h1>
      {description && <p className="mt-1 text-body-md text-on-surface-variant">{description}</p>}
      <div className="mt-space-sm lg:hidden">
        <HoldCountdown fallback={countdownFallback} />
      </div>
    </header>
  )
}
