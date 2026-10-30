import { t } from '../../../i18n'
import type { TripStatus } from '../types'

const STATUS_STYLE: Record<TripStatus, string> = {
  Scheduled: 'bg-surface-container text-deep-river',
  Boarding: 'bg-signal-amber/20 text-deep-river',
  EnRoute: 'bg-teal-flow/15 text-teal-flow',
  Suspended: 'bg-signal-amber/30 text-deep-river',
  Arrived: 'bg-teal-flow/10 text-deep-river',
  Completed: 'bg-sand-light text-on-surface-variant',
  Cancelled: 'bg-coral-glow/15 text-coral-glow',
  Terminated: 'bg-coral-glow/25 text-coral-glow',
}

const STATUS_LABEL: Record<TripStatus, string> = {
  Scheduled: 'Scheduled',
  Boarding: 'Boarding',
  EnRoute: 'In transit',
  Suspended: 'Suspended',
  Arrived: 'Arrived',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
  Terminated: 'Terminated',
}

/** Trip status (BE `TripStatus`) as a coloured pill. */
export default function TripStatusBadge({ status }: { status: TripStatus }) {
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-semibold ${STATUS_STYLE[status]}`}
    >
      {t(STATUS_LABEL[status])}
    </span>
  )
}
