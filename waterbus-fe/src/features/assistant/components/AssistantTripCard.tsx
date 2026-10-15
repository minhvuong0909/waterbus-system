import { Icon } from '../../../components/ui'
import { formatNumber } from '../../../utils/format'
import type { Trip } from '../../trips/types'
import { seatsLabel } from '../../trips/utils'
import { t } from '../../../i18n'

interface AssistantTripCardProps {
  trip: Trip
  onSelect: (trip: Trip) => void
}

/** Compact trip card shown inside the chat when the assistant suggests a departure. */
export default function AssistantTripCard({ trip, onSelect }: AssistantTripCardProps) {
  const sightseeing = trip.kind === 'sightseeing'
  return (
    <article className="rounded-xl border border-deep-river/10 bg-white p-3 shadow-xs">
      <div className="flex items-center justify-between gap-2 text-[11px]">
        <span className="truncate rounded-full bg-sand-light px-2 py-0.5 font-semibold text-deep-river">
          {trip.lineLabel}
        </span>
        <span className={`font-bold ${trip.seatsLow ? 'text-coral-glow' : 'text-teal-flow'}`}>
          {seatsLabel(trip)}
        </span>
      </div>
      <div className="mt-2 flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
        {trip.departTime}
        <Icon name="arrow_forward" className="text-[16px] text-teal-flow" />
        {trip.arriveTime}
        <span className="ml-auto text-xs font-medium text-on-surface-variant">
          {t('{durationMins} min', { durationMins: trip.durationMins })}
        </span>
      </div>
      <div className="mt-2 flex items-center justify-between">
        <span className="text-xs text-on-surface-variant">
          {sightseeing ? t('Sightseeing') : t('Regular')} •{' '}
          <strong className="text-deep-river">
            {formatNumber(trip.fareVnd)} {t('VND')}
          </strong>
        </span>
        <button
          type="button"
          onClick={() => onSelect(trip)}
          className="inline-flex items-center gap-1 rounded-full bg-teal-flow px-3 py-1 text-xs font-semibold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('Select Trip')}
          <Icon name="arrow_forward" className="text-[14px]" />
        </button>
      </div>
    </article>
  )
}
