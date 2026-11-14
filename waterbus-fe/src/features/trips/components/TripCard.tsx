import { useId, useState } from 'react'
import { Button, ErrorState, Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatNumber } from '../../../utils/format'
import { useJourneyStops } from '../hooks/useJourneyStops'
import type { PierOption, Trip } from '../types'
import { hasFare, seatsLabel } from '../utils'
import { t } from '../../../i18n'

interface TripCardProps {
  trip: Trip
  origin: PierOption
  destination: PierOption
  onSelect: (trip: Trip) => void
  /** Chuyến đã rời bến: không chọn được nữa. */
  departed?: boolean
  /** Where "Select Trip" leads; seat selection by default. */
  to?: string
  /** Not selectable for another reason, with the text shown instead of the button. */
  blockedReason?: string
}

/** The piers the boat calls at between the traveler's two piers, read when the card is opened. */
function JourneyStops({ tripId, originId, destinationId }: { tripId: string; originId: string; destinationId: string }) {
  const { stops, loading, error, retry } = useJourneyStops(tripId, originId, destinationId)

  if (error) return <ErrorState compact onRetry={retry} />
  if (loading || !stops) {
    return <p className="text-sm text-on-surface-variant">{t('Loading')}…</p>
  }
  if (stops.length === 0) {
    return <p className="text-sm text-on-surface-variant">{t('Stop times are not available for this trip.')}</p>
  }
  return (
    <ol className="relative space-y-2 border-l border-outline-variant pl-space-md">
      {stops.map((stop, i) => {
        const end = i === 0 || i === stops.length - 1
        return (
          <li key={stop.callId} className="relative flex items-baseline gap-3 text-sm">
            <span
              className={`absolute -left-[21px] top-1.5 h-2.5 w-2.5 rounded-full border-2 border-teal-flow ${
                end ? 'bg-teal-flow' : 'bg-white'
              }`}
            />
            <span className="w-11 shrink-0 font-display font-bold text-deep-river">{stop.time}</span>
            <span className={end ? 'font-semibold text-deep-river' : 'text-on-surface-variant'}>
              {stop.stationName}
            </span>
          </li>
        )
      })}
    </ol>
  )
}

/**
 * One trip of the search results: when it leaves and arrives, how long it takes, what it costs
 * and how many seats are left. The stops in between open on demand, so the list stays short.
 */
export default function TripCard({
  trip,
  origin,
  destination,
  onSelect,
  departed = false,
  to = ROUTES.seatSelection,
  blockedReason,
}: TripCardProps) {
  const [showStops, setShowStops] = useState(false)
  const stopsId = useId()
  const soldOut = trip.seatsCount <= 0
  const closed = soldOut || departed || Boolean(blockedReason)

  return (
    <article className={`rounded-2xl bg-white shadow-card ${departed || blockedReason ? 'opacity-60' : ''}`}>
      <div className="grid grid-cols-1 items-center gap-space-md p-space-lg md:grid-cols-[minmax(0,1fr)_auto] md:gap-space-xl">
        <div className="flex items-center gap-space-md">
          <div>
            <div className="font-display text-2xl font-bold text-deep-river">{trip.departTime}</div>
            <div className="text-sm text-on-surface-variant">{origin.name}</div>
          </div>
          <div className="flex min-w-[4rem] flex-1 flex-col items-center gap-1">
            {trip.durationKnown !== false && (
              <span className="whitespace-nowrap text-xs font-semibold text-teal-flow">
                {t('{durationMins} min', { durationMins: trip.durationMins })}
              </span>
            )}
            <span className="h-px w-full bg-outline-variant" />
          </div>
          <div className="text-right">
            <div className="font-display text-2xl font-bold text-deep-river">{trip.arriveTime}</div>
            <div className="text-sm text-on-surface-variant">{destination.name}</div>
          </div>
        </div>

        <div className="flex items-center justify-between gap-space-lg md:border-l md:border-surface-container md:pl-space-xl">
          <div className="md:text-right">
            {hasFare(trip.fareVnd) ? (
              <>
                <div className="text-2xs font-semibold uppercase tracking-wider text-outline">{trip.fareLabel}</div>
                <div className="whitespace-nowrap font-display text-xl font-bold text-deep-river">
                  {formatNumber(trip.fareVnd)}{' '}
                  <span className="text-xs font-medium text-on-surface-variant">{t('VND')}</span>
                </div>
              </>
            ) : (
              <div className="max-w-[10rem] text-sm font-semibold leading-snug text-deep-river">
                {t('Price shown when you choose a seat')}
              </div>
            )}
          </div>
          {closed ? (
            <span
              aria-disabled="true"
              className="inline-flex cursor-not-allowed items-center whitespace-nowrap rounded-full bg-surface-container px-space-lg py-2.5 text-sm font-semibold text-on-surface-variant"
            >
              {departed ? t('Departed') : soldOut ? t('Sold out') : blockedReason}
            </span>
          ) : (
            <Button to={to} onClick={() => onSelect(trip)}>
              {t('Select Trip')}
              <Icon name="arrow_forward" className="text-lg" />
            </Button>
          )}
        </div>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-surface-container px-space-lg py-2 text-xs">
        <button
          type="button"
          aria-expanded={showStops}
          aria-controls={stopsId}
          onClick={() => setShowStops((open) => !open)}
          className="tap-area inline-flex items-center gap-1 font-semibold text-teal-flow hover:text-deep-river"
        >
          {t('Stops')}
          <Icon name="expand_more" className={`text-base transition-transform ${showStops ? 'rotate-180' : ''}`} />
        </button>
        <span className="flex items-center gap-space-sm">
          {trip.kind === 'sightseeing' && (
            <span className="rounded-full bg-coral-glow/15 px-2.5 py-0.5 font-semibold text-coral-glow">
              {t('Sightseeing')}
            </span>
          )}
          {!departed && (
            <span className={`font-semibold ${trip.seatsLow && !soldOut ? 'text-coral-glow' : 'text-on-surface-variant'}`}>
              {seatsLabel(trip)}
            </span>
          )}
        </span>
      </div>

      {showStops && (
        <div id={stopsId} className="border-t border-surface-container px-space-lg py-space-md">
          <JourneyStops tripId={trip.id} originId={origin.id} destinationId={destination.id} />
        </div>
      )}
    </article>
  )
}
