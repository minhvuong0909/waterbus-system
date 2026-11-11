import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  getOperationLogs,
  getOperationTrips,
  getOpenIncidents,
  operateTrip,
  resolveIncident,
} from './operationsService'

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const fetchMock = vi.fn()

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const lastCall = () => {
  const [url, init] = fetchMock.mock.calls.at(-1) as [string, RequestInit]
  return { url, method: init.method, body: init.body ? JSON.parse(init.body as string) : undefined }
}

describe('operateTrip', () => {
  it('posts the operation, the reason and the incident it answers', async () => {
    fetchMock.mockResolvedValue(json({ status: 'Suspended' }))
    const result = await operateTrip('trip-1', 'Suspend', 'Dông', 'inc-1')
    expect(result.status).toBe('Suspended')
    expect(lastCall()).toMatchObject({
      url: expect.stringMatching(/\/api\/v1\/trips\/trip-1\/status$/),
      method: 'POST',
      body: { operation: 'Suspend', reason: 'Dông', incidentId: 'inc-1' },
    })
  })

  it('sends null for an empty reason and no incident', async () => {
    fetchMock.mockResolvedValue(json({ status: 'EnRoute' }))
    await operateTrip('trip-1', 'Start', '')
    expect(lastCall().body).toEqual({ operation: 'Start', reason: null, incidentId: null })
  })

  it('sends an SOS to its own endpoint and leaves the trip status alone', async () => {
    fetchMock.mockResolvedValue(json({ incidentId: 'inc-9' }))
    const result = await operateTrip('trip-1', 'SOS', 'Cháy động cơ')
    expect(result.status).toBeNull()
    expect(lastCall()).toMatchObject({
      url: expect.stringMatching(/\/api\/v1\/trips\/trip-1\/sos$/),
      body: { note: 'Cháy động cơ' },
    })
  })

  it('surfaces the backend refusal as an error', async () => {
    fetchMock.mockResolvedValue(json({ title: 'Conflict', detail: 'Không thể thực hiện' }, 409))
    await expect(operateTrip('trip-1', 'Cancel', 'x')).rejects.toMatchObject({ status: 409, message: 'Không thể thực hiện' })
  })
})

describe('getOperationTrips', () => {
  it('maps the backend list and keeps the operations it allows', async () => {
    fetchMock.mockResolvedValue(
      json([
        {
          id: 't1',
          scheduleId: null,
          routeId: 'r1',
          routeCode: 'RT01',
          routeName: 'Bạch Đằng - Linh Đông',
          boatId: null,
          boatCode: null,
          tripType: 'Commuter',
          status: 'Scheduled',
          departureTime: '2026-10-17T08:00:00Z',
          arrivalTime: '2026-10-17T08:45:00Z',
          allowedOperations: ['Start', 'Cancel'],
        },
      ]),
    )
    const [trip] = await getOperationTrips()
    expect(trip).toMatchObject({
      id: 't1',
      boatId: '',
      routeName: 'Bạch Đằng - Linh Đông',
      status: 'Scheduled',
      allowedOperations: ['Start', 'Cancel'],
    })
    expect(trip.boatCode).toBeNull()
  })
})

describe('getOperationLogs', () => {
  it('maps events to the log and shows a dash when nobody is recorded', async () => {
    fetchMock.mockResolvedValue(
      json([
        { id: 'e1', tripId: 't1', action: 'Suspend', note: 'Dông', occurredAt: '2026-10-17T08:00:00Z', actedBy: 'Admin' },
        { id: 'e2', tripId: 't1', action: 'Resume', note: null, occurredAt: '2026-10-17T08:10:00Z', actedBy: null },
      ]),
    )
    const logs = await getOperationLogs()
    expect(logs).toEqual([
      { id: 'e1', type: 'Suspend', tripId: 't1', reason: 'Dông', by: 'Admin', at: '2026-10-17T08:00:00Z' },
      { id: 'e2', type: 'Resume', tripId: 't1', reason: '', by: '—', at: '2026-10-17T08:10:00Z' },
    ])
  })
})

describe('getOpenIncidents', () => {
  const summary = (over: object) => ({
    id: 'i1',
    severity: 'High',
    incidentType: 'Safety',
    description: 'SOS',
    occurredAt: '2026-10-17T08:00:00Z',
    affectedTripCount: 1,
    ...over,
  })

  it('maps severity and kind, and does not load trips unless asked', async () => {
    fetchMock.mockResolvedValue(json({ items: [summary({}), summary({ id: 'i2', severity: 'Medium', incidentType: 'Weather' })], totalCount: 2 }))
    const alerts = await getOpenIncidents()
    expect(alerts.map((a) => [a.level, a.kind])).toEqual([[2, 'Safety'], [1, 'Weather']])
    expect(alerts[0].trips).toBeUndefined()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('loads the affected trips and decisions when asked', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ items: [summary({})], totalCount: 1 }))
      .mockResolvedValueOnce(json({
        affectedTrips: [
          { tripId: 't1', operationDecision: null, departureTime: '2026-10-21T08:00:00+00:00', tripStatus: 1, decisionAt: null },
          { tripId: 't2', operationDecision: 'Cancel', tripStatus: 7, decisionAt: '2026-10-20T15:20:00+00:00' },
        ],
      }))
    const [alert] = await getOpenIncidents(true)
    // The backend sends the trip status as its enum number.
    expect(alert.trips).toEqual([
      { tripId: 't1', decision: null, departureTime: '2026-10-21T08:00:00+00:00', tripStatus: 'Scheduled', decisionAt: null },
      { tripId: 't2', decision: 'Cancel', tripStatus: 'Cancelled', decisionAt: '2026-10-20T15:20:00+00:00' },
    ])
  })

  it('treats an unknown incident type as other', async () => {
    fetchMock.mockResolvedValue(json({ items: [summary({ incidentType: 'Strike' })], totalCount: 1 }))
    expect((await getOpenIncidents())[0].kind).toBe('Other')
  })
})

describe('resolveIncident', () => {
  it('posts to the resolve endpoint of that incident', async () => {
    fetchMock.mockResolvedValue(json({ incidentId: 'inc-1', status: 'Resolved' }))
    await resolveIncident('inc-1')
    expect(lastCall()).toMatchObject({ url: expect.stringMatching(/\/api\/v1\/incidents\/inc-1\/resolve$/), method: 'POST' })
  })
})
