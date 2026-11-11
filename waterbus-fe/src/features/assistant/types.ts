/** Phản hồi từ AI API; Trip Card chỉ lấy từ backend. */
export interface AssistantReply {
  text: string
  apiCards?: AssistantApiCard[]
  searchContext?: AssistantSearchContext | null
  pendingClarification?: AssistantScopeClarification | null
  /** True when the backend could not answer normally and returned a safe default reply. */
  isFallback?: boolean
}

/** Câu hỏi cần giữ lại đến khi khách chọn tìm mới hay lọc tiếp. */
export interface AssistantScopeClarification {
  originalMessage: string
}

/** Bộ lọc đã được backend kiểm tra ở lần tìm trước, gửi lại cho câu hỏi nối tiếp. */
export interface AssistantSearchContext {
  departureStationName?: string | null
  arrivalStationName?: string | null
  restrictSingleStationToRequestedDirection?: boolean
  departureDate?: string | null
  earliestDepartureTime?: string | null
  latestDepartureTime?: string | null
  preferredDepartureTime?: string | null
  latestArrivalTime?: string | null
  tripType?: string | null
  passengerCount?: number | null
  minimumAvailableSeats?: number | null
  priceGreaterThan?: number | null
  priceLessThan?: number | null
}

export interface ChatMessage {
  id: number
  from: 'user' | 'assistant'
  text: string
  apiCards?: AssistantApiCard[]
  /** `fallback`: the backend's safe default reply. `error`: the request failed (shown locally only). */
  tone?: 'fallback' | 'error'
  /** For an error: the question to send again with "Try again". */
  retryQuestion?: string
}

export interface AssistantChatTurn {
  role: 'user' | 'assistant'
  content: string
}

export interface AssistantApiCard {
  tripId: string
  boardingStationId?: string | null
  disembarkingStationId?: string | null
  routeName: string
  departureStationName: string
  arrivalStationName: string
  departureTime: string
  availableSeatsCount: number
  basePrice: number | null
  isPriceVerified: boolean
  bookingUrl: string | null
  isDevelopmentPreview?: boolean
}
