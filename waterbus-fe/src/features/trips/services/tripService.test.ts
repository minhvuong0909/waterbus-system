import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clearPierCache, getPierOptions } from './tripService'

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
