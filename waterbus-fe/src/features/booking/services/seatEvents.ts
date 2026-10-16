import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { connectHub } from '../../../services/realtime'
import type { SeatEvent } from '../types'
import { isGuid } from '../utils'

export type SeatEventListener = (event: SeatEvent) => void

const TICK_MS = 4000

/** Payload of `seatHeld` / `seatReleased` / `seatSold` (see docs/REALTIME_CONTRACT.md). */
interface SeatHubEvent {
  tripId: string
  seatId: string
  boardingVisitOrder: number
  disembarkingVisitOrder: number
  status: string
  expiresAt: string | null
}

/**
 * Seat changes of a trip as other travelers make them. Uses the seat-sync hub when the trip and
 * its seats come from the backend (Guid ids); the mock seat map uses seat codes, so it keeps the mock.
 *
 * Each hub event names the journey segment it applies to (`boardingVisitOrder`..
 * `disembarkingVisitOrder`); the listener receives those orders so a change on a segment that does
 * not overlap the traveler's journey can be ignored.
 *
 * @param seatIds seats that were free when the map loaded
 * @param onReconnected called after a dropped connection is back: the map must be reloaded with
 *   REST, because events sent while it was down are not replayed
 * @returns a function that stops the updates
 */
export function subscribeSeatEvents(
  tripId: string,
  seatIds: string[],
  listener: SeatEventListener,
  onReconnected?: () => void,
): () => void {
  if (!USE_MOCK && isGuid(tripId) && seatIds.length > 0 && seatIds.every(isGuid)) {
    return subscribeHub(tripId, listener, onReconnected)
  }
  return subscribeMock(tripId, seatIds, listener)
}

function subscribeHub(
  tripId: string,
  listener: SeatEventListener,
  onReconnected?: () => void,
): () => void {
  const forward = (type: SeatEvent['type']) => (event: SeatHubEvent) => {
    if (event.tripId.toLowerCase() === tripId.toLowerCase()) {
      listener({
        type,
        tripId,
        seatId: event.seatId,
        boardingVisitOrder: event.boardingVisitOrder,
        disembarkingVisitOrder: event.disembarkingVisitOrder,
      })
    }
  }
  return connectHub(HUBS.seatSync, {
    handlers: {
      [HUB_EVENTS.seatHeld]: forward('SeatLocked'),
      [HUB_EVENTS.seatReleased]: forward('SeatUnlocked'),
      [HUB_EVENTS.seatSold]: forward('SeatSold'),
    },
    onConnected: (connection) => connection.invoke('JoinTrip', tripId),
    onReconnected,
  })
}

/**
 * Mock stand-in for the hub: imitates other travelers by holding a free seat or releasing one it
 * held every few seconds.
 */
function subscribeMock(tripId: string, seatIds: string[], listener: SeatEventListener): () => void {
  const free = [...seatIds]
  const held: string[] = []

  const timer = window.setInterval(() => {
    const release = held.length > 0 && (free.length === 0 || Math.random() < 0.4)
    const pool = release ? held : free
    if (pool.length === 0) return
    const [seatId] = pool.splice(Math.floor(Math.random() * pool.length), 1)
    ;(release ? free : held).push(seatId)
    listener({ type: release ? 'SeatUnlocked' : 'SeatLocked', tripId, seatId })
  }, TICK_MS)

  return () => window.clearInterval(timer)
}
