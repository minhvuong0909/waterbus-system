import { describe, expect, it } from 'vitest'
import { hasFare, toDeparture, toTrip } from './utils'
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
