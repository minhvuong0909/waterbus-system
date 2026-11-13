import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clearPierCache, clearTripReads, getPierOptions, getTripDetail } from './tripService'

const station = {
  id: 'st-1',
  code: 'ST01',
  name: 'Bến Bạch Đằng',
  address: 'Bạch Đằng',
  latitude: 10.77,
  longitude: 106.7,
  orderIndex: 1,
  isActive: true,
}
const fetchMock = vi.fn()
const stations = () => Promise.resolve(new Response(JSON.stringify([station])))

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2026-10-20T08:00:00Z'))
  clearPierCache()
  clearTripReads()
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
  vi.useRealTimers()
})

describe('getPierOptions cache', () => {
  it('shares one request between calls made together', async () => {
    fetchMock.mockImplementation(stations)
    const [a, b] = await Promise.all([getPierOptions(), getPierOptions()])
    expect(a).toEqual(b)
    expect(a[0].id).toBe('st-1')
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('asks again once the cached piers are a minute old', async () => {
    fetchMock.mockImplementation(stations)
    await getPierOptions()
    vi.setSystemTime(new Date('2026-10-20T08:00:59Z'))
    await getPierOptions()
    expect(fetchMock).toHaveBeenCalledTimes(1)
    vi.setSystemTime(new Date('2026-10-20T08:01:01Z'))
    await getPierOptions()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('does not keep a failed request', async () => {
    fetchMock.mockRejectedValueOnce(new Error('offline')).mockImplementation(stations)
    await expect(getPierOptions()).rejects.toMatchObject({ status: 0 })
    expect((await getPierOptions())[0].id).toBe('st-1')
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })

  it('reads again after the cache is cleared', async () => {
    fetchMock.mockImplementation(stations)
    await getPierOptions()
    clearPierCache()
    await getPierOptions()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})

describe('getTripDetail', () => {
  const stop = (callId: string, stationId: string, visitOrder: number, extra: object) => ({
    callId, stationId, stationName: stationId, visitOrder, canBoard: true, canAlight: true, status: 'Planned', ...extra,
  })
  const serve = (canBook: boolean) =>
    fetchMock.mockImplementation(async (url: string) => {
      const u = String(url)
      if (/\/trips\/t1\/seats/.test(u)) {
        return new Response(JSON.stringify({ seats: [{ price: 15000 }], availableSeatsCount: canBook ? 60 : 0, canBook, salesState: canBook ? 'open' : 'closed' }))
      }
      if (/\/trips\/t1$/.test(u)) {
        return new Response(JSON.stringify({
          tripId: 't1', routeId: 'r', routeName: 'Route', tripType: 'Commuter', status: 'Scheduled', canBook, salesState: canBook ? 'open' : 'closed',
          departureTime: '2026-10-21T01:00:00Z', arrivalTime: '2026-10-21T01:45:00Z', boat: { boatId: 'b', code: 'SWB-01', name: 'Boat' },
          stops: [
            // Khung check-in đóng sau giờ tàu rời bến: dữ liệu cấu hình sai vẫn phải hiện đúng thứ tự.
            stop('c1', 'st-1', 1, { estimatedDepartureTime: '2026-10-21T01:00:00Z', checkInOpenAt: '2026-10-21T00:32:00Z', checkInCloseAt: '2026-10-21T01:02:00Z' }),
            stop('c2', 'st-2', 2, { estimatedArrivalTime: '2026-10-21T01:45:00Z' }),
          ],
        }))
      }
      return new Response(JSON.stringify([station, { ...station, id: 'st-2', code: 'ST02', name: 'Bến B', orderIndex: 2 }]))
    })
  const query = { tripType: 'one-way' as const, originId: 'st-1', destinationId: 'st-2', dateId: '2026-10-21' }

  it('marks a trip whose sales the backend closed', async () => {
    serve(false)
    expect((await getTripDetail(null, 't1', query)).salesOpen).toBe(false)
  })

  it('lists the timeline in time order and keeps an open trip on sale', async () => {
    serve(true)
    const detail = await getTripDetail(null, 't1', query)
    expect(detail.salesOpen).toBe(true)
    expect(detail.timeline.map((step) => step.time)).toEqual(['07:32', '08:00', '08:02', '08:45'])
  })
})
