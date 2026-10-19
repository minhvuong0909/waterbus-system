import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip } from '../../booking/bookingSlice'
import DepartureCard from '../../trips/components/DepartureCard'
import type { RouteFilter } from '../types'
import type { NextDepartures } from '../services/homeService'
import { t } from '../../../i18n'

interface DeparturesSectionProps {
  filters: RouteFilter[]
  departures: NextDepartures
  pierName: string
}

/** Chuyến sắp khởi hành, đặt ngay dưới form tìm chuyến để khách chọn nhanh mà không cần tìm. */
export default function DeparturesSection({ filters, departures, pierName }: DeparturesSectionProps) {
  const dispatch = useAppDispatch()
  const [active, setActive] = useState<RouteFilter['id']>('all')
  // The filter ids 'transit' and 'sightseeing' are the trip kinds; 'all' keeps everything.
  const visible =
    active === 'all' ? departures.items : departures.items.filter((d) => d.kind === active)

  return (
    <section id="departures" className="w-full scroll-mt-20 bg-mist py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-6 px-5 sm:px-8">
        <div
          data-reveal
          className="flex flex-col justify-between gap-space-md md:flex-row md:items-end"
        >
          <div>
            <h2 className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]">
              {t('Upcoming departures')}
            </h2>
            <p className="mt-1 text-sm text-on-surface-variant">
              {departures.tomorrow
                ? t('No more departures today. Here is tomorrow’s timetable from {pier}.', {
                    pier: pierName,
                  })
                : t('Live from {pier}', { pier: pierName })}
            </p>
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

        {visible.length > 0 ? (
          <div data-reveal className="grid grid-cols-1 gap-space-sm md:grid-cols-2">
            {visible.map((d) => (
              <DepartureCard key={d.id} departure={d} onSelect={(trip) => dispatch(selectTrip(trip))} />
            ))}
          </div>
        ) : (
          <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-teal-flow/20 bg-white px-5 py-8 text-center">
            <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-teal-flow/10 text-teal-flow">
              <Icon name="directions_boat" className="text-[26px]" />
            </span>
            <p className="max-w-sm text-sm leading-relaxed text-on-surface-variant">
              {t('No departures of this type are scheduled yet.')}
            </p>
          </div>
        )}

        <Link
          to={ROUTES.searchResults}
          className="inline-flex items-center gap-0.5 text-sm font-bold text-teal-flow hover:underline"
        >
          <span>{t('View the full timetable')}</span>
          <Icon name="chevron_right" className="text-[16px]" />
        </Link>
      </div>
    </section>
  )
}
