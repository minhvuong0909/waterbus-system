import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVndSuffix } from '../../../utils/format'
import type { TripDetail } from '../../trips/types'
import type { Seat } from '../types'
import { cabinsLabel, passengersLabel } from '../utils'
import { t } from '../../../i18n'

/** White strip under the page title: vessel, times, duration, fare and "Change Trip". */
export default function TripStrip({ trip, seats, totalVnd }: { trip: TripDetail; seats: Seat[]; totalVnd: number }) {
  return (
    <div className="flex flex-col items-start justify-between gap-space-md rounded-2xl bg-white p-space-md shadow-[0_2px_16px_rgba(13,37,56,0.05)] md:flex-row md:items-center">
      <div className="flex flex-wrap items-center gap-space-md">
        <div className="flex items-center gap-2 rounded-xl bg-deep-river px-space-md py-2 text-on-primary">
          <Icon name="directions_boat" className="text-[20px]" />
          <span className="font-headline-sm text-lg font-bold">{trip.vesselCode}</span>
        </div>
        <div className="flex items-center gap-space-md">
          <div>
            <div className="font-headline-sm text-lg font-bold text-deep-river">
              {trip.departTime}
            </div>
            <div className="text-[11px] text-on-surface-variant">{trip.originPierLabel}</div>
          </div>
          <Icon name="arrow_forward" className="text-[18px] text-outline" />
          <div>
            <div className="font-headline-sm text-lg font-bold text-deep-river">
              {trip.arriveTime}
            </div>
            <div className="text-[11px] text-on-surface-variant">{trip.destinationPierLabel}</div>
          </div>
        </div>
        <span className="flex items-center gap-1 rounded-full bg-mist px-3 py-1.5 text-xs font-medium text-on-surface-variant">
          <Icon name="schedule" className="text-[15px] text-teal-flow" />
          {t('{durationMins} min {crossingKind} Crossing', {
            durationMins: trip.durationMins,
            crossingKind: trip.crossingKind,
          })}
        </span>
      </div>
      <div className="flex items-center gap-space-lg">
        <div className="text-right">
          <div className="text-[11px] text-on-surface-variant">
            {t('{passengers} • {class}', {
              passengers: passengersLabel(seats.length),
              class: cabinsLabel(seats),
            })}
          </div>
          <div className="font-headline-sm text-lg font-bold text-deep-river">
            {formatVndSuffix(totalVnd)}
          </div>
        </div>
        <Link
          to={ROUTES.searchResults}
          className="flex items-center gap-1 text-sm font-semibold text-teal-flow hover:text-deep-river"
        >
          {t('Change Trip')} <Icon name="edit" className="text-[16px]" />
        </Link>
      </div>
    </div>
  )
}
