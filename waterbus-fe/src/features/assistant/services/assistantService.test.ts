import { afterEach, describe, expect, it, vi } from 'vitest'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { askAssistant } from './assistantService'
import { setActiveLocale } from '../../../i18n/translate'

vi.mock('../../../services/apiClient', () => ({ apiRequest: vi.fn() }))

describe('askAssistant', () => {
  afterEach(() => { vi.clearAllMocks(); setActiveLocale('en') })

  it('always sends the question and chat history to the real Assistant API', async () => {
    const cards = [{ tripId: 'trip-1' }]
    vi.mocked(apiRequest).mockResolvedValue({ textAnswer: 'Tìm thấy chuyến.', cards })
    const history = [{ role: 'user' as const, content: 'Từ Bạch Đằng' }]

    const answer = await askAssistant('Đến Linh Đông ngày mai', history)

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.assistant, {
      method: 'POST', body: { message: 'Đến Linh Đông ngày mai', history, searchContext: null, pendingClarification: null, languageCode: 'en' },
    })
    expect(answer).toEqual({ text: 'Tìm thấy chuyến.', apiCards: cards, searchContext: undefined, pendingClarification: undefined })
  })

  it('sends the selected Vietnamese interface language', async () => {
    setActiveLocale('vi')
    vi.mocked(apiRequest).mockResolvedValue({ textAnswer: 'Không có chuyến.', cards: [] })

    await askAssistant('Có chuyến nào?')

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.assistant, {
      method: 'POST', body: { message: 'Có chuyến nào?', history: [], searchContext: null, pendingClarification: null, languageCode: 'vi' },
    })
  })

  it('sends the pending scope question back with the user choice', async () => {
    const pendingClarification = { originalMessage: 'Có chuyến nào còn ghế?' }
    vi.mocked(apiRequest).mockResolvedValue({ textAnswer: 'Tìm thấy chuyến.', cards: [], pendingClarification: null })

    const answer = await askAssistant('lọc tiếp', [], { departureStationName: 'Bạch Đằng' }, pendingClarification)

    expect(apiRequest).toHaveBeenCalledWith(API_ENDPOINTS.assistant, {
      method: 'POST', body: {
        message: 'lọc tiếp', history: [], searchContext: { departureStationName: 'Bạch Đằng' },
        pendingClarification, languageCode: 'en',
      },
    })
    expect(answer.pendingClarification).toBeNull()
  })

  it('propagates API errors instead of returning a canned answer', async () => {
    vi.mocked(apiRequest).mockRejectedValue(new Error('backend unavailable'))

    await expect(askAssistant('Tìm chuyến')).rejects.toThrow('backend unavailable')
  })
})
