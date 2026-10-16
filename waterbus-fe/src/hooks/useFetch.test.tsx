import { act, renderHook, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { combineFetches, useFetch } from './useFetch'

describe('useFetch', () => {
  it('loads data and clears loading', async () => {
    const { result } = renderHook(() => useFetch(() => Promise.resolve('ok')))
    expect(result.current.loading).toBe(true)
    await waitFor(() => expect(result.current.data).toBe('ok'))
    expect(result.current.loading).toBe(false)
    expect(result.current.error).toBeUndefined()
  })

  it('exposes an error and recovers on retry', async () => {
    const loader = vi
      .fn<[], Promise<string>>()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValueOnce('fine')
    const { result } = renderHook(() => useFetch(loader))
    await waitFor(() => expect(result.current.error?.message).toBe('boom'))
    act(() => result.current.retry())
    await waitFor(() => expect(result.current.data).toBe('fine'))
    expect(result.current.error).toBeUndefined()
  })

  it('reloads when deps change and keeps old data meanwhile', async () => {
    const { result, rerender } = renderHook(
      ({ id }) => useFetch(() => Promise.resolve(`item-${id}`), [id]),
      { initialProps: { id: 1 } },
    )
    await waitFor(() => expect(result.current.data).toBe('item-1'))
    rerender({ id: 2 })
    expect(result.current.data).toBe('item-1')
    expect(result.current.loading).toBe(false)
    await waitFor(() => expect(result.current.data).toBe('item-2'))
  })
})

describe('combineFetches', () => {
  it('merges loading and error, retrying only failed requests', () => {
    const retryA = vi.fn()
    const retryB = vi.fn()
    const combined = combineFetches(
      { data: 1, loading: false, fetching: false, error: undefined, retry: retryA },
      { data: undefined, loading: false, fetching: false, error: new Error('x'), retry: retryB },
    )
    expect(combined.error?.message).toBe('x')
    combined.retry()
    expect(retryA).not.toHaveBeenCalled()
    expect(retryB).toHaveBeenCalledTimes(1)
  })
})
