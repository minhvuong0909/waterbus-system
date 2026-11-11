import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest } from '../../../services/apiClient'
import { getPierOptions } from '../../trips/services/tripService'
import { getTrackedTrip } from './trackingService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))
vi.mock('../../trips/services/tripService', () => ({ getPierOptions: vi.fn() }))

const pier = (id: string, latitude: number) => ({ id, latitude, longitude: 106.7 })
const stop = (callId: string, stationId: string, visitOrder: number) => ({
  callId, stationId, stationName: stationId, visitOrder, canBoard: true, canAlight: true, status: 'Scheduled',
  estimatedDepartureTime: '2026-10-31T01:00:00Z',
})
const trip = (stops: ReturnType<typeof stop>[]) => ({
  tripId: 't1', routeId: 'r1', routeName: 'RT01', tripType: 'Commuter', status: 'EnRoute',
  departureTime: '2026-10-31T01:00:00Z', arrivalTime: '2026-10-31T01:45:00Z', canBook: true, salesState: 'Open',
  boat: { boatId: 'b1', code: 'SWB-01', name: 'Waterbus 01' }, stops,
})

describe('getTrackedTrip', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('places the stops at their stations in visit order', async () => {
    vi.mocked(apiRequest).mockResolvedValue(trip([stop('c2', 's2', 2), stop('c1', 's1', 1)]))
    vi.mocked(getPierOptions).mockResolvedValue([pier('s1', 10.77), pier('s2', 10.83)] as never)

    const result = await getTrackedTrip('t1')

    expect(result.stops.map((s) => [s.callId, s.latitude])).toEqual([['c1', 10.77], ['c2', 10.83]])
    expect(result.status).toBe('EnRoute')
    expect(result.boat?.boatId).toBe('b1')
  })

  it('leaves off a stop whose station is no longer listed instead of inventing a position', async () => {
    vi.mocked(apiRequest).mockResolvedValue(trip([stop('c1', 's1', 1), stop('c2', 'gone', 2)]))
    vi.mocked(getPierOptions).mockResolvedValue([pier('s1', 10.77)] as never)

    expect((await getTrackedTrip('t1')).stops.map((s) => s.callId)).toEqual(['c1'])
  })

  it('propagates a failed trip request so the page can offer a retry', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('404'))
    vi.mocked(getPierOptions).mockResolvedValue([] as never)

    await expect(getTrackedTrip('t1')).rejects.toThrow('404')
  })
})
