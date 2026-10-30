import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'

/** `GET /fares` item (BE `FareDto`): BR-FARE-01, one price per trip type and seat class. */
export interface FareDto {
  tripType: 'Commuter' | 'Sightseeing' | string
  seatClassId: string
  seatClassCode: string
  seatClassName: string
  price: number
  currency: string
}

/** `GET /fares`: the active fares, one per trip type and seat class. */
export const getFares = () => apiRequest<FareDto[]>(API_ENDPOINTS.fares)

/** Lowest active fare of each trip type, for "from …" labels. A type without fares is absent. */
export async function getLowestFares(): Promise<Partial<Record<'Commuter' | 'Sightseeing', number>>> {
  const fares = await getFares()
  const lowest: Partial<Record<'Commuter' | 'Sightseeing', number>> = {}
  for (const fare of fares) {
    if (fare.tripType !== 'Commuter' && fare.tripType !== 'Sightseeing') continue
    const current = lowest[fare.tripType]
    if (current === undefined || fare.price < current) lowest[fare.tripType] = fare.price
  }
  return lowest
}
