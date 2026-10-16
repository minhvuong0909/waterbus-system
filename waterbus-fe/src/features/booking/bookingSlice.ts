import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import { DEFAULT_PURCHASER, DEFAULT_SEAT_IDS } from '../../mocks/booking'
import { DEFAULT_SEARCH_QUERY } from '../../mocks/tripSearch'
import type { SearchQuery, Trip } from '../trips/types'
import type { PaymentMethodId } from '../payment/types'
import { loadBooking } from './bookingStorage'
import { MAX_SEATS_PER_BOOKING } from './utils'
import type { BookingHold, PurchaserForm, SeatPassengerForm } from './types'

/** The order a payment was started for; kept after the hold ends so its result can be read. */
export interface PlacedOrder {
  orderId: string
  accessToken: string | null
  /** True once the backend reported the order as paid, expired or cancelled. */
  settled?: boolean
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
  termsAccepted: boolean
  paymentMethod: PaymentMethodId
  /** Set once the checkout is created; the seats stay held until it ends or is paid. */
  hold: BookingHold | null
  /** The latest order, read from the backend by id for the payment result. */
  order: PlacedOrder | null
}

export const EMPTY_SEAT_PASSENGER: SeatPassengerForm = { fullName: '', email: '', phone: '' }

const initialState: BookingState = {
  query: DEFAULT_SEARCH_QUERY,
  selectedTripId: null,
  selectedTrip: null,
  seatIds: DEFAULT_SEAT_IDS,
  purchaser: DEFAULT_PURCHASER,
  passengers: {},
  termsAccepted: true,
  paymentMethod: 'qr',
  hold: null,
  order: null,
  ...loadBooking(),
}

const bookingSlice = createSlice({
  name: 'booking',
  initialState,
  reducers: {
    setQuery(state, action: PayloadAction<Partial<SearchQuery>>) {
      state.query = { ...state.query, ...action.payload }
    },
    /** A full trip (from search results) or just an id (home page departures). */
    selectTrip(state, action: PayloadAction<Trip | string>) {
      const trip = typeof action.payload === 'string' ? null : action.payload
      state.selectedTripId = trip ? trip.id : (action.payload as string)
      state.selectedTrip = trip
      state.hold = null
      state.order = null
    },
    // A checkout is created from the trip, seats, names and fare, so changing any of them
    // makes an existing hold stale: it is dropped and a new checkout is made at review.
    /** Adds the seat, or removes it when it is already chosen. Past the limit nothing is added. */
    toggleSeat(state, action: PayloadAction<string>) {
      const id = action.payload
      if (state.seatIds.includes(id)) {
        state.seatIds = state.seatIds.filter((seatId) => seatId !== id)
        delete state.passengers[id]
      } else if (state.seatIds.length < MAX_SEATS_PER_BOOKING) {
        state.seatIds.push(id)
      }
      state.hold = null
    },
    updatePurchaser(state, action: PayloadAction<Partial<PurchaserForm>>) {
      state.purchaser = { ...state.purchaser, ...action.payload }
      state.hold = null
    },
    updateSeatPassenger(
      state,
      action: PayloadAction<{ seatId: string; patch: Partial<SeatPassengerForm> }>,
    ) {
      const { seatId, patch } = action.payload
      state.passengers[seatId] = { ...EMPTY_SEAT_PASSENGER, ...state.passengers[seatId], ...patch }
      state.hold = null
    },
    holdStarted(state, action: PayloadAction<BookingHold>) {
      state.hold = action.payload
      state.order = { orderId: action.payload.orderId, accessToken: action.payload.accessToken }
    },
    holdCleared(state) {
      state.hold = null
    },
    /** The backend reported the order as paid, expired or cancelled: nothing is held any more. */
    orderSettled(state) {
      state.hold = null
      if (state.order) state.order.settled = true
    },
    setTermsAccepted(state, action: PayloadAction<boolean>) {
      state.termsAccepted = action.payload
    },
    setPaymentMethod(state, action: PayloadAction<PaymentMethodId>) {
      state.paymentMethod = action.payload
    },
  },
})

export const {
  setQuery,
  selectTrip,
  toggleSeat,
  updatePurchaser,
  updateSeatPassenger,
  setTermsAccepted,
  setPaymentMethod,
  holdStarted,
  holdCleared,
  orderSettled,
} = bookingSlice.actions
export default bookingSlice.reducer
