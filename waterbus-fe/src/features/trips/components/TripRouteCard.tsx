import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import type { TripDetail } from '../types'
import { formatNumber } from '../../../utils/format'
import { t } from '../../../i18n'

interface TripRouteCardProps {
  trip: TripDetail
}

/** Hero card on the trip detail page: route, times and fare with the "Choose Seat" action. */
export default function TripRouteCard({ trip }: TripRouteCardProps) {
  return (
    <section className="relative mb-space-xl overflow-hidden rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_4px_24px_rgba(13,37,56,0.06)] md:p-space-xl">
      <div className="pointer-events-none absolute -right-16 -top-16 h-80 w-80 rounded-full bg-sky-aqua/10 blur-3xl" />
      <div className="relative z-10 flex flex-col justify-between gap-space-xl xl:flex-row xl:items-center">
        <div className="flex-1 space-y-space-md">
          <div className="flex flex-wrap items-center gap-space-sm">
            <span className="rounded bg-deep-river px-space-sm py-1 text-xs font-bold uppercase tracking-wider text-on-primary">
              {t('Trip {vesselCode}', { vesselCode: trip.vesselCode })}
            </span>
            <span className="rounded bg-mist px-space-sm py-1 text-xs font-semibold text-teal-flow">
              {trip.direct ? t('Direct Crossing') : trip.crossingKind}
            </span>
            <span className="flex items-center gap-1 rounded bg-sand-light px-space-sm py-1 text-xs font-semibold text-teal-flow">
              <Icon name="event_seat" className="text-[15px]" />
              {t('{count} Seats Available', { count: trip.seatsCount })}
            </span>
          </div>

          <div className="grid grid-cols-1 items-center gap-space-md pt-space-xs md:grid-cols-12">
            <div className="flex items-start gap-space-sm md:col-span-4">
              <div className="flex flex-col items-center">
                <span className="flex h-4 w-4 items-center justify-center rounded-full bg-teal-flow">
                  <span className="h-1.5 w-1.5 rounded-full bg-surface-container-lowest" />
                </span>
                <span className="my-1 h-10 w-0.5 bg-mist" />
              </div>
              <div>
                <div className="font-numeric-lg text-numeric-lg leading-none text-deep-river">
                  {trip.departTime}
                </div>
                <div className="mt-1 font-headline-sm text-headline-sm text-deep-river">
                  {trip.originPierName}
                </div>
                <div className="text-body-sm text-on-surface-variant">{trip.originDistrict}</div>
              </div>
            </div>

            <div className="flex flex-col items-center justify-center px-space-xs py-space-xs md:col-span-4">
              <span className="mb-1 flex items-center gap-1 text-body-sm font-semibold text-teal-flow">
                <Icon name="schedule" className="text-[16px]" />
                {t('{durationMins} min {crossingKind}', {
                  durationMins: trip.durationMins,
                  crossingKind: trip.crossingKind,
                })}
              </span>
              <div className="flex w-full items-center gap-2">
                <div className="h-0.5 flex-1 rounded-full bg-gradient-to-r from-teal-flow to-sky-aqua" />
                <div className="flex h-8 w-8 items-center justify-center rounded-full bg-secondary-container text-teal-flow shadow-sm">
                  <Icon name="directions_boat" className="text-[18px]" />
                </div>
                <div className="h-0.5 flex-1 rounded-full bg-gradient-to-r from-sky-aqua to-teal-flow" />
              </div>
              <span className="mt-1 text-body-sm text-on-surface-variant">
                {trip.direct ? t('River Corridor Non-stop') : trip.crossingCaption}
              </span>
            </div>

            <div className="flex items-start gap-space-sm text-left md:col-span-4 md:justify-end md:text-right">
              <div>
                <div className="font-numeric-lg text-numeric-lg leading-none text-deep-river">
                  {trip.arriveTime}
                </div>
                <div className="mt-1 font-headline-sm text-headline-sm text-deep-river">
                  {trip.destinationPierName}
                </div>
                <div className="text-body-sm text-on-surface-variant">
                  {trip.destinationDistrict}
                </div>
              </div>
              <div className="flex flex-col items-center">
                <span className="flex h-4 w-4 items-center justify-center rounded-full bg-deep-river">
                  <span className="h-1.5 w-1.5 rounded-full bg-surface-container-lowest" />
                </span>
                <span className="my-1 h-10 w-0.5 bg-mist" />
              </div>
            </div>
          </div>
        </div>

        <div className="flex flex-col justify-between rounded-xl bg-mist/60 p-space-md xl:w-72">
          <div>
            <div className="flex items-center justify-between">
              <span className="text-body-sm font-semibold uppercase tracking-wider text-on-surface-variant">
                {t('Transit Fare')}
              </span>
              <span className="rounded bg-secondary-container px-space-xs py-0.5 text-body-sm font-bold text-teal-flow">
                {t('1 Pax')}
              </span>
            </div>
            <div className="mt-1 flex items-baseline gap-1">
              <span className="font-numeric-lg text-numeric-lg font-bold text-deep-river">
                {formatNumber(trip.fareVnd)}
              </span>
              <span className="text-body-md font-medium text-on-surface-variant">{t('VND')}</span>
            </div>
            <div className="mt-0.5 flex items-center gap-1 text-body-sm text-on-surface-variant">
              <Icon name="check_circle" className="text-[14px] text-teal-flow" />{' '}
              {t('Instant ticket confirmation')}
            </div>
          </div>
          <div className="space-y-space-xs pt-space-md">
            <Link
              to={ROUTES.seatSelection}
              className="group inline-flex w-full items-center justify-center gap-space-xs rounded-full bg-teal-flow px-space-lg py-space-sm font-headline-sm text-[16px] font-semibold text-on-primary shadow-[0_2px_12px_rgba(20,122,126,0.25)] transition-all hover:bg-secondary"
            >
              <span>{t('Choose Seat')}</span>
              <Icon
                name="arrow_forward"
                className="text-[18px] transition-transform group-hover:translate-x-1"
              />
            </Link>
            <p className="text-center text-body-sm text-on-surface-variant">
              {t('Select your seat on the next step')}
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}
