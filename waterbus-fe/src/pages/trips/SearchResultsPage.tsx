import { useMemo, useState } from 'react'
import { ErrorState, PageLoader } from '../../components/ui'
import CorridorSidebar from '../../features/trips/components/CorridorSidebar'
import DateStrip from '../../features/trips/components/DateStrip'
import ResultFilters from '../../features/trips/components/ResultFilters'
import type { KindFilter, SortKey, TimeFilter } from '../../features/trips/components/ResultFilters'
import SearchSummaryBar from '../../features/trips/components/SearchSummaryBar'
import TripCard from '../../features/trips/components/TripCard'
import { formatDateLabel } from '../../features/trips/utils'
import { useSearchResults } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

export default function SearchResultsPage() {
  useDocumentTitle('Available Departures')
  const { query, updateQuery, piers, dates, trips, searching, loading, error, retry, chooseTrip } =
    useSearchResults()
  const [time, setTime] = useState<TimeFilter>('all')
  const [kind, setKind] = useState<KindFilter>('all')
  const [sort, setSort] = useState<SortKey>('time')

  const visible = useMemo(() => {
    if (!trips) return []
    const list = trips.filter(
      (trip) => (time === 'all' || trip.band === time) && (kind === 'all' || trip.kind === kind),
    )
    const by: Record<SortKey, (a: (typeof list)[number], b: (typeof list)[number]) => number> = {
      time: (a, b) => a.departTime.localeCompare(b.departTime),
      speed: (a, b) => a.durationMins - b.durationMins,
      price: (a, b) => a.fareVnd - b.fareVnd,
    }
    return [...list].sort(by[sort])
  }, [trips, time, kind, sort])

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
              <h1 className="font-headline-lg text-2xl font-bold leading-tight tracking-tight text-deep-river md:text-3xl lg:text-[34px]">
                {t('Available Departures')}
              </h1>
              <p className="mt-1 text-sm text-on-surface-variant">
                {t('Select your river crossing across the Saigon River transit corridor.')}
              </p>
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
            time={time}
            kind={kind}
            sort={sort}
            onTime={setTime}
            onKind={setKind}
            onSort={setSort}
          />
        </section>

        <div className="grid grid-cols-1 items-start gap-space-xl lg:grid-cols-12">
          <div
            className={`flex flex-col gap-space-md transition-opacity lg:col-span-8 ${
              searching ? 'opacity-60' : ''
            }`}
          >
            <div className="flex items-center justify-between px-1">
              <div className="flex items-center gap-space-xs text-sm text-deep-river">
                <span className="font-bold text-teal-flow">
                  {t('{length} Departures', { length: visible.length })}
                </span>
                <span className="text-on-surface-variant">{t('on {date}', { date: dateLabel })}</span>
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
              <div className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-xl text-center">
                <p className="font-semibold text-deep-river">
                  {trips.length === 0
                    ? t('No departures on this day.')
                    : t('No departures match these filters.')}
                </p>
                <p className="mt-1 text-sm text-on-surface-variant">
                  {trips.length === 0
                    ? t('Try another day in the date strip above, or change the piers.')
                    : t('Try “All Times” and “All Types” to see every departure.')}
                </p>
              </div>
            )}
          </div>
          <CorridorSidebar />
        </div>

      </div>
    </div>
  )
}
