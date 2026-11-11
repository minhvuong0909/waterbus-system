import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getLowestFares } from './fareService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

const fare = (tripType: string, price: number) =>
  ({ tripType, seatClassId: 'c', seatClassCode: 'SC', seatClassName: 'Class', price, currency: 'VND' })

describe('getLowestFares', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('keeps the lowest price of each trip type from the database fares', async () => {
    vi.mocked(apiRequest).mockResolvedValue([
      fare('Commuter', 20000), fare('Commuter', 15000), fare('Sightseeing', 150000), fare('Sightseeing', 100000),
    ])

    await expect(getLowestFares()).resolves.toEqual({ Commuter: 15000, Sightseeing: 100000 })
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.fares)
  })

  it('leaves out a trip type that has no fare instead of inventing one', async () => {
    vi.mocked(apiRequest).mockResolvedValue([fare('Commuter', 15000)])

    await expect(getLowestFares()).resolves.toEqual({ Commuter: 15000 })
  })

  it('propagates API errors', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('down'))

    await expect(getLowestFares()).rejects.toThrow('down')
  })
})
