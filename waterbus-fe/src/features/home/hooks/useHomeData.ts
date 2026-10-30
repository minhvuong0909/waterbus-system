import { useAppSelector } from '../../../store/hooks'
import { useFetch } from '../../../hooks'
import { getCorridorMap, getHomeData, getNextDepartures } from '../services/homeService'

/**
 * The three sources of the home page, kept apart so that one failing request (e.g. the stations)
 * only hides its own sections instead of the whole page.
 */
export function useHomeData() {
  const home = useFetch(getHomeData)
  // Same piers as the search form, so the timetable page opened from here shows the same trips.
  const { originId, destinationId } = useAppSelector((s) => s.booking.query)
  const departures = useFetch(() => getNextDepartures({ originId, destinationId }), [originId, destinationId])
  const corridor = useFetch(getCorridorMap)
  return { home, departures, corridor }
}
