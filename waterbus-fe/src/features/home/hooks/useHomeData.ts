import { combineFetches, useFetch } from '../../../hooks'
import { getCorridorMap, getHomeData, getNextDepartures } from '../services/homeService'

export function useHomeData() {
  const home = useFetch(getHomeData)
  const departures = useFetch(getNextDepartures)
  const corridor = useFetch(getCorridorMap)

  return {
    home: home.data,
    departures: departures.data,
    corridor: corridor.data,
    ...combineFetches(home, departures, corridor),
  }
}
