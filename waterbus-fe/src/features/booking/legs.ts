import type { SearchQuery, Trip } from '../trips/types'
import type { BookingState } from './bookingSlice'
import type { SeatPassengerForm } from './types'

/** The two legs of a round trip; a one-way booking only has the outbound one. */
export type Leg = 'outbound' | 'return'

export const isRoundTrip = (query: Pick<SearchQuery, 'tripType'>): boolean => query.tripType === 'round-trip'

/** Everything a page needs to know about one leg, whichever leg it is. */
export interface LegView {
  leg: Leg
  /** Piers and day of this leg, in the shape of a search. */
  query: SearchQuery
  tripId: string | null
  trip: Trip | null
  seatIds: string[]
  passengers: Record<string, SeatPassengerForm>
}

export function legView(state: BookingState, leg: Leg): LegView {
  if (leg === 'outbound') {
    return {
      leg,
      query: state.query,
      tripId: state.selectedTripId,
      trip: state.selectedTrip,
      seatIds: state.seatIds,
      passengers: state.passengers,
    }
  }
  const r = state.returnLeg
  return {
    leg,
    query: { tripType: 'one-way', originId: r.originId, destinationId: r.destinationId, dateId: r.dateId },
    tripId: r.tripId,
    trip: r.trip,
    seatIds: r.seatIds,
    passengers: r.passengers,
  }
}

/** The legs being booked, in order: one for one-way, two for a round trip. */
export const activeLegs = (query: Pick<SearchQuery, 'tripType'>): Leg[] =>
  isRoundTrip(query) ? ['outbound', 'return'] : ['outbound']

/** "YYYY-MM-DDTHH:mm" in Vietnam time: the day of the search plus the time shown on the trip. */
export const localStamp = (dateId: string, time: string): string => `${dateId}T${time}`

/**
 * True when a return trip leaves only after the outbound trip has arrived: a traveler cannot be
 * on both boats at once. Times are compared as the Vietnam day and clock time the pages show.
 */
export function leavesAfterArrival(
  outbound: { dateId: string; arriveTime: string },
  returnTrip: { dateId: string; departTime: string },
): boolean {
  return localStamp(returnTrip.dateId, returnTrip.departTime) > localStamp(outbound.dateId, outbound.arriveTime)
}
