import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { ErrorState, PageLoader } from '../../components/ui'
import { CONTACT } from '../../constant/contact'
import CorridorSidebar from '../../features/trips/components/CorridorSidebar'
import ReturnLegBar from '../../features/trips/components/ReturnLegBar'
import { isRoundTrip, leavesAfterArrival } from '../../features/booking/legs'
import type { Leg } from '../../features/booking/legs'
import { ROUTES } from '../../routes/routes'
import { useAppSelector } from '../../store/hooks'
import DateStrip from '../../features/trips/components/DateStrip'
import ResultFilters from '../../features/trips/components/ResultFilters'
import type { KindFilter, SortKey, TimeFilter } from '../../features/trips/components/ResultFilters'
import SearchForm from '../../features/trips/components/SearchForm'
import TripCard from '../../features/trips/components/TripCard'
import { formatDateLabel, isDeparted } from '../../features/trips/utils'
import { useSearchResults } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

export default function SearchResultsPage() {
  useDocumentTitle('Available Departures')
  const [searchParams] = useSearchParams()
  const outboundQuery = useAppSelector((s) => s.booking.query)
  const outboundTrip = useAppSelector((s) => s.booking.selectedTrip)
  const roundTrip = isRoundTrip(outboundQuery)
  // `?leg=return`: the return-trip step of a round trip; the same page lists the trips back.
  const leg: Leg = roundTrip && searchParams.get('leg') === 'return' ? 'return' : 'outbound'
  const { query, updateQuery, piers, dates: allDates, counts, trips, searching, loading, error, retry, chooseTrip } =
    useSearchResults(leg)
  // The way back can only be on the outbound day or later.
  const dates = useMemo(
    () => (leg === 'return' ? allDates?.filter((d) => d.id >= outboundQuery.dateId) : allDates),
    [leg, allDates, outboundQuery.dateId],
  )
  const [time, setTime] = useState<TimeFilter>('all')
  // `?type=sightseeing` (from the Explore page) opens the list already filtered to sightseeing trips.
  const [kind, setKind] = useState<KindFilter>(() =>
    searchParams.get('type') === 'sightseeing' ? 'sightseeing' : 'all',
  )
  const [sort, setSort] = useState<SortKey>('time')

  // Vào trang khi hôm nay đã hết chuyến thì chuyển sang ngày gần nhất còn chuyến (một lần).
  const triedAutoMove = useRef(false)
  // Khách đã tự bấm chọn một ngày: không cần giải thích vì sao trang không hiện hôm nay nữa.
  const [pickedDay, setPickedDay] = useState(false)
  useEffect(() => {
    if (leg === 'return' || triedAutoMove.current || !counts || !dates) return
    triedAutoMove.current = true
    const today = dates.find((d) => d.isToday)
    if (!today || query.dateId !== today.id || counts[today.id] !== 0) return
    const next = dates.find((d) => (counts[d.id] ?? 0) > 0)
    if (!next) return
    updateQuery({ dateId: next.id })
    // `updateQuery` được tạo lại mỗi lần render; chỉ cần chạy khi số chuyến vừa tải xong.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [counts, dates])

  // Trang vừa tự chuyển khỏi hôm nay (đã hết chuyến) và khách chưa chọn ngày khác: nói rõ hôm nay là
  // ngày nào. Khách tự chọn ngày thì không cần câu này.
  const todayOption = dates?.find((d) => d.isToday)
  const nearestOption = counts && dates ? dates.find((d) => (counts[d.id] ?? 0) > 0) : undefined
  const nearestDay =
    !pickedDay && todayOption && counts && counts[todayOption.id] === 0 && nearestOption?.id === query.dateId
      ? nearestOption
      : undefined
  // Every offered day is known to have nothing to book: advising "pick another day" would lead nowhere.
  const noTripsAtAll = !!counts && !!dates && dates.length > 0 && dates.every((d) => counts[d.id] === 0)
  /** A return trip leaving before the outbound one arrives cannot be taken (one person, two boats). */
  const tooEarly = (trip: { departTime: string }) =>
    leg === 'return' &&
    outboundTrip !== null &&
    !leavesAfterArrival(
      { dateId: outboundQuery.dateId, arriveTime: outboundTrip.arriveTime },
      { dateId: query.dateId, departTime: trip.departTime },
    )

  // Giá chuyến thường như nhau nên chỉ cho sắp xếp theo giá khi các chuyến đang xem có giá khác nhau.
  const priceVaries = useMemo(() => new Set((trips ?? []).map((trip) => trip.fareVnd)).size > 1, [trips])

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
    const sorted = [...list].sort(by[priceVaries || sort !== 'price' ? sort : 'time'])
    // Chuyến đã khởi hành xuống cuối danh sách, chuyến còn đặt được ở trên.
    return [
      ...sorted.filter((trip) => !isDeparted(trip, query.dateId)),
      ...sorted.filter((trip) => isDeparted(trip, query.dateId)),
    ]
  }, [trips, time, kind, sort, priceVaries, query.dateId])

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !piers || !dates || !trips) return <PageLoader />

  const pier = (id: string) => piers.find((p) => p.id === id) ?? piers[0]
  const selectedDate = dates.find((d) => d.id === query.dateId) ?? dates[0]
  // Chuyến đặt được: chưa khởi hành và còn chỗ. `dayBookable` đếm cả ngày (khớp số trên thanh chọn
  // ngày), `bookableCount` chỉ đếm các chuyến đang hiện sau khi lọc.
  const isBookable = (trip: (typeof visible)[number]) =>
    !isDeparted(trip, query.dateId) && trip.seatsCount > 0 && !tooEarly(trip)
  const dayBookable = (trips ?? []).filter(isBookable).length
  const bookableCount = visible.filter(isBookable).length

  return (
    <div className="bg-mist pb-16 pt-4">
      <div className="mx-auto flex max-w-7xl flex-col gap-space-xl px-margin">
        <section className="flex w-full flex-col gap-space-md">
          <div className="flex flex-col justify-between gap-space-md pt-2 md:flex-row md:items-end">
            <div>
              {roundTrip && (
                <span className="mb-1 inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
                  {leg === 'return' ? t('Return trip · step 2 of 2') : t('Outbound trip · step 1 of 2')}
                </span>
              )}
              <h1 className="font-headline-lg text-2xl font-bold leading-tight tracking-tight text-deep-river md:text-3xl lg:text-[34px]">
                {leg === 'return' ? t('Choose your return trip') : t('Available Departures')}
              </h1>
              <p className="mt-1 text-sm text-on-surface-variant">
                {leg === 'return'
                  ? t('Pick the trip back. It can start at another pier and on another day.')
                  : t('Select your river crossing across the Saigon River transit corridor.')}
              </p>
            </div>
          </div>

          {leg === 'return' ? <ReturnLegBar piers={piers} /> : <SearchForm piers={piers} dates={dates} variant="bar" />}
          {noTripsAtAll && dates && (
            <p
              role="status"
              className="rounded-2xl border border-signal-amber/40 bg-signal-amber/10 px-space-md py-3 text-sm text-deep-river"
            >
              <span className="font-semibold">
                {t('There are no trips in the next {days} days.', { days: dates.length })}
              </span>{' '}
              {t('Try another pair of piers, or call the hotline for the timetable:')}{' '}
              <a href={CONTACT.hotlineHref} className="tap-area whitespace-nowrap font-semibold text-teal-flow hover:underline">
                {CONTACT.hotline}
              </a>
            </p>
          )}
          {nearestDay && (
            <p
              role="status"
              className="rounded-2xl border border-signal-amber/40 bg-signal-amber/10 px-space-md py-3 text-sm text-deep-river"
            >
              {t('No more trips today ({today}), so the nearest trips are shown: {date}.', {
                today: todayOption ? t('{weekday}, {label}', { weekday: todayOption.weekday, label: todayOption.label }) : '',
                date: formatDateLabel(nearestDay),
              })}
            </p>
          )}
          <DateStrip
            dates={dates}
            selectedId={selectedDate.id}
            tripCount={dayBookable}
            counts={counts}
            onSelect={(id) => {
              setPickedDay(true)
              updateQuery({ dateId: id })
            }}
          />
          <ResultFilters
            time={time}
            kind={kind}
            sort={sort}
            onTime={setTime}
            onKind={setKind}
            onSort={setSort}
            showPriceSort={priceVaries}
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
                  {t('{length} trips with seats left', { length: bookableCount })}
                </span>
              </div>
            </div>

            {visible.length > 0 && dayBookable === 0 && !noTripsAtAll && (
              <p
                role="status"
                className="rounded-2xl border border-signal-amber/40 bg-signal-amber/10 px-space-md py-3 text-sm text-deep-river"
              >
                {t('No trips left to book on this day. Choose another day above.')}
              </p>
            )}
            {visible.length > 0 ? (
              visible.map((trip) => (
                <TripCard
                  key={trip.id}
                  trip={trip}
                  origin={pier(query.originId)}
                  destination={pier(query.destinationId)}
                  onSelect={chooseTrip}
                  departed={isDeparted(trip, query.dateId)}
                  to={leg === 'return' ? `${ROUTES.seatSelection}?leg=return` : undefined}
                  blockedReason={tooEarly(trip) ? t('Leaves before your outbound trip arrives') : undefined}
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
                    ? noTripsAtAll
                      ? t('Try another pair of piers or call the hotline.')
                      : t('Try another day in the date strip above, or change the piers.')
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
