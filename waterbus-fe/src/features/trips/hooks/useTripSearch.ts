import { useEffect } from 'react'
import { useAppDispatch, useAppSelector } from '../../../store/hooks'
import { combineFetches, useFetch } from '../../../hooks'
import { selectTrip, setQuery } from '../../booking/bookingSlice'
import {
  getDateOptions,
  getNetworkRoutes,
  getPierOptions,
  getTripDetail,
  getTrips,
} from '../services/tripService'
import type { PierOption, SearchQuery, Trip } from '../types'
import { pickPiers } from '../utils'

export function useSearchQuery() {
  const dispatch = useAppDispatch()
  const query = useAppSelector((s) => s.booking.query)
  return { query, updateQuery: (patch: Partial<SearchQuery>) => dispatch(setQuery(patch)) }
}

/** Fills in the departure and arrival pier once the piers are known, if none is chosen yet. */
function useDefaultPiers(piers: PierOption[] | undefined) {
  const { query, updateQuery } = useSearchQuery()
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

export function useSearchForm() {
  const piers = useFetch(getPierOptions)
  const routes = useFetch(getNetworkRoutes)
  const dates = useFetch(getDateOptions)
  useDefaultPiers(piers.data)
  return {
    piers: piers.data,
    routes: routes.data,
    dates: dates.data,
    ...combineFetches(piers, routes, dates),
  }
}

export function useSearchResults() {
  const { query, updateQuery } = useSearchQuery()
  const dispatch = useAppDispatch()
  const piers = useFetch(getPierOptions)
  const dates = useFetch(getDateOptions)
  useDefaultPiers(piers.data)

  // Search again whenever the piers or the day change. Until a pier pair is chosen the request
  // simply stays pending, so the page keeps showing its loader instead of an empty list.
  const ready = query.originId !== '' && query.destinationId !== ''
  const trips = useFetch(
    () => (ready ? getTrips(query) : new Promise<Trip[]>(() => undefined)),
    [ready, query.originId, query.destinationId, query.dateId],
  )
  return {
    query,
    updateQuery,
    piers: piers.data,
    dates: dates.data,
    trips: trips.data,
    searching: trips.fetching,
    ...combineFetches(piers, dates, trips),
    chooseTrip: (trip: Trip) => dispatch(selectTrip(trip)),
  }
}

export function useTripDetail() {
  const query = useAppSelector((s) => s.booking.query)
  const selectedTripId = useAppSelector((s) => s.booking.selectedTripId)
  const selectedTrip = useAppSelector((s) => s.booking.selectedTrip)
  const detail = useFetch(() => getTripDetail(selectedTrip, selectedTripId, query))
  return { detail: detail.data, loading: detail.loading, error: detail.error, retry: detail.retry }
}
