import { describe, expect, it } from 'vitest'
import { hasFare, journeyFor, toDeparture, toTrip } from './utils'
import type { TripDto } from './types'

describe('hasFare', () => {
  it('treats 0 as "not known yet", because the search endpoint sends no fare', () => {
    expect(hasFare(0)).toBe(false)
  })

  it('accepts a real fare', () => {
    expect(hasFare(15000)).toBe(true)
  })

  it('does not accept a negative amount', () => {
    expect(hasFare(-1)).toBe(false)
  })
})

describe('toTrip duration', () => {
  const dto = (durationMinutes: number | null) => ({ id: 'a1b2c3d4', routeName: 'R', departureTime: '2026-11-07T00:30:00Z', arrivalTime: '2026-11-07T01:15:00Z', durationMinutes, tripType: 'Commuter', basePrice: 15000, availableSeatsCount: 5, canBook: true } as TripDto)
  it('keeps a known segment duration', () => {
    expect(toTrip(dto(20))).toMatchObject({ durationMins: 20, durationKnown: true })
  })

  it('does not turn an unknown segment duration into the whole-trip duration', () => {
    expect(toTrip(dto(null))).toMatchObject({ durationMins: 0, durationKnown: false })
  })

  it('omits the duration from the departure row when it is unknown', () => {
    const pier = { id: 'p', name: 'P', shortName: 'P' } as never
    const details = toDeparture(toTrip(dto(null)), pier, pier).details
    expect(details.join(' ')).not.toMatch(/voyage|min/i)
  })
})

describe('sightseeing runs its whole route', () => {
  const stop = (callId: string, stationId: string, visitOrder: number, flags = {}) => ({
    callId, stationId, stationName: stationId, visitOrder, canBoard: true, canAlight: true, status: 'Planned', ...flags,
  })
  const loop = [stop('c1', 'A', 1), stop('c2', 'B', 2), stop('c3', 'C', 3), stop('c4', 'A', 4)]

  it('ignores the piers the traveler searched and boards at the first stop, leaves at the last', () => {
    const journey = journeyFor('Sightseeing', loop, 'B', 'C')
    expect([journey?.boarding.callId, journey?.disembarking.callId]).toEqual(['c1', 'c4'])
  })

  it('still follows the chosen piers for a regular trip', () => {
    const journey = journeyFor('Commuter', loop, 'B', 'C')
    expect([journey?.boarding.callId, journey?.disembarking.callId]).toEqual(['c2', 'c3'])
  })

  it('skips stops that cannot board or alight, and returns null when no journey is left', () => {
    const stops = [stop('c1', 'A', 1, { canBoard: false }), stop('c2', 'B', 2), stop('c3', 'C', 3, { canAlight: false })]
    expect(journeyFor('Sightseeing', stops, 'A', 'C')).toBeNull()
  })

  it('toTrip exposes the ends of a tour, even when they are the same pier', () => {
    const dto = {
      id: 't', routeName: 'Tour', departureStationName: 'A', arrivalStationName: 'A', tripType: 'Sightseeing',
      departureTime: '2026-11-16T02:00:00+00:00', arrivalTime: '2026-11-16T03:30:00+00:00', durationMinutes: 90,
      basePrice: 100000, availableSeatsCount: 10,
      stops: [{ stationId: 'C', stationName: 'C', orderIndex: 3 }, { stationId: 'A', stationName: 'A', orderIndex: 1 }, { stationId: 'A', stationName: 'A', orderIndex: 4 }],
    } as TripDto
    expect(toTrip(dto).tour).toMatchObject({ originId: 'A', destinationId: 'A' })
    expect(toTrip({ ...dto, tripType: 'Commuter' }).tour).toBeUndefined()
  })
})
