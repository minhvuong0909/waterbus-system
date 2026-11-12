import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { buildStopRows, stopRowsError, toStopInputs } from '../scheduleStops'
import { timingErrors, toTimingInput } from '../tripTiming'
import type { RouteStop } from '../types'
import { getOperationTrips } from './operationsService'
import { assignTripBoat, createAdHocTrip } from './tripPlanningService'

const fetchMock = vi.fn()
const answer = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status })
const lastCall = () => {
  const [url, init] = fetchMock.mock.calls[fetchMock.mock.calls.length - 1] as [string, RequestInit]
  return { url, method: init.method, body: init.body ? JSON.parse(String(init.body)) : undefined }
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const routeStops: RouteStop[] = ['A', 'B', 'C'].map((name, i) => ({
  id: `s${i + 1}`, sequenceNo: i + 1, stationId: name, stationName: name, canBoard: true, canAlight: true,
}))

describe('trip planning', () => {
  it('assigns a boat to a trip', async () => {
    fetchMock.mockResolvedValue(answer({ tripId: 't1', boatId: 'b2', previousBoatId: 'b1', remappedReservations: 3 }))
    expect((await assignTripBoat('t1', 'b2')).remappedReservations).toBe(3)
    expect(lastCall()).toMatchObject({ method: 'PUT', body: { boatId: 'b2' } })
    expect(lastCall().url).toMatch(/\/admin\/trips\/t1\/boat$/)
  })

  it('passes on the refusal of a boat that overlaps another trip', async () => {
    fetchMock.mockResolvedValue(answer({ detail: 'BR-SCH-08' }, 409))
    await expect(assignTripBoat('t1', 'b2')).rejects.toMatchObject({ status: 409, message: 'BR-SCH-08' })
  })

  it('creates a trip outside a schedule', async () => {
    fetchMock.mockResolvedValue(answer({ tripId: 't9', departureTime: '2026-12-01T01:00:00Z' }, 201))
    await createAdHocTrip({
      routeId: 'r1', tripType: 'Commuter', departureTime: '2026-12-01T01:00:00.000Z',
      stops: [], salesCloseMinutesBeforeDeparture: null, checkInOpenMinutesBeforeDeparture: 30,
    })
    expect(lastCall()).toMatchObject({ method: 'POST', body: { routeId: 'r1', checkInOpenMinutesBeforeDeparture: 30 } })
    expect(lastCall().url).toMatch(/\/admin\/trips$/)
  })

  it('reads the week that starts at a given instant, or the default window', async () => {
    fetchMock.mockImplementation(async () => answer([]))
    await getOperationTrips()
    expect(lastCall().url).toMatch(/\/trips\/operations$/)
    await getOperationTrips({ from: '2026-12-01T17:00:00.000Z' })
    expect(lastCall().url).toContain('/trips/operations?from=2026-12-01T17')
  })
})

describe('stops of a run', () => {
  it('runs the route backwards when inbound and times the stops by their place', () => {
    const edits = { s2: { included: true, arrival: '20', departure: '22' }, s1: { included: true, arrival: '40', departure: '' } }
    const rows = buildStopRows(routeStops, 'Inbound', edits)
    expect(rows.map((r) => r.stationName)).toEqual(['C', 'B', 'A'])
    expect(stopRowsError(rows)).toBeUndefined()
    expect(toStopInputs(rows)).toEqual([
      { routeStopId: 's3', arrivalOffsetMin: null, departureOffsetMin: 0 },
      { routeStopId: 's2', arrivalOffsetMin: 20, departureOffsetMin: 22 },
      { routeStopId: 's1', arrivalOffsetMin: 40, departureOffsetMin: null },
    ])
  })

  it('leaves out unserved stops and asks for at least two', () => {
    const skipMiddle = { s2: { included: false, arrival: '', departure: '' }, s3: { included: true, arrival: '35', departure: '' } }
    expect(toStopInputs(buildStopRows(routeStops, 'Outbound', skipMiddle)).map((s) => s.routeStopId)).toEqual(['s1', 's3'])

    const onlyOne = { ...skipMiddle, s3: { included: false, arrival: '', departure: '' } }
    expect(stopRowsError(buildStopRows(routeStops, 'Outbound', onlyOne))).toBeDefined()
    expect(stopRowsError(buildStopRows(routeStops, 'Outbound', {}))).toBeDefined()
  })

  it('starts from the stops a schedule already serves', () => {
    const rows = buildStopRows(routeStops, 'Outbound', {}, [
      { routeStopId: 's1', arrivalOffsetMin: null, departureOffsetMin: 0 },
      { routeStopId: 's3', arrivalOffsetMin: 30, departureOffsetMin: null },
    ])
    expect(rows.map((r) => [r.included, r.arrival])).toEqual([[true, ''], [false, ''], [true, '30']])
  })
})

describe('trip timing', () => {
  it('sends empty marks as null and checks their range', () => {
    expect(toTimingInput({ salesClose: '', checkInOpen: '45' })).toEqual({
      salesCloseMinutesBeforeDeparture: null,
      checkInOpenMinutesBeforeDeparture: 45,
    })
    expect(timingErrors({ salesClose: '0', checkInOpen: '' })).toEqual({ salesClose: undefined, checkInOpen: undefined })
    expect(timingErrors({ salesClose: '-1', checkInOpen: '0' }).checkInOpen).toBeDefined()
    expect(timingErrors({ salesClose: '1441', checkInOpen: '' }).salesClose).toBeDefined()
  })
})
