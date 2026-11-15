import { act, renderHook, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { HUB_EVENTS } from '../../../constant/apiEndpoints'
import { connectHub } from '../../../services/realtime'
import { getLatestBoatPositions } from '../services/operationsService'
import { useFleetHub, useLiveFleet } from './useFleetHub'

vi.mock('../../../services/realtime', () => ({ connectHub: vi.fn(() => () => undefined) }))
vi.mock('../services/operationsService', async (original) => ({
  ...(await original<typeof import('../services/operationsService')>()),
  getLatestBoatPositions: vi.fn(),
}))

type HubOptions = Parameters<typeof connectHub>[1]
const hubOptions = () => vi.mocked(connectHub).mock.calls.at(-1)![1] as HubOptions
const emit = (event: string, payload: unknown) => (hubOptions().handlers as Record<string, (e: unknown) => void>)[event](payload)

beforeEach(() => {
  vi.mocked(getLatestBoatPositions).mockResolvedValue([])
})
afterEach(() => {
  vi.clearAllMocks()
})

describe('fleet hub listeners', () => {
  it('listens for positions, trip status and emergency reports, and no longer for a weather alert', () => {
    renderHook(() => useFleetHub({}))
    expect(Object.keys(hubOptions().handlers).sort()).toEqual(
      [HUB_EVENTS.boatPosition, HUB_EVENTS.incidentRaised, HUB_EVENTS.tripStatusChanged].sort(),
    )
    expect(HUB_EVENTS).not.toHaveProperty('weatherAlert')
  })

  it('an emergency report (incidentRaised) reaches the page at once', () => {
    const onIncident = vi.fn()
    renderHook(() => useFleetHub({ onIncident }))
    const event = { incidentId: 'i1', incidentType: 'Safety', severity: 'High' }

    act(() => emit(HUB_EVENTS.incidentRaised, event))

    expect(onIncident).toHaveBeenCalledWith(event)
  })
})

describe('useLiveFleet (Radar)', () => {
  it('refreshes the incident list on an emergency report, a trip status change and a reconnect', async () => {
    const handlers = { onIncident: vi.fn(), onTripStatus: vi.fn(), onReconnected: vi.fn() }
    renderHook(() => useLiveFleet(handlers))
    await waitFor(() => expect(getLatestBoatPositions).toHaveBeenCalledTimes(1))

    act(() => emit(HUB_EVENTS.incidentRaised, { incidentId: 'i1' }))
    act(() => emit(HUB_EVENTS.tripStatusChanged, { tripId: 't1' }))
    expect(handlers.onIncident).toHaveBeenCalledTimes(1)
    expect(handlers.onTripStatus).toHaveBeenCalledTimes(1)

    act(() => hubOptions().onReconnected?.())
    expect(handlers.onReconnected).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(getLatestBoatPositions).toHaveBeenCalledTimes(2)) // vị trí tàu cũng tải lại qua REST
  })

  it('keeps tracking boat positions without any handler', async () => {
    const { result } = renderHook(() => useLiveFleet())
    await waitFor(() => expect(result.current.loaded).toBe(true))

    act(() => emit(HUB_EVENTS.boatPosition, {
      boatId: 'b1', tripId: null, lat: 10.8, lng: 106.7, speedKnots: 5, observedAt: '2026-11-16T03:00:00Z',
    }))

    expect(result.current.positions).toHaveLength(1)
  })
})
