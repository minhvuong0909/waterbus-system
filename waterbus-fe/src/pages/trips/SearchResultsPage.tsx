import { useMemo, useState } from 'react'
import { ErrorState, FeatureStrip, PageLoader, StatusPill } from '../../components/ui'
import CorridorSidebar from '../../features/trips/components/CorridorSidebar'
import DateStrip from '../../features/trips/components/DateStrip'
import ResultFilters from '../../features/trips/components/ResultFilters'
import type { SortKey, TimeFilter } from '../../features/trips/components/ResultFilters'
import SearchSummaryBar from '../../features/trips/components/SearchSummaryBar'
import TripCard from '../../features/trips/components/TripCard'
import { formatDateLabel } from '../../features/trips/utils'
import { useSearchResults } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

const CUES = [
  {
    icon: 'qr_code_2',
    title: 'QR E-Ticket Boarding',
    description: 'Instant digital ticket pass with tap-and-go access at turnstiles.',
  },
  {
    icon: 'radar',
    title: 'Real-Time Vessel Tracking',
    description: 'Live GPS positioning and accurate pier arrival predictions.',
  },
  {
    icon: 'airline_seat_recline_extra',
    title: 'Seat Selection Next',
    description: 'Choose panoramic windows or open deck seats in the immediate next step.',
  },
]

export default function SearchResultsPage() {
  useDocumentTitle('Available Departures')
  const { query, updateQuery, piers, dates, trips, loading, error, retry, chooseTrip } =
    useSearchResults()
  const [time, setTime] = useState<TimeFilter>('all')
  const [vessel, setVessel] = useState('all')
  const [sort, setSort] = useState<SortKey>('time')

  const visible = useMemo(() => {
    if (!trips) return []
    const list = trips.filter(
      (trip) => (time === 'all' || trip.band === time) && (vessel === 'all' || trip.id === vessel),
    )
    const by: Record<SortKey, (a: (typeof list)[number], b: (typeof list)[number]) => number> = {
      time: (a, b) => a.departTime.localeCompare(b.departTime),
      speed: (a, b) => a.durationMins - b.durationMins,
      price: (a, b) => a.fareVnd - b.fareVnd,
    }
    return [...list].sort(by[sort])
  }, [trips, time, vessel, sort])

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !piers || !dates || !trips) return <PageLoader />

  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  const selectedDate = dates.find((d) => d.id === query.dateId) ?? dates[0]
  const dateLabel = formatDateLabel(selectedDate)

  return (
    <div className="bg-mist pb-16 pt-4">
      <div className="mx-auto flex max-w-7xl flex-col gap-space-xl px-margin">
        <section className="flex w-full flex-col gap-space-md">
          <div className="flex flex-col justify-between gap-space-md pt-2 md:flex-row md:items-end">
            <div>
              <div className="mb-2 inline-flex items-center gap-space-xs rounded-full border border-teal-flow/20 bg-sand-light px-2.5 py-0.5 text-xs font-semibold uppercase tracking-wider text-deep-river">
                <span className="h-2 w-2 rounded-full bg-teal-flow" />
                {t('Saigon River Corridor #1')}
              </div>
              <h1 className="font-headline-lg text-2xl font-bold leading-tight tracking-tight text-deep-river md:text-3xl lg:text-[34px]">
                {t('Available Departures')}
              </h1>
              <p className="mt-1 text-sm text-on-surface-variant">
                {t('Select your river crossing across the Saigon River transit corridor.')}
              </p>
            </div>
            <div className="self-start md:self-end">
              <StatusPill variant="soft">{t('All River Terminals Operational')}</StatusPill>
            </div>
          </div>

          <SearchSummaryBar query={query} piers={piers} dateLabel={dateLabel} />
          <DateStrip
            dates={dates}
            selectedId={selectedDate.id}
            tripCount={trips.length}
            onSelect={(id) => updateQuery({ dateId: id })}
          />
          <ResultFilters
            trips={trips}
            time={time}
            vessel={vessel}
            sort={sort}
            onTime={setTime}
            onVessel={setVessel}
            onSort={setSort}
          />
        </section>

        <div className="grid grid-cols-1 items-start gap-space-xl lg:grid-cols-12">
          <div className="flex flex-col gap-space-md lg:col-span-8">
            <div className="flex items-center justify-between px-1">
              <div className="flex items-center gap-space-xs text-sm text-deep-river">
                <span className="font-bold text-teal-flow">
                  {t('{length} Departures', { length: visible.length })}
                </span>
                <span className="text-on-surface-variant">{t('scheduled for today')}</span>
              </div>
              <div className="flex items-center gap-space-xs text-xs text-on-surface-variant">
                <span className="h-2 w-2 animate-pulse rounded-full bg-teal-flow" />
                {t('Real-time boarding gate & turnstile sync')}
              </div>
            </div>

            {visible.length > 0 ? (
              visible.map((trip) => (
                <TripCard
                  key={trip.id}
                  trip={trip}
                  origin={pier(query.originId)}
                  destination={pier(query.destinationId)}
                  onSelect={chooseTrip}
                />
              ))
            ) : (
              <p className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-xl text-center text-on-surface-variant">
                {t('No departures match these filters.')}
              </p>
            )}
          </div>
          <CorridorSidebar />
        </div>

        <FeatureStrip items={CUES} variant="panel" />
      </div>
    </div>
  )
}
