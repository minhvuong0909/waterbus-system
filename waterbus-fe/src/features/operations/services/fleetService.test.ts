import { afterEach, describe, expect, it, vi } from 'vitest'
import { getBoats } from './fleetService'

const fetchMock = vi.fn()
const json = (body: unknown) => new Response(JSON.stringify(body))
afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })

describe('fleetService', () => {
  it('reads every page of the fleet rather than truncating at the first page', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json({ items: [{ id: 'one', totalSeats: 10 }], totalCount: 2 }))
      .mockResolvedValueOnce(json({ items: [{ id: 'two', totalSeats: 12 }], totalCount: 2 }))
    expect((await getBoats(true)).map((boat) => boat.id)).toEqual(['one', 'two'])
    expect(fetchMock.mock.calls[1][0]).toContain('pageNumber=2')
    expect(fetchMock.mock.calls[0][0]).toContain('isActiveOnly=true')
  })
  it('omits isActiveOnly (not false) when the admin requests the full catalogue, since the backend treats false as inactive-only', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValueOnce(json({
      items: [{ id: 'active', isActive: true }, { id: 'inactive', isActive: false }], totalCount: 2,
    }))
    expect((await getBoats()).map((boat) => boat.id)).toEqual(['active', 'inactive'])
    expect(fetchMock.mock.calls[0][0]).not.toContain('isActiveOnly')
  })
})
