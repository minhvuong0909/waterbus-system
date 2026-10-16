import { HUBS, HUB_EVENTS } from '../../../constant/apiEndpoints'
import { USE_MOCK } from '../../../constant/env'
import { connectHub } from '../../../services/realtime'
import type { SeatEvent } from '../types'

export type SeatEventListener = (event: SeatEvent) => void

const TICK_MS = 4000
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

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
 * Seat hold changes of a trip as other travelers make them. Uses the seat-sync hub when the trip and
 * its seats come from the backend (Guid ids); the mock seat map uses seat codes, so it keeps the mock.
 *
 * The hub reports each change for a journey segment (`boardingVisitOrder`..`disembarkingVisitOrder`);
 * this map has no segment yet, so any hold is shown as held.
 *
 * @param seatIds seats that were free when the map loaded
 * @returns a function that stops the updates
 */
export function subscribeSeatEvents(
  tripId: string,
  seatIds: string[],
  listener: SeatEventListener,
): () => void {
  if (!USE_MOCK && GUID.test(tripId) && seatIds.length > 0 && seatIds.every((id) => GUID.test(id))) {
    return subscribeHub(tripId, listener)
  }
  return subscribeMock(tripId, seatIds, listener)
}

function subscribeHub(tripId: string, listener: SeatEventListener): () => void {
  const forward = (type: SeatEvent['type']) => (event: SeatHubEvent) => {
    if (event.tripId.toLowerCase() === tripId.toLowerCase()) {
      listener({ type, tripId, seatId: event.seatId })
    }
  }
  return connectHub(HUBS.seatSync, {
    handlers: {
      [HUB_EVENTS.seatHeld]: forward('SeatLocked'),
      [HUB_EVENTS.seatReleased]: forward('SeatUnlocked'),
      [HUB_EVENTS.seatSold]: forward('SeatSold'),
    },
    onConnected: (connection) => connection.invoke('JoinTrip', tripId),
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
