import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { RouteDetail, RouteInput, ScheduleDetail, ScheduleInput } from '../types'
import { fetchAllPages } from './catalogRequests'
import { createFareRule } from './fareRuleService'
import { setRouteActive, updateRoute } from './routeService'
import { updateSchedule } from './scheduleService'
import { getSeatClasses } from './seatClassService'

const fetchMock = vi.fn()
const answer = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
const calls = () =>
  fetchMock.mock.calls.map(([url, init]) => ({
    url: String(url),
    method: (init as RequestInit).method,
    body: (init as RequestInit).body ? JSON.parse(String((init as RequestInit).body)) : undefined,
  }))

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  fetchMock.mockImplementation(async () => answer({}))
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const route: RouteDetail = {
  id: 'r1', code: 'RT01', name: 'Old', serviceType: 'Regular', isActive: true, estimatedDurationMinutes: 40,
  distanceKm: 10, structureLocked: false,
  stops: [
    { id: 's1', sequenceNo: 1, stationId: 'A', stationName: 'A', canBoard: true, canAlight: false },
    { id: 's2', sequenceNo: 2, stationId: 'B', stationName: 'B', canBoard: false, canAlight: true },
  ],
}
const routeInput: RouteInput = {
  code: 'RT01', name: 'New', serviceType: 'Regular', estimatedDurationMinutes: 45, distanceKm: 11,
  stops: route.stops.map(({ stationId, canBoard, canAlight }) => ({ stationId, canBoard, canAlight })),
}

describe('catalogue services', () => {
  it('reads every page of a paged list', async () => {
    fetchMock
      .mockResolvedValueOnce(answer({ items: [1, 2], totalCount: 3 }))
      .mockResolvedValueOnce(answer({ items: [3], totalCount: 3 }))
    expect(await fetchAllPages<number>('/api/v1/admin/routes', { isActive: true })).toEqual([1, 2, 3])
    expect(calls()[1].url).toMatch(/isActive=true&pageNumber=2&pageSize=100$/)
  })

  it('saves the details of a route without touching unchanged stops', async () => {
    await updateRoute(route, routeInput)
    expect(calls()).toEqual([
      { url: expect.stringMatching(/\/admin\/routes\/r1$/), method: 'PUT',
        body: { name: 'New', serviceType: 'Regular', estimatedDurationMinutes: 45, distanceKm: 11 } },
    ])
  })

  it('replaces the stops of a route only while its structure is not locked', async () => {
    const reordered = { ...routeInput, stops: [...routeInput.stops].reverse() }
    await updateRoute(route, reordered)
    expect(calls()[1]).toMatchObject({ url: expect.stringMatching(/\/admin\/routes\/r1\/stops$/), method: 'PUT' })

    fetchMock.mockClear()
    await updateRoute({ ...route, structureLocked: true }, reordered)
    expect(calls()).toHaveLength(1)
  })

  it('keeps the stops of a schedule that already produced trips', async () => {
    const schedule: ScheduleDetail = {
      id: 'sc1', routeId: 'r1', routeCode: 'RT01', routeName: 'Old', tripType: 'Commuter', direction: 'Outbound',
      departureTime: '07:30:00', daysOfWeek: '1,2,3', effectiveFrom: '2026-11-01', effectiveTo: null, isActive: true,
      stopCount: 2, structureLocked: true, stops: [],
    }
    const input: ScheduleInput = {
      routeId: 'r1', tripType: 'Commuter', direction: 'Outbound', departureTime: '08:00:00', daysOfWeek: '1,2,3',
      effectiveFrom: '2026-11-01', effectiveTo: null,
      stops: [{ routeStopId: 's1', arrivalOffsetMin: null, departureOffsetMin: 0 }],
    }
    await updateSchedule(schedule, input)
    expect(calls()).toHaveLength(1)
    expect(calls()[0].body).not.toHaveProperty('routeId')
    expect(calls()[0].body).toMatchObject({ departureTime: '08:00:00' })
  })

  it('switches a route off and on with a POST', async () => {
    await setRouteActive('r1', false)
    await setRouteActive('r1', true)
    expect(calls().map((c) => [c.method, c.url.replace(/^.*\/r1\//, '')])).toEqual([
      ['POST', 'deactivate'],
      ['POST', 'activate'],
    ])
  })

  it('creates fares in VND and lists switched-off seat classes for the admin', async () => {
    await createFareRule({ tripType: 'Commuter', seatClassId: 'c1', price: 15000, effectiveFrom: '2026-12-01T00:00:00.000Z', effectiveTo: null })
    expect(calls()[0].body).toMatchObject({ currency: 'VND', price: 15000 })

    fetchMock.mockResolvedValueOnce(answer([]))
    await getSeatClasses()
    expect(calls()[1].url).toMatch(/\/seat-classes\?includeInactive=true$/)
  })
})
