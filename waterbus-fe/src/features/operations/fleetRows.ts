import type { AdminTrip, LiveBoatPosition } from './types'

/** Vị trí thật cũ hơn mốc này coi là mất tín hiệu (tàu ngừng gửi vị trí). */
const STALE_AFTER_MS = 2 * 60_000

/** `live`: vị trí mới; `stale`: có vị trí nhưng đã cũ; `none`: tàu có chuyến mà chưa gửi vị trí nào. */
type FleetSignal = 'live' | 'stale' | 'none'

export interface FleetRow {
  boatId: string
  code: string
  signal: FleetSignal
  /** Chuyến đang chạy của tàu (Boarding, EnRoute, Suspended); `undefined` khi tàu không trong chuyến. */
  trip?: Pick<AdminTrip, 'id' | 'routeName' | 'departureTime' | 'status'>
  position?: LiveBoatPosition
}

const isActive = (trip: Pick<AdminTrip, 'status' | 'boatId'>) =>
  Boolean(trip.boatId) && (trip.status === 'Boarding' || trip.status === 'EnRoute' || trip.status === 'Suspended')

/**
 * Gộp chuyến đang chạy với vị trí GPS theo tàu. Tàu có chuyến mà chưa có vị trí vẫn hiện (`none`), vì đó là ca
 * điều phối cần biết nhất. Tàu có vị trí mà không có chuyến vẫn hiện để thấy tàu ngoài chuyến. Mất tín hiệu và
 * chưa có tín hiệu xếp trước.
 */
export function fleetRows(
  trips: Pick<AdminTrip, 'id' | 'boatId' | 'boatCode' | 'routeName' | 'departureTime' | 'status'>[],
  positions: LiveBoatPosition[],
  boatCodes: ReadonlyMap<string, string>,
  now: number,
): FleetRow[] {
  const positionOf = new Map(positions.map((p) => [p.boatId, p]))
  const rows = new Map<string, FleetRow>()
  const signalOf = (position?: LiveBoatPosition): FleetSignal =>
    !position ? 'none' : now - Date.parse(position.observedAt) > STALE_AFTER_MS ? 'stale' : 'live'
  const codeOf = (boatId: string, fallback?: string | null) => boatCodes.get(boatId) ?? fallback ?? boatId.slice(0, 8)

  for (const trip of trips.filter(isActive)) {
    const position = positionOf.get(trip.boatId)
    rows.set(trip.boatId, {
      boatId: trip.boatId,
      code: codeOf(trip.boatId, trip.boatCode),
      signal: signalOf(position),
      trip,
      position,
    })
  }
  for (const position of positions) {
    if (rows.has(position.boatId)) continue
    rows.set(position.boatId, {
      boatId: position.boatId,
      code: codeOf(position.boatId),
      signal: signalOf(position),
      position,
    })
  }
  const weight = (row: FleetRow) => (row.signal === 'live' ? 1 : 0)
  return [...rows.values()].sort((a, b) => weight(a) - weight(b) || a.code.localeCompare(b.code))
}
