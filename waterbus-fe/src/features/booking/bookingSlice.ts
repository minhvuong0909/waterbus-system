import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import { DEFAULT_SEARCH_QUERY } from '../trips/searchDefaults'
import type { SearchQuery, Trip } from '../trips/types'
import { loadBooking } from './bookingStorage'
import { MAX_SEATS_PER_BOOKING } from './utils'
import type { BookingHold, Journey, PurchaserForm, SeatPassengerForm } from './types'

/** The order a payment was started for; kept after the hold ends so its result can be read. */
export interface PlacedOrder {
  orderId: string
  accessToken: string | null
  /** True once the backend reported the order as paid, expired or cancelled. */
  settled?: boolean
  /**
   * Seats this order holds and until when (ms since epoch). The backend shows them as held like
   * anyone else's, so the seat map needs them to tell "held by me" from "held by another traveler".
   */
  seatIds?: string[]
  heldUntil?: number
  /** The trip of the hold: the same boat seat has the same id on every trip of that boat. */
  tripId?: string | null
  /** Seats held per trip, for every booking of the order (two for a round trip). */
  legs?: { tripId: string | null; seatIds: string[] }[]
}

/**
 * The return leg of a round trip (BR-COM-02/03): its own piers, day, trip, seats and passengers.
 * It may start at another pier than the one the outbound leg ends at.
 */
export interface ReturnLegState {
  originId: string
  destinationId: string
  dateId: string
  tripId: string | null
  trip: Trip | null
  seatIds: string[]
  passengers: Record<string, SeatPassengerForm>
}

// Client-side booking state shared by the booking pages. Every field has a default,
// so any page still renders when opened directly by URL.
export interface BookingState {
  query: SearchQuery
  selectedTripId: string | null
  /** The trip picked from the search results, or null when only an id is known. */
  selectedTrip: Trip | null
  /** Chosen seats, in the order they were picked (up to MAX_SEATS_PER_BOOKING). */
  seatIds: string[]
  /** Who pays (BR-COM-07). */
  purchaser: PurchaserForm
  /** Who sits on each seat, by seat id (BR-COM-06). A seat without an entry has no name yet. */
  passengers: Record<string, SeatPassengerForm>
  /**
   * The buyer travels too: the first chosen seat carries the buyer's details. The buyer may also
   * book for someone else (BR-COM-05), then every passenger is typed in.
   */
  buyerTravels: boolean
  /** Used only when `query.tripType` is `round-trip`. */
  returnLeg: ReturnLegState
  /**
   * Boarding and alighting stops of each leg, kept when the traveler leaves its seat page: a round
   * trip is held in one request for both legs, made on the return seat page.
   */
  journeys: { outbound?: Journey; return?: Journey }
  termsAccepted: boolean
  /** Set once the checkout is created; the seats stay held until it ends or is paid. */
  hold: BookingHold | null
  /** The latest order, read from the backend by id for the payment result. */
  order: PlacedOrder | null
}

/** Blank buyer form. */
const EMPTY_PURCHASER: PurchaserForm = { fullName: '', phoneCode: '+84', phone: '', email: '' }

export const EMPTY_SEAT_PASSENGER: SeatPassengerForm = { fullName: '', email: '', phone: '' }

export const EMPTY_RETURN_LEG: ReturnLegState = {
  originId: '',
  destinationId: '',
  dateId: '',
  tripId: null,
  trip: null,
  seatIds: [],
  passengers: {},
}

const initialState: BookingState = {
  query: DEFAULT_SEARCH_QUERY,
  selectedTripId: null,
  selectedTrip: null,
  // No seat is picked for the traveler: they choose every seat themselves.
  seatIds: [],
  purchaser: EMPTY_PURCHASER,
  passengers: {},
  buyerTravels: true,
  returnLeg: EMPTY_RETURN_LEG,
  journeys: {},
  // Consent must be given on purpose, never pre-ticked.
  termsAccepted: false,
  hold: null,
  order: null,
  ...loadBooking(),
}

/** Adds the seat, or removes it when it is already chosen. Past the limit nothing is added. */
function toggle(seatIds: string[], passengers: Record<string, SeatPassengerForm>, id: string) {
  if (seatIds.includes(id)) {
    delete passengers[id]
    return seatIds.filter((seatId) => seatId !== id)
  }
  return seatIds.length < MAX_SEATS_PER_BOOKING ? [...seatIds, id] : seatIds
}

/** Copies the buyer onto the first chosen seat while the buyer travels too. */
function syncBuyerSeat(state: BookingState) {
  const seatId = state.seatIds[0]
  if (!state.buyerTravels || !seatId) return
  const { fullName, email, phone } = state.purchaser
  state.passengers[seatId] = { fullName, email, phone }
}
// A booking saved before the buyer could travel too gets its first seat in line on reload.
syncBuyerSeat(initialState)

const bookingSlice = createSlice({
  name: 'booking',
  initialState,
  reducers: {
    setQuery(state, action: PayloadAction<Partial<SearchQuery>>) {
      const next = { ...state.query, ...action.payload }
      // Switching between one-way and round trip changes what has to be held.
      if (next.tripType !== state.query.tripType) state.hold = null
      // The return day can never be before the outbound day.
      if (next.returnDateId && next.returnDateId < next.dateId) next.returnDateId = next.dateId
      const leg = state.returnLeg
      if (next.originId !== state.query.originId || next.destinationId !== state.query.destinationId) {
        // Other outbound piers: the return leg is set up again (reversed) when it is opened.
        if (leg.originId) {
          state.returnLeg = EMPTY_RETURN_LEG
          delete state.journeys.return
        }
      } else if (leg.dateId) {
        const day = next.returnDateId ?? next.dateId
        const wanted = day < next.dateId ? next.dateId : day
        if (wanted !== leg.dateId && (action.payload.returnDateId !== undefined || leg.dateId < next.dateId)) {
          state.returnLeg = { ...leg, dateId: wanted, tripId: null, trip: null, seatIds: [], passengers: {} }
          delete state.journeys.return
          state.hold = null
        }
      }
      state.query = next
    },
    /** A full trip (from search results) or just an id (home page departures). */
    /**
     * Picking the trip the traveler is already booking (and has not paid yet) keeps their seats,
     * names and hold. Any other trip starts a new selection. The previous order stays recorded,
     * unpaid, so it is cancelled on the backend before the next hold instead of keeping its seats.
     */
    selectTrip(state, action: PayloadAction<Trip | string>) {
      const trip = typeof action.payload === 'string' ? null : action.payload
      const tripId = trip ? trip.id : (action.payload as string)
      if (tripId === state.selectedTripId && !state.order?.settled) {
        if (trip) state.selectedTrip = trip
        return
      }
      state.selectedTripId = tripId
      state.selectedTrip = trip
      state.seatIds = []
      state.passengers = {}
      state.journeys = {}
      state.hold = null
      if (state.order?.settled) {
        state.order = null
        state.returnLeg = EMPTY_RETURN_LEG
      }
    },
    // The hold is made for the trip and seats, so changing either makes it stale: it is dropped
    // (the order is released when the next hold is made). Names are sent later, on submit.
    toggleSeat(state, action: PayloadAction<string>) {
      state.seatIds = toggle(state.seatIds, state.passengers, action.payload)
      syncBuyerSeat(state)
      state.hold = null
    },
    updatePurchaser(state, action: PayloadAction<Partial<PurchaserForm>>) {
      state.purchaser = { ...state.purchaser, ...action.payload }
      syncBuyerSeat(state)
    },
    /** Unticking "I am also a passenger" leaves the first seat blank for the real traveler's name. */
    setBuyerTravels(state, action: PayloadAction<boolean>) {
      state.buyerTravels = action.payload
      if (action.payload) syncBuyerSeat(state)
      else if (state.seatIds[0]) state.passengers[state.seatIds[0]] = { ...EMPTY_SEAT_PASSENGER }
    },
    updateSeatPassenger(
      state,
      action: PayloadAction<{ seatId: string; patch: Partial<SeatPassengerForm> }>,
    ) {
      const { seatId, patch } = action.payload
      state.passengers[seatId] = { ...EMPTY_SEAT_PASSENGER, ...state.passengers[seatId], ...patch }
    },

    // ---- Return leg of a round trip ------------------------------------------------------------

    /**
     * Opens the return leg: the first time, it runs back from the outbound arrival pier to the
     * departure pier on the chosen return day. A return day before the outbound day is moved up.
     */
    startReturnLeg(state) {
      const leg = state.returnLeg
      if (!leg.originId || !leg.destinationId) {
        leg.originId = state.query.destinationId
        leg.destinationId = state.query.originId
      }
      const day = state.query.returnDateId ?? state.query.dateId
      if (!leg.dateId || leg.dateId < state.query.dateId) {
        leg.dateId = day < state.query.dateId ? state.query.dateId : day
      }
    },
    /** Other piers or another day for the return leg: its trip and seats no longer apply. */
    setReturnQuery(state, action: PayloadAction<Partial<Pick<ReturnLegState, 'originId' | 'destinationId' | 'dateId'>>>) {
      const leg = state.returnLeg
      const next = { ...leg, ...action.payload }
      if (next.dateId && next.dateId < state.query.dateId) next.dateId = state.query.dateId
      const changed =
        next.originId !== leg.originId || next.destinationId !== leg.destinationId || next.dateId !== leg.dateId
      if (!changed) return
      state.returnLeg = { ...next, tripId: null, trip: null, seatIds: [], passengers: {} }
      delete state.journeys.return
      state.hold = null
    },
    selectReturnTrip(state, action: PayloadAction<Trip | string>) {
      const trip = typeof action.payload === 'string' ? null : action.payload
      const tripId = trip ? trip.id : (action.payload as string)
      const leg = state.returnLeg
      if (tripId === leg.tripId) {
        if (trip) leg.trip = trip
        return
      }
      state.returnLeg = { ...leg, tripId, trip, seatIds: [], passengers: {} }
      delete state.journeys.return
      state.hold = null
    },
    toggleReturnSeat(state, action: PayloadAction<string>) {
      state.returnLeg.seatIds = toggle(state.returnLeg.seatIds, state.returnLeg.passengers, action.payload)
      state.hold = null
    },
    updateReturnPassenger(
      state,
      action: PayloadAction<{ seatId: string; patch: Partial<SeatPassengerForm> }>,
    ) {
      const { seatId, patch } = action.payload
      const passengers = state.returnLeg.passengers
      passengers[seatId] = { ...EMPTY_SEAT_PASSENGER, ...passengers[seatId], ...patch }
    },
    /** "Same as outbound": the n-th return seat gets the passenger of the n-th outbound seat. */
    copyPassengersToReturn(state) {
      state.returnLeg.seatIds.forEach((seatId, index) => {
        const outbound = state.passengers[state.seatIds[index]]
        if (outbound) state.returnLeg.passengers[seatId] = { ...outbound }
      })
    },
    /** Keeps the boarding and alighting stops of a leg until both legs are held together. */
    setJourney(state, action: PayloadAction<{ leg: 'outbound' | 'return'; journey: Journey }>) {
      state.journeys[action.payload.leg] = action.payload.journey
    },

    // ---- Hold and order ------------------------------------------------------------------------

    holdStarted(state, action: PayloadAction<BookingHold>) {
      const hold = action.payload
      state.hold = hold
      const legs = hold.bookings?.map(({ tripId, seatIds }) => ({ tripId, seatIds })) ?? [
        { tripId: state.selectedTripId, seatIds: hold.seatIds },
      ]
      state.order = {
        orderId: hold.orderId,
        accessToken: hold.accessToken,
        seatIds: hold.seatIds,
        heldUntil: hold.expiresAt,
        tripId: state.selectedTripId,
        legs,
      }
    },
    /** The previous order was cancelled to hold other seats: its seats are no longer ours. */
    orderReleased(state) {
      if (state.order && !state.order.settled) state.order = null
    },
    /** Buyer and passengers were submitted: the order now waits for payment. */
    holdSubmitted(state) {
      if (state.hold) state.hold.submitted = true
    },
    holdCleared(state) {
      state.hold = null
    },
    /**
     * Opening the gateway moves the order's deadline to the payment window the backend set
     * (`Checkout:PaymentPendingTtlMinutes`), so the countdown follows the backend, not the first hold.
     */
    holdExtended(state, action: PayloadAction<number>) {
      if (state.hold) state.hold.expiresAt = action.payload
      if (state.order && !state.order.settled) state.order.heldUntil = action.payload
    },
    /** The backend reported the order as paid, expired or cancelled: nothing is held any more. */
    orderSettled(state) {
      state.hold = null
      if (state.order) state.order.settled = true
    },
    setTermsAccepted(state, action: PayloadAction<boolean>) {
      state.termsAccepted = action.payload
    },
  },
})

export const {
  setQuery,
  selectTrip,
  toggleSeat,
  updatePurchaser,
  updateSeatPassenger,
  setBuyerTravels,
  startReturnLeg,
  setReturnQuery,
  selectReturnTrip,
  toggleReturnSeat,
  updateReturnPassenger,
  copyPassengersToReturn,
  setJourney,
  setTermsAccepted,
  holdStarted,
  holdSubmitted,
  holdCleared,
  holdExtended,
  orderReleased,
  orderSettled,
} = bookingSlice.actions
export default bookingSlice.reducer
