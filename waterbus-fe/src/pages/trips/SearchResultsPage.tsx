import { useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { Reveal } from '../../components/motion'
import { Button, ErrorState, PageLoader, Tabs } from '../../components/ui'
import type { TabItem } from '../../components/ui'
import { CONTACT } from '../../constant/contact'
import ReturnLegBar from '../../features/booking/components/ReturnLegBar'
import { isRoundTrip, leavesAfterArrival } from '../../features/booking/legs'
import type { Leg } from '../../features/booking/legs'
import { ROUTES } from '../../routes/routes'
import { useAppSelector } from '../../store/hooks'
import ChoiceDropdown from '../../features/trips/components/ChoiceDropdown'
import DateStrip from '../../features/trips/components/DateStrip'
import SearchForm from '../../features/booking/components/SearchForm'
import TripCard from '../../features/trips/components/TripCard'
import { getNextTripDate } from '../../features/trips/services/tripService'
import type { TimeBand } from '../../features/trips/types'
import { formatDateLabel, isDeparted } from '../../features/trips/utils'
import { useSearchResults } from '../../features/booking/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

type SortKey = 'time' | 'latest' | 'speed' | 'price'
type TimeFilter = 'any' | TimeBand

const SORTS: { id: SortKey; label: string }[] = [
  { id: 'time', label: 'Earliest Departure' },
  { id: 'latest', label: 'Latest Departure' },
  { id: 'speed', label: 'Fastest Crossing' },
  { id: 'price', label: 'Lowest Fare' },
]

/** Khung giờ khởi hành khớp `timeBandOf`: sáng trước 12:00, chiều 12:00–17:00, tối từ 17:00. */
const TIME_FILTERS: { id: TimeFilter; label: string }[] = [
  { id: 'any', label: 'Any time' },
  { id: 'morning', label: 'Morning (before 12:00)' },
  { id: 'afternoon', label: 'Afternoon (12:00–17:00)' },
  { id: 'sunset', label: 'Evening (after 17:00)' },
]

/** Số chỗ trống tối thiểu để lọc (đặt cho nhóm đi cùng nhau). */
const SEAT_FILTERS = [1, 2, 4, 6]

const SERVICE_TABS: TabItem<'regular' | 'sightseeing'>[] = [
  { key: 'regular', label: 'Regular trips' },
  { key: 'sightseeing', label: 'Sightseeing tours' },
]

export default function SearchResultsPage() {
  useDocumentTitle('Available Departures')
  const [searchParams, setSearchParams] = useSearchParams()
  const outboundQuery = useAppSelector((s) => s.booking.query)
  const outboundTrip = useAppSelector((s) => s.booking.selectedTrip)
  const roundTrip = isRoundTrip(outboundQuery)
  // `?leg=return`: the return-trip step of a round trip; the same page lists the trips back.
  const leg: Leg = roundTrip && searchParams.get('leg') === 'return' ? 'return' : 'outbound'
  // `?type=sightseeing` (from the Explore page) lists the sightseeing tours only; each runs its whole route.
  const sightseeingOnly = searchParams.get('type') === 'sightseeing'
  const {
    query, updateQuery, piers, dates, strip, canPrev, canNext, shift, counts, trips, searching, loading, error, retry, chooseTrip,
  } = useSearchResults(leg, sightseeingOnly)
  const [sort, setSort] = useState<SortKey>('time')
  const [timeFilter, setTimeFilter] = useState<TimeFilter>('any')
  const [minSeats, setMinSeats] = useState(1)
  const [finding, setFinding] = useState(false)
  const [nextDayMissing, setNextDayMissing] = useState(false)

  // Ngày đang xem trống: nhảy tới ngày gần nhất sau đó còn chuyến (có thể nằm ngoài tuần đang hiện).
  const goToNextDay = async () => {
    setFinding(true)
    setNextDayMissing(false)
    try {
      const day = await getNextTripDate(query, sightseeingOnly, query.dateId)
      if (day && dates?.some((d) => d.id === day)) updateQuery({ dateId: day })
      else setNextDayMissing(true)
    } catch {
      setNextDayMissing(true)
    } finally {
      setFinding(false)
    }
  }

  // Vào trang khi hôm nay đã hết chuyến thì chuyển sang ngày gần nhất còn chuyến (một lần).
  const triedAutoMove = useRef(false)
  // Khách đã tự bấm chọn một ngày: không cần giải thích vì sao trang không hiện hôm nay nữa.
  const [pickedDay, setPickedDay] = useState(false)
  useEffect(() => {
    if (leg === 'return' || triedAutoMove.current || !counts || !strip) return
    triedAutoMove.current = true
    const today = strip.find((d) => d.isToday)
    if (!today || query.dateId !== today.id || counts[today.id] !== 0) return
    const next = strip.find((d) => (counts[d.id] ?? 0) > 0)
    if (!next) return
    updateQuery({ dateId: next.id })
    // `updateQuery` được tạo lại mỗi lần render; chỉ cần chạy khi số chuyến vừa tải xong.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [counts, strip])

  // Trang vừa tự chuyển khỏi hôm nay (đã hết chuyến) và khách chưa chọn ngày khác: nói rõ hôm nay là
  // ngày nào. Khách tự chọn ngày thì không cần câu này.
  const todayOption = strip?.find((d) => d.isToday)
  const nearestOption = counts && strip ? strip.find((d) => (counts[d.id] ?? 0) > 0) : undefined
  const nearestDay =
    !pickedDay && todayOption && counts && counts[todayOption.id] === 0 && nearestOption?.id === query.dateId
      ? nearestOption
      : undefined
  // Every offered day is known to have nothing to book: advising "pick another day" would lead nowhere.
  // Only the first week is read, so "nothing at all" is claimed only while the strip is at the start of the window.
  const noTripsAtAll = !!counts && !!strip && !canPrev && strip.length > 0 && strip.every((d) => counts[d.id] === 0)
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
    // Lọc theo khung giờ và số chỗ trống; chuyến hết chỗ vẫn hiện khi không đặt điều kiện chỗ (để thấy "Sold out").
    const list = trips.filter(
      (trip) => (timeFilter === 'any' || trip.band === timeFilter) && (minSeats <= 1 || trip.seatsCount >= minSeats),
    )
    const by: Record<SortKey, (a: (typeof list)[number], b: (typeof list)[number]) => number> = {
      time: (a, b) => a.departTime.localeCompare(b.departTime),
      latest: (a, b) => b.departTime.localeCompare(a.departTime),
      speed: (a, b) => (a.durationKnown === false ? Infinity : a.durationMins) - (b.durationKnown === false ? Infinity : b.durationMins),
      price: (a, b) => a.fareVnd - b.fareVnd,
    }
    const sorted = [...list].sort(by[priceVaries || sort !== 'price' ? sort : 'time'])
    // Chuyến đã khởi hành xuống cuối danh sách, chuyến còn đặt được ở trên.
    return [
      ...sorted.filter((trip) => !isDeparted(trip, query.dateId)),
      ...sorted.filter((trip) => isDeparted(trip, query.dateId)),
    ]
  }, [trips, sort, priceVaries, query.dateId, timeFilter, minSeats])

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
      <div className="mx-auto flex max-w-5xl flex-col gap-space-lg px-margin">
        <section className="flex w-full flex-col gap-space-md">
          <div className="flex flex-col justify-between gap-space-md pt-2 md:flex-row md:items-end">
            <div>
              {roundTrip && (
                <span className="mb-1 inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
                  {leg === 'return' ? t('Return trip · step 2 of 2') : t('Outbound trip · step 1 of 2')}
                </span>
              )}
              <h1 className="font-display text-2xl font-bold leading-tight tracking-tight text-deep-river md:text-3xl lg:text-4xl">
                {leg === 'return' ? t('Choose your return trip') : t('Available Departures')}
              </h1>
              <p className="mt-1 text-sm text-on-surface-variant">
                {leg === 'return'
                  ? t('Pick the trip back. It can start at another pier and on another day.')
                  : sightseeingOnly
                    ? t('See the river sightseeing tours running on the day you choose.')
                    : t('Select your river crossing across the Saigon River transit corridor.')}
              </p>
            </div>
          </div>

          <Tabs
            label="Trip type"
            tabs={SERVICE_TABS}
            value={sightseeingOnly ? 'sightseeing' : 'regular'}
            onChange={(key) =>
              setSearchParams((params) => {
                if (key === 'sightseeing') params.set('type', 'sightseeing')
                else params.delete('type')
                return params
              })
            }
          >
            {leg === 'return' ? (
              <ReturnLegBar piers={piers} tour={sightseeingOnly} />
            ) : (
              <SearchForm piers={piers} dates={dates} variant="bar" submit={false} tour={sightseeingOnly} />
            )}
          </Tabs>
          {noTripsAtAll && strip && (
            <p
              role="status"
              className="rounded-2xl border border-signal-amber/40 bg-signal-amber/10 px-space-md py-3 text-sm text-deep-river"
            >
              <span className="font-semibold">
                {t('There are no trips in the next {days} days.', { days: strip.length })}
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
        </section>

        {/* Dính dưới thanh đầu trang (từ màn hình vừa trở lên) để đổi ngày được ngay khi đang xem danh sách dài. */}
        <div data-sticky-top className="z-20 -my-2 bg-mist/90 py-2 backdrop-blur md:sticky md:top-20">
          <DateStrip
            dates={strip ?? []}
            onShift={shift}
            canPrev={canPrev}
            canNext={canNext}
            selectedId={selectedDate.id}
            tripCount={dayBookable}
            counts={counts}
            onSelect={(id) => {
              setPickedDay(true)
              updateQuery({ dateId: id })
            }}
          />
        </div>

        <section className={`flex flex-col gap-space-md transition-opacity ${searching ? 'opacity-60' : ''}`}>
          <div className="flex flex-wrap items-center justify-between gap-space-sm px-1">
            <p className="text-sm font-bold text-teal-flow">
              {t('{length} trips with seats left', { length: bookableCount })}
            </p>
            {trips.length > 1 && (
              <div className="flex flex-wrap items-center gap-space-sm">
                <ChoiceDropdown
                  label={t('Departs:')}
                  icon="schedule"
                  options={TIME_FILTERS.map((f) => ({ id: f.id, label: t(f.label) }))}
                  value={timeFilter}
                  onChange={(id) => setTimeFilter(id as TimeFilter)}
                />
                <ChoiceDropdown
                  label={t('Seats:')}
                  icon="event_seat"
                  options={SEAT_FILTERS.map((n) => ({
                    id: String(n),
                    label: n === 1 ? t('Any') : t('{count} or more free', { count: n }),
                  }))}
                  value={String(minSeats)}
                  onChange={(id) => setMinSeats(Number(id))}
                />
                {visible.length > 1 && (
                  <ChoiceDropdown
                    label={t('Sort:')}
                    icon="swap_vert"
                    options={SORTS.filter((s) => s.id !== 'price' || priceVaries).map((s) => ({
                      id: s.id,
                      label: t(s.label),
                    }))}
                    value={sort}
                    onChange={(id) => setSort(id as SortKey)}
                  />
                )}
              </div>
            )}
          </div>

          <aside
            aria-label={t('Before you book')}
            className="rounded-2xl border border-teal-flow/20 bg-white px-space-md py-3 text-xs leading-relaxed text-on-surface-variant"
          >
            <p className="font-semibold text-deep-river">{t('Before you book')}</p>
            <ul className="mt-1 list-disc space-y-0.5 pl-4">
              <li>{t('Check in at your boarding pier before its check-in window closes, or you cannot board.')}</li>
              <li>{t('Tickets are issued once payment is confirmed. Seats are held for a limited time while you pay.')}</li>
              <li>{t('If a trip is cancelled, 100% of what you paid for the affected tickets is refunded.')}</li>
              <li>
                {t('Need help? Call')}{' '}
                <a href={CONTACT.hotlineHref} className="tap-area whitespace-nowrap font-semibold text-teal-flow hover:underline">
                  {CONTACT.hotline}
                </a>
              </li>
            </ul>
          </aside>

            {visible.length > 0 && dayBookable === 0 && !noTripsAtAll && (
              <p
                role="status"
                className="rounded-2xl border border-signal-amber/40 bg-signal-amber/10 px-space-md py-3 text-sm text-deep-river"
              >
                {t('No trips left to book on this day. Choose another day above.')}
              </p>
            )}
            {visible.length > 0 ? (
              <Reveal className="contents">
              {visible.map((trip) => (
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
              ))}
              </Reveal>
            ) : trips.length > 0 ? (
              <div className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-xl text-center">
                <p className="font-semibold text-deep-river">{t('No trips match these filters.')}</p>
                <Button
                  size="sm"
                  variant="secondary"
                  className="mt-3"
                  onClick={() => {
                    setTimeFilter('any')
                    setMinSeats(1)
                  }}
                >
                  {t('Clear filters')}
                </Button>
              </div>
            ) : (
              <div className="rounded-2xl border border-dashed border-outline-variant bg-white p-space-xl text-center">
                <p className="font-semibold text-deep-river">
                  {sightseeingOnly
                    ? t('No sightseeing trips on this day.')
                    : t('No departures on this day.')}
                </p>
                <p className="mt-1 text-sm text-on-surface-variant">
                  {noTripsAtAll
                    ? t('Try another pair of piers or call the hotline.')
                    : t('Try another day in the date strip above, or change the piers.')}
                </p>
                <Button size="sm" variant="secondary" icon="calendar_today" className="mt-3" disabled={finding} onClick={goToNextDay}>
                  {t('Next day with trips')}
                </Button>
                {nextDayMissing && (
                  <p className="mt-2 text-xs text-on-surface-variant">{t('No later day has trips yet.')}</p>
                )}
              </div>
            )}
        </section>
      </div>
    </div>
  )
}
