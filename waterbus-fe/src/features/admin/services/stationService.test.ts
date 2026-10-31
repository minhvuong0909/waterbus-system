import { afterEach, describe, expect, it, vi } from 'vitest'
import { createStation, deleteStation } from './stationService'

const fetchMock = vi.fn()
const json = (body: unknown) => new Response(JSON.stringify(body))
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })
describe('stationService', () => {
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
})
