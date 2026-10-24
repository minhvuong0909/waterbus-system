import { useAppSelector } from '../../../store/hooks'
import { combineFetches, useFetch } from '../../../hooks'
import { getCorridorMap, getHomeData, getNextDepartures } from '../services/homeService'

export function useHomeData() {
  const home = useFetch(getHomeData)
  // Same piers as the search form, so the timetable page opened from here shows the same trips.
  const { originId, destinationId } = useAppSelector((s) => s.booking.query)
  const departures = useFetch(() => getNextDepartures({ originId, destinationId }), [originId, destinationId])
  const corridor = useFetch(getCorridorMap)

  return {
    home: home.data,
    departures: departures.data,
    corridor: corridor.data,
    ...combineFetches(home, departures, corridor),
  }
}
