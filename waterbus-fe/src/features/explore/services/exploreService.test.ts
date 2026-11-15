import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getPierOptions, getSightseeingTours, isTripOnSale } from '../../trips/services/tripService'
import type { PierOption, Trip } from '../../trips/types'
import { getSightseeingDepartures } from './exploreService'

vi.mock('../../trips/services/tripService', () => ({
  getPierOptions: vi.fn(),
  getSightseeingTours: vi.fn(),
  isTripOnSale: vi.fn(),
}))

const pier = (id: string) => ({ id, shortName: id } as PierOption)
const tour = (id: string, departTime: string, ends: [string, string] = ['A', 'C'], seatsCount = 60) =>
  ({
    id,
    departTime,
    kind: 'sightseeing',
    seatsCount,
    durationMins: 90,
    fareVnd: 100000,
    tour: { originId: ends[0], destinationId: ends[1], originName: ends[0], destinationName: ends[1] },
  }) as Trip
// 2026-11-03 08:00 at Vietnam time.
const NOW = new Date('2026-11-03T01:00:00Z')

describe('getSightseeingDepartures', () => {
  beforeEach(() => {
    vi.mocked(isTripOnSale).mockResolvedValue(true)
    vi.mocked(getPierOptions).mockResolvedValue([pier('A'), pier('B'), pier('C')])
  })
  afterEach(() => {
    vi.clearAllMocks()
  })

  it('lists bookable tours in time order with the two ends of their route', async () => {
    vi.mocked(getSightseeingTours).mockResolvedValue([
      tour('a1', '14:30'),
      tour('back', '10:30', ['C', 'A']),
      tour('full', '15:00', ['A', 'C'], 0),
      tour('gone', '07:00'),
    ])

    const result = await getSightseeingDepartures(2, NOW)

    expect(result.map((r) => r.departure.trip.id)).toEqual(['back', 'a1'])
    expect(result[0].search).toEqual({ originId: 'C', destinationId: 'A', dateId: '2026-11-03' })
  })

  it('keeps a tour that loops back to its first pier', async () => {
    vi.mocked(getSightseeingTours).mockResolvedValue([tour('loop', '10:30', ['A', 'A'])])

    const [only] = await getSightseeingDepartures(1, NOW)

    expect(only.search).toEqual({ originId: 'A', destinationId: 'A', dateId: '2026-11-03' })
  })

  it('looks at the next days when today has none, and stops at the limit', async () => {
    vi.mocked(getSightseeingTours).mockImplementation(async (dateId) =>
      dateId === '2026-11-04' ? [tour('tomorrow', '10:30')] : [],
    )

    const result = await getSightseeingDepartures(1, NOW)

    expect(result.map((r) => r.search.dateId)).toEqual(['2026-11-04'])
    expect(vi.mocked(getSightseeingTours)).toHaveBeenCalledTimes(2)
  })

  it('leaves out a trip whose sales the backend closed', async () => {
    vi.mocked(getSightseeingTours).mockResolvedValue([tour('closed', '10:30'), tour('open', '12:00')])
    vi.mocked(isTripOnSale).mockImplementation(async (id) => id !== 'closed')

    const result = await getSightseeingDepartures(1, NOW)

    expect(result.map((r) => r.departure.trip.id)).toEqual(['open'])
  })
})
