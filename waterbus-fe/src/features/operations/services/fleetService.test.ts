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
    expect((await getBoats()).map((boat) => boat.id)).toEqual(['one', 'two'])
    expect(fetchMock.mock.calls[1][0]).toContain('pageNumber=2')
  })
})
