import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getCurrentWeather, toKmh } from './weatherService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))
vi.mock('../../../constant/env', () => ({ USE_MOCK: false }))

describe('weatherService', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('reads the current weather of the routes from the backend', async () => {
    const routes = [{ routeCode: 'RT01', status: 'Caution' }]
    vi.mocked(apiRequest).mockResolvedValue(routes)

    await expect(getCurrentWeather()).resolves.toBe(routes)
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.incidents.weatherNow)
  })

  it('propagates errors so the panel can offer a retry', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('down'))

    await expect(getCurrentWeather()).rejects.toThrow('down')
  })

  it('converts m/s to km/h with one decimal', () => {
    expect(toKmh(10.6)).toBe(38.2)
    expect(toKmh(0)).toBe(0)
  })
})
