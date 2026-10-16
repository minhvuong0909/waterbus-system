import { useEffect, useMemo, useState } from 'react'
import { subscribeSeatEvents } from '../services/seatEvents'
import type { SeatEvent, SeatMap, SeatStatus } from '../types'

/** True when a change on the event's segment affects the journey of the seat map. */
function affectsJourney(event: SeatEvent, journey: SeatMap['journey']): boolean {
  if (!journey || event.boardingVisitOrder === undefined || event.disembarkingVisitOrder === undefined) {
    return true
  }
  // Segments [a, b) and [c, d) overlap when a < d and b > c; consecutive segments do not.
  return (
    event.boardingVisitOrder < journey.disembarkingVisitOrder &&
    event.disembarkingVisitOrder > journey.boardingVisitOrder
  )
}

/**
 * The seat map with changes from other travelers applied as they arrive. Only changes on the
 * traveler's own journey segment count. Pass `enabled = false` on pages that do not need live
 * updates. `reload` runs after the connection was lost and came back, to fetch a fresh snapshot.
 */
export function useLiveSeatMap(
  seatMap: SeatMap | undefined,
  tripId: string | null,
  enabled: boolean,
  reload?: () => void,
): SeatMap | undefined {
  const [live, setLive] = useState<{ map: SeatMap; status: Record<string, SeatStatus> }>()

  useEffect(() => {
    if (!enabled || !seatMap || !tripId) return
    const freeIds = seatMap.seats.filter((s) => s.status === 'available').map((s) => s.id)
    return subscribeSeatEvents(
      tripId,
      freeIds,
      (event) => {
        if (!affectsJourney(event, seatMap.journey)) return
        const status: SeatStatus =
          event.type === 'SeatSold' ? 'sold' : event.type === 'SeatLocked' ? 'held' : 'available'
        setLive((current) => ({
          map: seatMap,
          // Changes recorded for an older map are dropped.
          status: { ...(current?.map === seatMap ? current.status : {}), [event.seatId]: status },
        }))
      },
      reload,
    )
    // `reload` is re-created on every render; the subscription only depends on the map and trip.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [seatMap, tripId, enabled])

  return useMemo(() => {
    if (!seatMap || !enabled || live?.map !== seatMap) return seatMap
    return {
      ...seatMap,
      seats: seatMap.seats.map((seat) =>
        live.status[seat.id] ? { ...seat, status: live.status[seat.id] } : seat,
      ),
    }
  }, [seatMap, enabled, live])
}
