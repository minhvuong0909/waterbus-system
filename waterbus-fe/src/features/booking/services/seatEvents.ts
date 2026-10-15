import type { SeatEvent } from '../types'

export type SeatEventListener = (event: SeatEvent) => void

const TICK_MS = 4000

/**
 * Mock stand-in for the SignalR `BookingHub` (roadmap P2-BE-01, not built yet). It imitates other
 * travelers: every few seconds it holds a free seat or releases one it held, and reports it with
 * the same `SeatLocked` / `SeatUnlocked` events the hub will send. Replace the body with a
 * `HubConnection` that joins the trip's group; callers only depend on this signature.
 *
 * @param seatIds seats that were free when the map loaded, the only ones the mock plays with
 * @returns a function that stops the updates
 */
export function subscribeSeatEvents(
  tripId: string,
  seatIds: string[],
  listener: SeatEventListener,
): () => void {
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
