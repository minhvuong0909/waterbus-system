import { useEffect, useMemo } from 'react'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { combineFetches, useFetch } from '../../../hooks'
import { selectReturnTrip, selectTrip, setQuery, setReturnQuery } from '../../booking/bookingSlice'
import { legView } from '../../booking/legs'
import type { Leg } from '../../booking/legs'
import {
  getDateOptions,
  getDayTripCounts,
  getNetworkRoutes,
  getPierOptions,
  getTripDetail,
  getTrips,
} from '../services/tripService'
import type { DateOption, PierOption, SearchQuery, Trip } from '../types'
import { pickPiers } from '../utils'

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
  const routes = useFetch(getNetworkRoutes)
  const dates = useFetch(getDateOptions)
  useDefaultPiers(piers.data, 'outbound')
  useValidDate(dates.data, 'outbound')
  return {
    piers: piers.data,
    routes: routes.data,
    dates: dates.data,
    ...combineFetches(piers, routes, dates),
  }
}

export function useSearchResults(leg: Leg = 'outbound') {
  const { query, updateQuery } = useSearchQuery(leg)
  const dispatch = useAppDispatch()
  const piers = useFetch(getPierOptions)
  const dates = useFetch(getDateOptions)
  useDefaultPiers(piers.data, leg)
  useValidDate(dates.data, leg)

  // Search again whenever the piers or the day change. Until a pier pair is chosen the request
  // simply stays pending, so the page keeps showing its loader instead of an empty list.
  const ready = query.originId !== '' && query.destinationId !== '' && query.dateId !== ''
  const trips = useFetch(
    () => (ready ? getTrips(query) : new Promise<Trip[]>(() => undefined)),
    [ready, query.originId, query.destinationId, query.dateId],
  )
  // Số chuyến còn bán của từng ngày; chỉ để hiện trên thanh chọn ngày nên lỗi không chặn trang.
  const counts = useFetch(
    () =>
      ready && dates.data
        ? getDayTripCounts(query, dates.data)
        : new Promise<Record<string, number>>(() => undefined),
    [ready, query.originId, query.destinationId, dates.data?.length],
  )
  return {
    query,
    updateQuery,
    piers: piers.data,
    dates: dates.data,
    counts: counts.data,
    trips: trips.data,
    searching: trips.fetching,
    ...combineFetches(piers, dates, trips),
    chooseTrip: (trip: Trip) => dispatch(leg === 'outbound' ? selectTrip(trip) : selectReturnTrip(trip)),
  }
}

/** The chosen trip of a leg, with its real stops and fares for that leg's piers. */
export function useTripDetail(leg: Leg = 'outbound') {
  const booking = useAppSelector((s) => s.booking)
  const view = useMemo(() => legView(booking, leg), [booking, leg])
  const detail = useFetch(() => getTripDetail(view.trip, view.tripId, view.query), [leg, view.tripId])
  return {
    detail: detail.data,
    loading: detail.loading,
    error: detail.error,
    retry: detail.retry,
    /** No trip chosen in this session (the page was opened directly). */
    noTrip: !view.tripId,
  }
}
