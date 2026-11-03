import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getPierOptions, getTrips, isTripOnSale } from '../../trips/services/tripService'
import type { PierOption, Trip } from '../../trips/types'
import { getSightseeingDepartures } from './exploreService'

vi.mock('../../trips/services/tripService', () => ({
  getPierOptions: vi.fn(),
  getTrips: vi.fn(),
  isTripOnSale: vi.fn(),
}))

const pier = (id: string) => ({ id, shortName: id } as PierOption)
const trip = (
  id: string,
  departTime: string,
  kind: Trip['kind'] = 'sightseeing',
  seatsCount = 60,
) => ({ id, departTime, kind, seatsCount, durationMins: 45, fareVnd: 100000 } as Trip)
// 2026-11-03 08:00 at Vietnam time.
const NOW = new Date('2026-11-03T01:00:00Z')

describe('getSightseeingDepartures', () => {
  beforeEach(() => {
    vi.mocked(isTripOnSale).mockResolvedValue(true)
  })
  afterEach(() => {
    vi.clearAllMocks()
  })

  it('searches the first and last pier both ways and keeps bookable sightseeing trips in time order', async () => {
    vi.mocked(getPierOptions).mockResolvedValue([pier('A'), pier('B'), pier('C')])
    vi.mocked(getTrips).mockImplementation(async (query) =>
      query.originId === 'A'
        ? [
            trip('a1', '14:30'),
            trip('commuter', '09:00', 'transit'),
            trip('full', '15:00', 'sightseeing', 0),
          ]
        : [trip('c1', '10:30'), trip('gone', '07:00')],
    )

    const result = await getSightseeingDepartures(2, NOW)

    expect(result.map((r) => r.departure.trip.id)).toEqual(['c1', 'a1'])
    expect(result[0].search).toEqual({ originId: 'C', destinationId: 'A', dateId: '2026-11-03' })
    expect(vi.mocked(getTrips).mock.calls.map(([q]) => `${q.originId}-${q.destinationId}`)).toEqual(
      ['A-C', 'C-A'],
    )
  })

  it('looks at the next days when today has none, and stops at the limit', async () => {
    vi.mocked(getPierOptions).mockResolvedValue([pier('A'), pier('C')])
    vi.mocked(getTrips).mockImplementation(async (query) =>
      query.dateId === '2026-11-04' && query.originId === 'A' ? [trip('tomorrow', '10:30')] : [],
    )

    const result = await getSightseeingDepartures(1, NOW)

    expect(result.map((r) => r.search.dateId)).toEqual(['2026-11-04'])
    expect(vi.mocked(getTrips)).toHaveBeenCalledTimes(4)
  })

  it('leaves out a trip whose sales the backend closed', async () => {
    vi.mocked(getPierOptions).mockResolvedValue([pier('A'), pier('C')])
    vi.mocked(getTrips).mockImplementation(async (query) =>
      query.originId === 'A' ? [trip('closed', '10:30'), trip('open', '12:00')] : [],
    )
    vi.mocked(isTripOnSale).mockImplementation(async (id) => id !== 'closed')

    const result = await getSightseeingDepartures(1, NOW)

    expect(result.map((r) => r.departure.trip.id)).toEqual(['open'])
  })

  it('returns nothing without two piers', async () => {
    vi.mocked(getPierOptions).mockResolvedValue([pier('A')])
    await expect(getSightseeingDepartures(4, NOW)).resolves.toEqual([])
    expect(getTrips).not.toHaveBeenCalled()
  })
})
