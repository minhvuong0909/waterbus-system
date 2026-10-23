import type { AssistantReply, AssistantApiCard, AssistantChatTurn, AssistantSearchContext } from '../types'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { getLocale } from '../../../i18n/translate'

/** Luôn dùng AI API thật; widget không có câu trả lời hoặc Trip Card giả lập. */
export async function askAssistant(
  message: string,
  history: AssistantChatTurn[] = [],
  searchContext: AssistantSearchContext | null = null,
): Promise<AssistantReply> {
  const response = await apiRequest<{ textAnswer: string; cards: AssistantApiCard[]; searchContext?: AssistantSearchContext | null; isFallback?: boolean }>(API_ENDPOINTS.assistant, {
    method: 'POST', body: { message, history, searchContext, languageCode: getLocale() },
  })
  // Backend quyết định nội dung và liên kết; không tự dựng giá hoặc chuyến từ lời AI.
  return {
    text: response.textAnswer,
    apiCards: response.cards,
    searchContext: response.searchContext,
    isFallback: response.isFallback,
  }
}
