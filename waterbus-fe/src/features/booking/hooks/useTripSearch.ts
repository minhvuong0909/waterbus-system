import { useEffect, useMemo, useState } from 'react'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { combineFetches, useFetch } from '../../../hooks'
import { selectReturnTrip, selectTrip, setQuery, setReturnQuery } from '../bookingSlice'
import { legView } from '../legs'
import type { Leg } from '../legs'
import {
  getDateOptions,
  getDayTripCounts,
  getPierOptions,
  getSightseeingTours,
  getTrips,
} from '../../trips/services/tripService'
import type { DateOption, PierOption, SearchQuery, Trip } from '../../trips/types'
import { STRIP_DAYS, pickPiers } from '../../trips/utils'

/**
 * The search of one leg and how to change it. The outbound leg is the main search; the return
 * leg of a round trip has its own piers and day (BR-COM-03).
 */
export function useSearchQuery(leg: Leg = 'outbound') {
  const dispatch = useAppDispatch()
  // Select the stored pieces (stable references) and build the leg's query from them.
  const booking = useAppSelector((s) => s.booking)
  const query = useMemo(() => legView(booking, leg).query, [booking, leg])
  const updateQuery = (patch: Partial<SearchQuery>) => {
    if (leg === 'outbound') {
      dispatch(setQuery(patch))
      return
    }
    const { originId, destinationId, dateId } = patch
    dispatch(
      setReturnQuery({
        ...(originId !== undefined ? { originId } : {}),
        ...(destinationId !== undefined ? { destinationId } : {}),
        ...(dateId !== undefined ? { dateId } : {}),
      }),
    )
  }
  return { query, updateQuery }
}

/** Fills in the departure and arrival pier once the piers are known, if none is chosen yet. */
function useDefaultPiers(piers: PierOption[] | undefined, leg: Leg) {
  const { query, updateQuery } = useSearchQuery(leg)
  useEffect(() => {
    if (!piers || piers.length === 0) return
    const picked = pickPiers(piers, query)
    if (picked.originId !== query.originId || picked.destinationId !== query.destinationId) {
      updateQuery(picked)
    }
    // `updateQuery` is re-created on every render; only the piers and the chosen ids matter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [piers, query.originId, query.destinationId])
}

/** Moves the chosen day to the first offered day when it is not offered (e.g. it is already past). */
function useValidDate(dates: DateOption[] | undefined, leg: Leg) {
  const { query, updateQuery } = useSearchQuery(leg)
  useEffect(() => {
    if (dates && dates.length > 0 && !dates.some((d) => d.id === query.dateId)) {
      updateQuery({ dateId: dates[0].id })
    }
    // `updateQuery` is re-created on every render; only the offered days and the chosen one matter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dates, query.dateId])
}

export function useSearchForm() {
  const piers = useFetch(getPierOptions)
  const dates = useFetch(getDateOptions)
  useDefaultPiers(piers.data, 'outbound')
  useValidDate(dates.data, 'outbound')
  return {
    piers: piers.data,
    dates: dates.data,
    ...combineFetches(piers, dates),
  }
}

/**
 * The week shown in the date strip, which follows the chosen day: a day picked outside it (e.g. on the
 * calendar) moves the strip there. `shift` slides it by a week.
 */
function useDayStrip(dates: DateOption[] | undefined, selectedId: string) {
  const [start, setStart] = useState(0)
  const count = dates?.length ?? 0
  const last = Math.max(0, count - STRIP_DAYS)
  const selected = dates?.findIndex((d) => d.id === selectedId) ?? -1
  // Khi ngày chọn đổi và nằm ngoài tuần đang hiện (vd chọn từ lịch tháng) thì dời thanh đến đó.
  const [seen, setSeen] = useState(selected)
  if (seen !== selected) {
    setSeen(selected)
    if (selected >= 0 && (selected < start || selected >= start + STRIP_DAYS)) {
      setStart(Math.min(Math.max(selected - Math.floor(STRIP_DAYS / 2), 0), last))
    }
  }
  const from = Math.min(start, last)
  return {
    strip: dates?.slice(from, from + STRIP_DAYS),
    canPrev: from > 0,
    canNext: from < last,
    shift: (weeks: 1 | -1) => setStart(Math.min(Math.max(from + weeks * STRIP_DAYS, 0), last)),
  }
}

export function useSearchResults(leg: Leg = 'outbound', sightseeingOnly = false) {
  const { query, updateQuery } = useSearchQuery(leg)
  const dispatch = useAppDispatch()
  const outboundDateId = useAppSelector((s) => s.booking.query.dateId)
  const piers = useFetch(getPierOptions)
  const allDates = useFetch(getDateOptions)
  // The way back can only be on the outbound day or later.
  const dates = useMemo(
    () => (leg === 'return' ? allDates.data?.filter((d) => d.id >= outboundDateId) : allDates.data),
    [leg, allDates.data, outboundDateId],
  )
  useDefaultPiers(piers.data, leg)
  useValidDate(dates, leg)
  const dayStrip = useDayStrip(dates, query.dateId)

  // Search again whenever the piers or the day change. Until a pier pair is chosen the request
  // simply stays pending, so the page keeps showing its loader instead of an empty list.
  const ready = query.originId !== '' && query.destinationId !== '' && query.dateId !== ''
  // Chuyến ngắm cảnh đi trọn hành trình nên không phụ thuộc cặp bến đang chọn.
  const findTrips = (q: SearchQuery) => (sightseeingOnly ? getSightseeingTours(q.dateId) : getTrips(q))
  const trips = useFetch(
    () => (ready ? findTrips(query) : new Promise<Trip[]>(() => undefined)),
    [ready, sightseeingOnly, query.originId, query.destinationId, query.dateId],
  )
  // Số chuyến còn bán của các ngày đang hiện trên thanh chọn ngày; lỗi không chặn trang.
  const stripFrom = dayStrip.strip?.[0]?.id
  const counts = useFetch(
    () =>
      ready && dayStrip.strip
        ? getDayTripCounts(query, dayStrip.strip, findTrips)
        : new Promise<Record<string, number>>(() => undefined),
    [ready, sightseeingOnly, query.originId, query.destinationId, stripFrom],
  )
  return {
    query,
    updateQuery,
    piers: piers.data,
    dates,
    ...dayStrip,
    counts: counts.data,
    trips: trips.data,
    searching: trips.fetching,
    ...combineFetches(piers, allDates, trips),
    chooseTrip: (trip: Trip) => dispatch(leg === 'outbound' ? selectTrip(trip) : selectReturnTrip(trip)),
  }
}
