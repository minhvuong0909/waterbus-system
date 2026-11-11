import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createRequestCache } from './requestCache'

beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2026-10-31T08:00:00Z'))
})
afterEach(() => {
  vi.useRealTimers()
})

describe('createRequestCache', () => {
  it('shares one request per key while it is fresh', async () => {
    const cache = createRequestCache<number>(15_000)
    const load = vi.fn(() => Promise.resolve(1))
    await Promise.all([cache.get('a', load), cache.get('a', load)])
    vi.advanceTimersByTime(14_000)
    await cache.get('a', load)
    expect(load).toHaveBeenCalledTimes(1)
    await cache.get('b', load)
    expect(load).toHaveBeenCalledTimes(2)
  })

  it('asks again once the result is older than the time to live', async () => {
    const cache = createRequestCache<number>(15_000)
    const load = vi.fn(() => Promise.resolve(1))
    await cache.get('a', load)
    vi.advanceTimersByTime(15_000)
    await cache.get('a', load)
    expect(load).toHaveBeenCalledTimes(2)
  })

  it('does not keep a failure, and clear() forgets everything', async () => {
    const cache = createRequestCache<number>(15_000)
    const failing = vi.fn(() => Promise.reject(new Error('down')))
    await expect(cache.get('a', failing)).rejects.toThrow('down')
    const load = vi.fn(() => Promise.resolve(2))
    await expect(cache.get('a', load)).resolves.toBe(2)
    cache.clear()
    await cache.get('a', load)
    expect(load).toHaveBeenCalledTimes(2)
  })
})
