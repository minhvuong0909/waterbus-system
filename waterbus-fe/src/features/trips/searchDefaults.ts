import { toIsoDate } from '../../utils/time'
import type { SearchQuery } from './types'

// Id bến được điền từ danh sách bến thật khi tải (xem `getPierOptions`).
export const DEFAULT_SEARCH_QUERY: SearchQuery = {
  tripType: 'one-way',
  originId: '',
  destinationId: '',
  dateId: toIsoDate(new Date()),
}
