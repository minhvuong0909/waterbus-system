import { afterEach, describe, expect, it, vi } from 'vitest'
import { createBoat, deleteBoat } from './boatService'

const fetchMock = vi.fn()
const json = (body: unknown) => new Response(JSON.stringify(body))
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })
describe('boatService', () => {
  it('creates a boat without a captain and returns the saved record', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json('boat-id')).mockResolvedValueOnce(json({ id: 'boat-id', totalSeats: 60 }))
    const boat = await createBoat({ code: 'SWB-02', name: 'Two', registrationNumber: 'R2', manufacturer: '', yearBuilt: 2025, totalSeats: 60 })
    expect(boat.id).toBe('boat-id')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).captainUserId).toBeNull()
  })
  it('deactivates a boat with PUT because the backend has no DELETE', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json({ id: 'b', isActive: true })).mockResolvedValueOnce(new Response(null, { status: 204 }))
    await deleteBoat('b')
    expect(fetchMock.mock.calls[1][1].method).toBe('PUT')
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).isActive).toBe(false)
  })
})
