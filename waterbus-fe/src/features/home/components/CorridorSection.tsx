import { useState } from 'react'
import { Link } from 'react-router-dom'
import RiverMap from '../../../components/map/RiverMap'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { useAppDispatch } from '../../../store/hooks'
import { selectTrip } from '../../booking/bookingSlice'
import DepartureCard from '../../trips/components/DepartureCard'
import type { CorridorMap, Departure } from '../../trips/types'
import type { RouteFilter } from '../types'
import { t } from '../../../i18n'

interface CorridorSectionProps {
  filters: RouteFilter[]
  map: CorridorMap
  departures: Departure[]
}

// Which departures each filter pill keeps. 'all' keeps everything.
const FILTER_LINES: Record<RouteFilter['id'], Departure['line'][] | null> = {
  all: null,
  line1: ['line1', 'direct'],
  line2: ['line2'],
  sunset: ['sunset'],
}

export default function CorridorSection({ filters, map, departures }: CorridorSectionProps) {
  const dispatch = useAppDispatch()
  const [active, setActive] = useState<RouteFilter['id']>('all')
  const lines = FILTER_LINES[active]
  const visible = lines ? departures.filter((d) => lines.includes(d.line)) : departures

  return (
    <section className="w-full bg-mist py-space-3xl">
      <div className="mx-auto max-w-7xl space-y-space-xl px-margin">
        <div className="flex flex-col justify-between gap-space-md md:flex-row md:items-end">
          <div>
            <span className="block text-body-sm font-bold uppercase tracking-wider text-teal-flow">
              {t('Connected Waterway Grid')}
            </span>
            <h2 className="font-headline-lg text-headline-lg font-bold tracking-tight text-deep-river">
              {t('Explore the River Corridor')}
            </h2>
            <p className="pt-1 text-body-md text-on-surface-variant">
              {t(
                'Real-time monitoring across 18 river stations connecting District 1, Thu Thiem, Binh Thanh, and Thu Duc.',
              )}
            </p>
          </div>
          <div
            className="inline-flex flex-wrap rounded-full bg-surface-container p-1 shadow-inner"
            role="tablist"
          >
            {filters.map((filter) => (
              <button
                key={filter.id}
                type="button"
                role="tab"
                aria-selected={active === filter.id}
                onClick={() => setActive(filter.id)}
                className={`rounded-full px-space-md py-2 text-body-sm transition-colors ${
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
          <div className="space-y-space-md rounded-panel border border-outline-variant/30 bg-surface p-space-lg shadow-sm lg:col-span-7">
            <div className="flex items-center justify-between pb-space-xs">
              <div className="flex items-center gap-space-sm">
                <span className="h-3 w-3 animate-ping rounded-full bg-teal-flow" />
                <span className="font-headline-sm text-headline-sm font-bold text-deep-river">
                  {t('Live Waterway Vector')}
                </span>
              </div>
              <span className="rounded-full bg-sand-light px-space-sm py-1 text-body-sm font-medium text-on-surface-variant">
                {t('GPS Radar Live • 10s cycle')}
              </span>
            </div>
            <RiverMap map={map} />
          </div>

          <div className="space-y-space-md lg:col-span-5">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-headline-sm text-headline-sm font-bold text-deep-river">
                  {t('Next Departures')}
                </h3>
                <span className="text-xs text-on-surface-variant">
                  {t('Live from Bach Dang Pier')}
                </span>
              </div>
              <span className="rounded-full bg-sand-light px-2.5 py-1 text-xs font-semibold text-teal-flow">
                {t('Updated Live')}
              </span>
            </div>

            <div className="space-y-space-sm">
              {visible.length > 0 ? (
                visible.map((d) => (
                  <DepartureCard
                    key={d.id}
                    departure={d}
                    onSelect={(id) => dispatch(selectTrip(id))}
                  />
                ))
              ) : (
                <p className="rounded-2xl border border-dashed border-outline-variant bg-surface p-space-lg text-center text-body-md text-on-surface-variant">
                  {t('No departures on this line right now.')}
                </p>
              )}
            </div>

            <div className="flex items-center justify-between rounded-xl border border-teal-flow/20 bg-sand-light/80 p-3.5">
              <div className="flex items-center gap-2 text-deep-river">
                <Icon name="schedule" className="text-[20px] text-teal-flow" />
                <span className="text-xs font-medium">
                  {t('Boats arrive every 15 mins during peak hours (06:30–09:00 & 16:30–19:30)')}
                </span>
              </div>
              <Link
                to={ROUTES.searchResults}
                className="flex items-center gap-0.5 text-xs font-bold text-teal-flow hover:underline"
              >
                <span>{t('Timetable')}</span>
                <Icon name="chevron_right" className="text-[14px]" />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
