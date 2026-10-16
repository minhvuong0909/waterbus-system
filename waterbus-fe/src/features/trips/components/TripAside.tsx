import mapImage from '../../../assets/images/corridor-map.jpg'
import { Icon } from '../../../components/ui'
import type { TripDetail } from '../types'
import { t } from '../../../i18n'

/** Right-hand column of the trip detail page: the crossing map. Fare and the seat CTA sit in the route card. */
export default function TripAside({ trip }: { trip: TripDetail }) {
  return (
    <aside className="space-y-space-lg lg:sticky lg:top-24 lg:col-span-5">
      <div className="overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
        <div className="p-space-md">
          <div className="mb-space-sm flex items-center justify-between text-body-sm">
            <span className="flex items-center gap-1 font-semibold text-deep-river">
              <Icon name="explore" className="text-[16px] text-teal-flow" />
              {t('Corridor Crossing Map')}
            </span>
            <span className="text-on-surface-variant">
              {trip.originPierName} → {trip.destinationPierName}
            </span>
          </div>
          <div className="relative overflow-hidden rounded-xl bg-mist">
            <img
              alt={t('Corridor crossing map')}
              src={mapImage}
              className="h-40 w-full object-cover"
            />
            <div className="absolute left-3 top-3 rounded-lg bg-white/95 px-2 py-1 text-xs font-bold text-deep-river shadow-sm">
              {trip.originPierName}{' '}
              <span className="font-normal text-teal-flow">{trip.departTime}</span>
            </div>
            <div className="absolute bottom-3 right-3 rounded-lg bg-white/95 px-2 py-1 text-xs font-bold text-deep-river shadow-sm">
              {trip.destinationPierName}{' '}
              <span className="font-normal text-teal-flow">{trip.arriveTime}</span>
            </div>
          </div>
        </div>
      </div>

    </aside>
  )
}
