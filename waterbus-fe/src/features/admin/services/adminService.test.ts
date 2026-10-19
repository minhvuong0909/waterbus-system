import { afterEach, describe, expect, it, vi } from 'vitest'
import { createStation, deleteStation, getBoats, createSchedule } from './adminService'

const fetchMock = vi.fn()
const json = (body: unknown) => new Response(JSON.stringify(body))
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })
describe('live admin actions', () => {
  it('creates stations on the server and returns the persisted record', async () => {
    vi.stubGlobal('fetch', fetchMock)
    const station = { id: 'station-id', code: 'UI', name: 'Test', address: '', latitude: 10, longitude: 106, orderIndex: 7, isActive: true }
    fetchMock.mockResolvedValueOnce(json(station.id)).mockResolvedValueOnce(json(station))
    expect((await createStation(station)).id).toBe('station-id')
    expect(fetchMock.mock.calls[0][1].method).toBe('POST')
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/stations\/station-id$/)
  })
  it('deactivates a station with PUT without a nonexistent DELETE endpoint', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json({ id: 'id', name: 'Test', isActive: true }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    await deleteStation('id')
    expect(fetchMock.mock.calls[1][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).isActive).toBe(false)
  })
  it('reads every page of the fleet rather than truncating at the first page', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json({ items: [{ id: 'one', totalSeats: 10 }], totalCount: 2 }))
      .mockResolvedValueOnce(json({ items: [{ id: 'two', totalSeats: 12 }], totalCount: 2 }))
    expect((await getBoats()).map((boat) => boat.id)).toEqual(['one', 'two'])
    expect(fetchMock.mock.calls[1][0]).toContain('pageNumber=2')
  })
  it('rejects unsupported schedule writes without pretending they were saved', async () => {
    await expect(createSchedule({ routeId: 'id', departureTime: '08:00', daysOfWeek: [1], tripType: 'Commuter' })).rejects.toMatchObject({ status: 501 })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
