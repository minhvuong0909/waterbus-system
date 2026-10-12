import { useEffect, useMemo, useState } from 'react'
import { subscribeSeatEvents } from '../services/seatEvents'
import type { SeatMap, SeatStatus } from '../types'

/**
 * The seat map with hold changes from other travelers applied as they arrive. Only seats that
 * were not sold when the map loaded can change. Pass `enabled = false` on pages that do not
 * need live updates.
 */
export function useLiveSeatMap(
  seatMap: SeatMap | undefined,
  tripId: string | null,
  enabled: boolean,
): SeatMap | undefined {
  const [live, setLive] = useState<{ map: SeatMap; status: Record<string, SeatStatus> }>()

  useEffect(() => {
    if (!enabled || !seatMap || !tripId) return
    const freeIds = seatMap.seats.filter((s) => s.status === 'available').map((s) => s.id)
    return subscribeSeatEvents(tripId, freeIds, (event) => {
      const status: SeatStatus = event.type === 'SeatLocked' ? 'held' : 'available'
      setLive((current) => ({
        map: seatMap,
        // Changes recorded for an older map are dropped.
        status: { ...(current?.map === seatMap ? current.status : {}), [event.seatId]: status },
      }))
    })
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
