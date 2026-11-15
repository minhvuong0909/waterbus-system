import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { WeatherAlert } from '../types'
import { getOpenIncidents } from '../services/operationsService'
import { useFleetHub } from './useFleetHub'
import { useOpenAlertSummary } from './useOpenAlertSummary'

vi.mock('../services/operationsService')
vi.mock('./useFleetHub', () => ({ useFleetHub: vi.fn(() => 'live') }))

const alert = (id: string, level: 1 | 2, pendingDecisionCount?: number): WeatherAlert => ({
  id, level, message: 'Gió mạnh', issuedAt: '2026-11-13T10:00:00Z', pendingDecisionCount,
})

beforeEach(() => {
  vi.mocked(getOpenIncidents).mockResolvedValue([])
})
afterEach(() => {
  vi.clearAllMocks()
  vi.useRealTimers()
})

describe('useOpenAlertSummary', () => {
  it('summarises the open alerts: how many, how many trips wait, and the worst level', async () => {
    vi.mocked(getOpenIncidents).mockResolvedValue([alert('a', 1, 2), alert('b', 2, 1), alert('c', 1)])

    const { result } = renderHook(() => useOpenAlertSummary(true))

    await waitFor(() => expect(result.current).toEqual({ open: 3, waiting: 3, level: 2 }))
  })

  it('is null when nothing is open', async () => {
    const { result } = renderHook(() => useOpenAlertSummary(true))

    await waitFor(() => expect(getOpenIncidents).toHaveBeenCalled())
    expect(result.current).toBeNull()
  })

  it('does not even ask the server when disabled (a captain in the shell)', async () => {
    const { result } = renderHook(() => useOpenAlertSummary(false))

    await act(async () => {})
    expect(getOpenIncidents).not.toHaveBeenCalled()
    expect(result.current).toBeNull()
    expect(vi.mocked(useFleetHub).mock.calls[0][1]).toBe(false)             // và không mở kết nối realtime
  })

  it('reloads when the hub pushes a weather alert, an emergency, a status change or a reconnect', async () => {
    vi.mocked(getOpenIncidents).mockResolvedValue([])
    renderHook(() => useOpenAlertSummary(true))
    await waitFor(() => expect(getOpenIncidents).toHaveBeenCalledTimes(1))
    const handlers = vi.mocked(useFleetHub).mock.calls.at(-1)![0]

    vi.mocked(getOpenIncidents).mockResolvedValue([alert('new', 1, 1)])
    act(() => handlers.onAlert?.({} as never))

    await waitFor(() => expect(getOpenIncidents).toHaveBeenCalledTimes(2))
    for (const key of ['onIncident', 'onTripStatus', 'onReconnected'] as const) expect(handlers[key]).toBeTypeOf('function')
  })

  it('polls every minute as a safety net', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderHook(() => useOpenAlertSummary(true))
    await vi.waitFor(() => expect(getOpenIncidents).toHaveBeenCalledTimes(1))

    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })

    expect(getOpenIncidents).toHaveBeenCalledTimes(2)
  })

  it('shows no banner when the list cannot be read', async () => {
    vi.mocked(getOpenIncidents).mockRejectedValue(new Error('down'))

    const { result } = renderHook(() => useOpenAlertSummary(true))

    await waitFor(() => expect(getOpenIncidents).toHaveBeenCalled())
    expect(result.current).toBeNull()
  })
})
