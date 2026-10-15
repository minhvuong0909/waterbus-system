import type { SearchQuery, Trip } from '../trips/types'

/** Answer of the assistant. `trips` are shown as trip cards under the text. */
export interface AssistantReply {
  text: string
  trips?: Trip[]
  /** Piers and day the trips were found for, so a chosen card opens the right trip. */
  search?: Pick<SearchQuery, 'originId' | 'destinationId' | 'dateId'>
}

export interface ChatMessage {
  id: number
  from: 'user' | 'assistant'
  text: string
  trips?: Trip[]
  search?: AssistantReply['search']
}
