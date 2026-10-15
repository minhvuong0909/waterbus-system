import { Link } from 'react-router-dom'
import mapImage from '../../../assets/images/corridor-map.jpg'
import vesselImage from '../../../assets/images/vessel.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TripDetail } from '../types'
import { formatNumber } from '../../../utils/format'
import { t } from '../../../i18n'

/** Right-hand column of the trip detail page: vessel photo, crossing map and fare summary. */
export default function TripAside({ trip }: { trip: TripDetail }) {
  const fare = formatNumber(trip.fareVnd)

  return (
    <aside className="space-y-space-lg lg:sticky lg:top-24 lg:col-span-5">
      <div className="overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
        <div className="relative h-36 w-full">
          <img
            alt={t('{vessel} sailing across the Saigon River towards {pier}', {
              vessel: trip.vesselName,
              pier: trip.destinationPierName,
            })}
            src={vesselImage}
            className="h-full w-full object-cover"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-deep-river/80 to-transparent" />
          <div className="absolute inset-x-space-md bottom-space-sm flex items-end justify-between text-white">
            <span className="font-headline-sm text-headline-sm">
              {t('Trip {code} • {kind}', {
                code: trip.vesselCode,
                kind: trip.direct ? t('Direct Service') : trip.crossingKind,
              })}
            </span>
            <span className="rounded bg-teal-flow px-2 py-0.5 text-xs font-semibold">
              {t('Scheduled')}
            </span>
          </div>
        </div>
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

      <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
        <h2 className="mb-space-sm font-headline-md text-headline-md text-deep-river">
          {t('Fare Summary')}
        </h2>
        <div className="flex items-center justify-between border-b border-surface-container pb-space-sm text-body-md text-on-surface-variant">
          <span>{t('Standard Transit Fare')}</span>
          <span className="text-deep-river">{t('{fare} VND', { fare })}</span>
        </div>
        <div className="flex items-end justify-between py-space-md">
          <div>
            <div className="font-headline-sm text-headline-sm text-deep-river">
              {t('Total Amount')}
            </div>
            <div className="text-body-sm text-on-surface-variant">{t('VAT included')}</div>
          </div>
          <div className="flex items-baseline gap-1">
            <span className="font-numeric-lg text-numeric-lg font-bold text-deep-river">
              {fare}
            </span>
            <span className="text-body-sm text-on-surface-variant">{t('VND')}</span>
          </div>
        </div>
        <Link
          to={ROUTES.seatSelection}
          className="group flex w-full items-center justify-center gap-space-xs rounded-full bg-teal-flow py-space-sm font-headline-sm text-[16px] font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary"
        >
          {t('Choose Seat')}
          <Icon
            name="arrow_forward"
            className="text-[18px] transition-transform group-hover:translate-x-1"
          />
        </Link>
        <p className="mt-space-xs text-center text-body-sm text-on-surface-variant">
          {t('Step 1 of 3: Trip Selected')}
        </p>
        <div className="mt-space-md flex items-center gap-2 rounded-lg bg-mist px-space-sm py-space-xs text-body-sm text-on-surface-variant">
          <Icon name="help_outline" className="text-[16px] text-teal-flow" />
          {t('Need assistance? Waterbus Support is available')}
        </div>
      </div>
    </aside>
  )
}
