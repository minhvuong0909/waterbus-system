import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { connectHub } from '../../../services/realtime'
import type { HubOptions } from '../../../services/realtime'
import { useCaptainGps } from './useCaptainGps'

vi.mock('../../../services/realtime', () => ({
  connectHub: vi.fn(() => vi.fn()),
  hubErrorMessage: (e: Error) => e.message,
}))
let hub: HubOptions
const fix = {
  boatId: 'boat-1',
  lat: 10.8,
  lng: 106.7,
  speedKnots: 12,
  observedAt: '2026-11-16T01:00:00Z',
}
beforeEach(() => {
  vi.useFakeTimers()
  vi.setSystemTime(new Date('2026-11-16T01:00:00Z'))
  vi.mocked(connectHub).mockImplementation((_path, options) => {
    hub = options
    return vi.fn()
  })
})
afterEach(() => {
  cleanup()
  vi.useRealTimers()
  vi.resetAllMocks()
})
const emit = (event: typeof fix) => act(() => hub.handlers.boatPosition(event as never))

describe('Captain GPS lifecycle', () => {
  it('ignores other boats and older events, retaining the last position after connection loss', () => {
    const { result } = renderHook(() => useCaptainGps('boat-1', vi.fn()))
    act(() => hub.onStatus?.('live'))
    emit(fix)
    expect(result.current.fresh).toBe(true)
    emit({ ...fix, boatId: 'boat-2', lat: 99 })
    emit({ ...fix, lat: 88, observedAt: '2026-11-16T00:59:00Z' })
    expect(result.current.position?.latitude).toBe(10.8)
    act(() => hub.onStatus?.('reconnecting'))
    expect(result.current.connected).toBe(false)
    expect(result.current.fresh).toBe(false)
    expect(result.current.position?.latitude).toBe(10.8)
  })
  it('marks a GPS fix stale without clearing it and reloads REST on reconnect', () => {
    const reload = vi.fn()
    const { result } = renderHook(() => useCaptainGps('boat-1', reload))
    act(() => hub.onStatus?.('live'))
    emit(fix)
    act(() => vi.advanceTimersByTime(65000))
    expect(result.current.fresh).toBe(false)
    expect(result.current.position).not.toBeNull()
    act(() => hub.onReconnected?.())
    expect(reload).toHaveBeenCalledOnce()
  })
  it('never reuses another boat’s last GPS fix after changing the assignment', () => {
    const { result, rerender } = renderHook(({ boatId }) => useCaptainGps(boatId, vi.fn()), {
      initialProps: { boatId: 'boat-1' },
    })
    emit(fix)
    rerender({ boatId: 'boat-2' })
    expect(result.current.position).toBeNull()
  })
})
