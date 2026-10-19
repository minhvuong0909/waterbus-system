import type { SearchQuery, Trip } from '../trips/types'

/** Answer of the assistant. `trips` are shown as trip cards under the text. */
export interface AssistantReply {
  text: string
  apiCards?: AssistantApiCard[]
  trips?: Trip[]
  /** Piers and day the trips were found for, so a chosen card opens the right trip. */
  search?: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>
}

export interface ChatMessage {
  id: number
  from: 'user' | 'assistant'
  text: string
  apiCards?: AssistantApiCard[]
  trips?: Trip[]
  search?: AssistantReply['search']
}

export interface AssistantApiCard {
  tripId: string
  routeName: string
  departureStationName: string
  arrivalStationName: string
  departureTime: string
  availableSeatsCount: number
  basePrice: number | null
  isPriceVerified: boolean
  bookingUrl: string | null
}
