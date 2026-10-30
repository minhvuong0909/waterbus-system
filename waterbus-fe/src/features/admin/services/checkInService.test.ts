import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getTripStops, setCheckInWindow } from './checkInService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('checkInService', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('returns the stops of a trip in visit order', async () => {
    vi.mocked(apiRequest).mockResolvedValue({ stops: [{ callId: 'b', visitOrder: 2 }, { callId: 'a', visitOrder: 1 }] })
    expect((await getTripStops('t1')).map((s) => s.callId)).toEqual(['a', 'b'])
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.trips.detail('t1'))
  })

  it('sends the window of one stop with PUT', async () => {
    vi.mocked(apiRequest).mockResolvedValue({})
    await setCheckInWindow('call-1', '2026-11-01T00:30:00.000Z', '2026-11-01T00:55:00.000Z')
    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.checkIns.window('call-1'), {
      method: 'PUT',
      body: { tripStopCallId: 'call-1', checkInOpenAt: '2026-11-01T00:30:00.000Z', checkInCloseAt: '2026-11-01T00:55:00.000Z' },
    })
  })

  it('passes the backend refusal on to the page', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('Không thể đặt cửa sổ check-in cho điểm dừng đã khởi hành'))
    await expect(setCheckInWindow('c', 'a', 'b')).rejects.toThrow('đã khởi hành')
  })
})
