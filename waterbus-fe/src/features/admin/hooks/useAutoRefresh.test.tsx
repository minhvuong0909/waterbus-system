import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useAutoRefresh } from './useAutoRefresh'

afterEach(() => { cleanup(); vi.useRealTimers() })
describe('automatic admin updates', () => {
  it('refreshes periodically, skips in-flight requests, and cleans up on unmount', () => {
    vi.useFakeTimers()
    const refresh = vi.fn()
    const { rerender, unmount } = renderHook(({ busy }) => useAutoRefresh(refresh, busy), { initialProps: { busy: false } })
    act(() => { vi.advanceTimersByTime(60_000) })
    expect(refresh).toHaveBeenCalledTimes(1)
    rerender({ busy: true })
    act(() => { vi.advanceTimersByTime(60_000) })
    expect(refresh).toHaveBeenCalledTimes(1)
    rerender({ busy: false })
    act(() => { window.dispatchEvent(new Event('online')) })
    expect(refresh).toHaveBeenCalledTimes(2)
    unmount()
    act(() => { vi.advanceTimersByTime(60_000); window.dispatchEvent(new Event('online')) })
    expect(refresh).toHaveBeenCalledTimes(2)
  })
})
