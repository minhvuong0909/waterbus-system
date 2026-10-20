import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { askAssistant } from './assistantService'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('askAssistant', () => {
  afterEach(() => { vi.clearAllMocks() })

  it('always sends the question and chat history to the real Assistant API', async () => {
    const cards = [{ tripId: 'trip-1' }]
    vi.mocked(apiRequest).mockResolvedValue({ textAnswer: 'Tìm thấy chuyến.', cards })
    const history = [{ role: 'user' as const, content: 'Từ Bạch Đằng' }]

    const answer = await askAssistant('Đến Linh Đông ngày mai', history)

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.assistant, {
      method: 'POST', body: { message: 'Đến Linh Đông ngày mai', history, searchContext: null },
    })
    expect(answer).toEqual({ text: 'Tìm thấy chuyến.', apiCards: cards, searchContext: undefined })
  })

  it('propagates API errors instead of returning a canned answer', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('backend unavailable'))

    await expect(askAssistant('Tìm chuyến')).rejects.toThrow('backend unavailable')
  })
})
