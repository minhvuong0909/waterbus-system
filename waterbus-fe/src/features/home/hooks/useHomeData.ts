import { useAppSelector } from '../../../store/hooks'
import { useFetch } from '../../../hooks'
import { getCorridorMap, getNextDepartures } from '../services/homeService'

/**
 * The two sources of the home page, kept apart so that one failing request (e.g. the stations)
 * only hides its own section instead of the whole page.
 */
export function useHomeData() {
  // Same piers as the search form, so the timetable page opened from here shows the same trips.
  const { originId, destinationId } = useAppSelector((s) => s.booking.query)
  const departures = useFetch(() => getNextDepartures({ originId, destinationId }), [originId, destinationId])
  const corridor = useFetch(getCorridorMap)
  return { departures, corridor }
}
