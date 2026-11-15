import { describe, expect, it } from 'vitest'
import { fleetRows } from './fleetRows'
import type { LiveBoatPosition } from './types'

const NOW = Date.parse('2026-11-15T08:00:00Z')
const trip = (boatId: string, status: 'EnRoute' | 'Scheduled' | 'Suspended' | 'Completed' = 'EnRoute') => ({
  id: `t-${boatId}`,
  boatId,
  boatCode: null,
  routeName: 'A - B',
  departureTime: '2026-11-15T07:00:00Z',
  status,
})
const position = (boatId: string, observedAt: string): LiveBoatPosition => ({
  boatId,
  tripId: null,
  latitude: 10.8,
  longitude: 106.7,
  speedKnots: null,
  observedAt,
})
const codes = new Map([['b1', 'SWB-01'], ['b2', 'SWB-02'], ['b3', 'SWB-03']])

describe('fleetRows', () => {
  it('marks a position older than two minutes as stale and a fresh one as live', () => {
    const rows = fleetRows(
      [trip('b1'), trip('b2')],
      [position('b1', '2026-11-15T07:59:30Z'), position('b2', '2026-11-15T07:50:00Z')],
      codes,
      NOW,
    )
    expect(rows.map((r) => [r.code, r.signal])).toEqual([
      ['SWB-02', 'stale'],
      ['SWB-01', 'live'],
    ])
  })

  it('keeps a boat on an active trip even when it has not reported any position', () => {
    const [row] = fleetRows([trip('b1')], [], codes, NOW)
    expect(row).toMatchObject({ code: 'SWB-01', signal: 'none', position: undefined })
    expect(row.trip?.id).toBe('t-b1')
  })

  it('ignores trips that are not running or have no boat', () => {
    expect(fleetRows([trip('b1', 'Scheduled'), trip('b2', 'Completed'), trip('')], [], codes, NOW)).toEqual([])
  })

  it('lists a boat that reports a position outside any trip, without a trip', () => {
    const [row] = fleetRows([], [position('b3', '2026-11-15T07:59:00Z')], codes, NOW)
    expect(row).toMatchObject({ code: 'SWB-03', signal: 'live' })
    expect(row.trip).toBeUndefined()
  })
})
