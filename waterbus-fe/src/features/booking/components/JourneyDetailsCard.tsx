import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import { seatTags } from '../utils'
import BookingCard from './BookingCard'
import CardHeading from './CardHeading'
import { t } from '../../../i18n'
import EditLink from './EditLink'

function Fact({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="rounded-xl bg-mist/70 px-space-md py-space-sm">
      <div className="text-2xs text-on-surface-variant">{label}</div>
      <div className={`text-sm font-semibold ${accent ? 'text-teal-flow' : 'text-deep-river'}`}>
        {value}
      </div>
    </div>
  )
}

export default function JourneyDetailsCard({
  trip,
  seats,
  title,
  editTo = ROUTES.seatSelection,
}: {
  trip: TripDetail
  seats: Seat[]
  /** Heading, e.g. "Outbound trip" for a round trip; "Journey Details" by default. */
  title?: string
  /** Where "Change Trip / Seat" leads: the seat page of this leg. */
  editTo?: string
}) {
  return (
    <BookingCard>
      <CardHeading
        icon="directions_boat"
        title={title ?? t('Journey Details')}
        subtitle={t('Saigon River {line} • {code}', {
          line: trip.lineLabel,
          code: trip.vesselCode,
        })}
        aside={<EditLink to={editTo}>{t('Change Trip / Seat')}</EditLink>}
      />
      <div className="grid grid-cols-1 items-center gap-space-md rounded-xl bg-mist/70 p-space-md md:grid-cols-12">
        <div className="flex items-start gap-3 md:col-span-4">
          <span className="mt-1 flex h-5 w-5 items-center justify-center rounded-full bg-deep-river text-2xs font-bold text-white">
            1
          </span>
          <div>
            <div className="font-display text-2xl font-bold leading-none text-deep-river">
              {trip.departTime}
            </div>
            <div className="mt-1 text-sm font-semibold text-deep-river">{trip.originPierLabel}</div>
            {trip.originGate && (
              <div className="text-xs text-on-surface-variant">
                {t('{originGate} • Pontoon Terminal', { originGate: trip.originGate })}
              </div>
            )}
          </div>
        </div>
        <div className="flex flex-col items-center md:col-span-4">
          <span className="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-0.5 text-xs font-semibold text-on-secondary-container">
            <Icon name="waves" className="text-sm" />
            {t('{durationMins} min river crossing', { durationMins: trip.durationMins })}
          </span>
          <div className="my-1 flex w-full items-center gap-2 text-outline">
            <span className="h-px flex-1 bg-outline-variant" />
            <Icon name="arrow_forward" className="text-base text-teal-flow" />
            <span className="h-px flex-1 bg-outline-variant" />
          </div>
        </div>
        <div className="flex items-start justify-end gap-3 text-right md:col-span-4">
          <div>
            <div className="font-display text-2xl font-bold leading-none text-deep-river">
              {trip.arriveTime}
            </div>
            <div className="mt-1 text-sm font-semibold text-deep-river">
              {trip.destinationPierLabel}
            </div>
            {trip.destinationGate && (
              <div className="text-xs text-on-surface-variant">
                {t('{destinationGate} • Park Promenade', { destinationGate: trip.destinationGate })}
              </div>
            )}
          </div>
          <span className="mt-1 flex h-5 w-5 items-center justify-center rounded-full bg-teal-flow text-2xs font-bold text-white">
            2
          </span>
        </div>
      </div>
      <div className="mt-space-sm grid grid-cols-1 gap-space-sm md:grid-cols-2">
        <Fact label={t('Vessel')} value={trip.vesselCode} />
        <Fact
          label={seats.length > 1 ? t('Selected Seats') : t('Selected Seat')}
          value={seats
            .map((seat) => (seatTags(seat) ? `${seat.code} (${seatTags(seat)})` : seat.code))
            .join(', ')}
          accent
        />
        <Fact label={t('Date')} value={trip.dateFull} />
      </div>
    </BookingCard>
  )
}
