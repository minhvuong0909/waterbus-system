import { lazy, Suspense, useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip, setQuery } from '../../booking/bookingSlice'
import DepartureCard from '../../trips/components/DepartureCard'
import type { CorridorMap, Departure } from '../../trips/types'
import type { RouteFilter } from '../types'
import { t } from '../../../i18n'

// The map pulls in Leaflet and its styles, so it loads after the rest of the page.
const PierMap = lazy(() => import('../../../components/map/PierMap'))

interface CorridorSectionProps {
  filters: RouteFilter[]
  map: CorridorMap
  departures: Departure[]
}

export default function CorridorSection({ filters, map, departures }: CorridorSectionProps) {
  const dispatch = useAppDispatch()
  const [active, setActive] = useState<RouteFilter['id']>('all')
  // The filter ids 'transit' and 'sightseeing' are the trip kinds; 'all' keeps everything.
  const visible = active === 'all' ? departures : departures.filter((d) => d.kind === active)

  return (
    <section className="w-full bg-mist py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-6 px-5 sm:px-8">
        <div
          data-reveal
          className="flex flex-col justify-between gap-space-md md:flex-row md:items-end"
        >
          <div>
            <h2 className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]">
              {t('Explore the River Corridor')}
            </h2>
          </div>
          <div
            className="grid grid-cols-3 rounded-xl bg-surface-container p-1 sm:inline-flex sm:rounded-full"
            role="tablist"
          >
            {filters.map((filter) => (
              <button
                key={filter.id}
                type="button"
                role="tab"
                aria-selected={active === filter.id}
                onClick={() => setActive(filter.id)}
                className={`min-h-[40px] whitespace-nowrap rounded-lg px-2 py-2 text-xs transition-colors sm:rounded-full sm:px-4 ${
                  active === filter.id
                    ? 'bg-teal-flow font-semibold text-on-primary shadow-sm'
                    : 'font-medium text-on-surface-variant hover:text-deep-river'
                }`}
              >
                {filter.label}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 items-start gap-space-xl lg:grid-cols-12">
          <div
            data-reveal
            className="space-y-space-md rounded-panel border border-outline-variant/30 bg-white p-4 shadow-sm sm:p-5 lg:col-span-7"
          >
            <div className="flex items-center justify-between pb-space-xs">
              <div className="flex items-center gap-space-sm">
                <span className="h-2 w-2 rounded-full bg-teal-flow ring-4 ring-teal-flow/10" />
                <span className="font-headline-sm text-headline-sm font-bold text-deep-river">
                  {t('Live Waterway Vector')}
                </span>
              </div>
            </div>
            <Suspense
              fallback={
                <div className="flex h-[320px] items-center justify-center rounded-2xl bg-surface-container text-sm text-on-surface-variant sm:h-[380px]">
                  {t('Loading map…')}
                </div>
              }
            >
              <PierMap
                compact
                map={map}
                pierActions={(pier) => (
                  <Link
                    to={ROUTES.search}
                    onClick={() => dispatch(setQuery({ originId: pier.id }))}
                    className="rounded-full bg-teal-flow px-3 py-1.5 text-xs font-semibold text-on-primary hover:bg-secondary"
                  >
                    {t('Plan a trip from here')}
                  </Link>
                )}
              />
            </Suspense>
          </div>

          <div
            data-reveal
            className="flex flex-col gap-4 rounded-panel border border-outline-variant/30 bg-white p-5 shadow-sm lg:col-span-5"
          >
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-headline-sm text-headline-sm font-bold text-deep-river">
                  {t('Next Departures')}
                </h3>
                <span className="text-xs text-on-surface-variant">
                  {t('Live from {pier}', { pier: map.piers[0]?.name ?? '' })}
                </span>
              </div>
            </div>

            <div className="space-y-space-sm">
              {visible.length > 0 ? (
                visible.map((d) => (
                  <DepartureCard
                    key={d.id}
                    departure={d}
                    onSelect={(trip) => dispatch(selectTrip(trip))}
                  />
                ))
              ) : (
                <div className="flex min-h-[180px] flex-col items-center justify-center gap-4 rounded-2xl border border-dashed border-teal-flow/20 bg-mist px-5 py-8 text-center sm:min-h-[274px]">
                  <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-teal-flow/10 text-teal-flow">
                    <Icon name="directions_boat" className="text-[28px]" />
                  </span>
                  <p className="max-w-[240px] text-sm leading-relaxed text-on-surface-variant">
                    {t('No more departures of this type today.')}
                  </p>
                </div>
              )}
            </div>

            <Link
              to={ROUTES.searchResults}
              className="flex items-center justify-end gap-0.5 text-sm font-bold text-teal-flow hover:underline"
            >
              <span>{t('Timetable')}</span>
              <Icon name="chevron_right" className="text-[16px]" />
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
