import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiRequest } from './apiClient'
import { fetchAllPages } from './paging'

vi.mock('./apiClient', () => ({ apiRequest: vi.fn() }))

afterEach(() => {
  vi.clearAllMocks()
})

describe('fetchAllPages', () => {
  it('reads page after page until it has every row', async () => {
    vi.mocked(apiRequest)
      .mockResolvedValueOnce({ items: [1, 2], totalCount: 3 })
      .mockResolvedValueOnce({ items: [3], totalCount: 3 })

    await expect(fetchAllPages<number>('/api/v1/boats', { isActiveOnly: false })).resolves.toEqual([1, 2, 3])
    expect(apiRequest).toHaveBeenNthCalledWith(2, '/api/v1/boats', {
      query: { isActiveOnly: false, pageNumber: 2, pageSize: 100 },
    })
  })

  it('stops on an empty page even if the total says there is more', async () => {
    vi.mocked(apiRequest)
      .mockResolvedValueOnce({ items: [1], totalCount: 5 })
      .mockResolvedValueOnce({ items: [], totalCount: 5 })

    await expect(fetchAllPages<number>('/x')).resolves.toEqual([1])
    expect(apiRequest).toHaveBeenCalledTimes(2)
  })

  it('passes a failure on to the caller', async () => {
    vi.mocked(apiRequest).mockRejectedValueOnce(new Error('offline'))
    await expect(fetchAllPages('/x')).rejects.toThrow('offline')
  })
})
