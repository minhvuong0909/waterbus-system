import { formatClock } from '../../../utils/time'
import { t } from '../../../i18n'
import type { TrackedStop } from '../types'

/** The stops of the trip in visit order, with the planned time at each one. */
export default function StopTimeline({ stops }: { stops: TrackedStop[] }) {
  return (
    <ol className="space-y-4">
      {stops.map((stop, index) => {
        const time = stop.estimatedDepartureTime ?? stop.estimatedArrivalTime
        const last = index === stops.length - 1
        return (
          <li key={stop.callId} className="relative flex gap-3 pl-1">
            {!last && <span className="absolute left-[9px] top-5 h-full w-px bg-outline-variant" aria-hidden="true" />}
            <span className="relative mt-1 h-3 w-3 shrink-0 rounded-full border-2 border-deep-river bg-white" />
            <div className="flex flex-1 items-baseline justify-between gap-3">
              <span className="text-sm font-semibold text-deep-river">{stop.stationName}</span>
              <span className="text-sm tabular-nums text-on-surface-variant">
                {time ? formatClock(new Date(time)) : t('No time set')}
              </span>
            </div>
          </li>
        )
      })}
    </ol>
  )
}
