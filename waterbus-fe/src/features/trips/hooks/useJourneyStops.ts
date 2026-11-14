import { useFetch } from '../../../hooks'
import { getJourneyStops } from '../services/tripService'

/** The stops of a trip between two piers, with the time the boat is expected at each. */
export function useJourneyStops(tripId: string, originId: string, destinationId: string) {
  const stops = useFetch(
    () => getJourneyStops(tripId, originId, destinationId),
    [tripId, originId, destinationId],
  )
  return { stops: stops.data, loading: stops.loading, error: stops.error, retry: stops.retry }
}
