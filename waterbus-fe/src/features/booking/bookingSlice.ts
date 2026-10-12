import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import { DEFAULT_PASSENGER, DEFAULT_SEAT_IDS } from '../../mocks/booking'
import { DEFAULT_SEARCH_QUERY } from '../../mocks/tripSearch'
import type { SearchQuery, Trip } from '../trips/types'
import type { PaymentMethodId } from '../payment/types'
import { MAX_SEATS_PER_BOOKING } from './utils'
import type { BookingHold, PassengerForm, Voucher } from './types'

// Client-side booking state shared by the booking pages. Every field has a default,
// so any page still renders when opened directly by URL.
export interface BookingState {
  query: SearchQuery
  selectedTripId: string | null
  /** The trip picked from the search results, or null when only an id is known. */
  selectedTrip: Trip | null
  /** Chosen seats, in the order they were picked (up to MAX_SEATS_PER_BOOKING). */
  seatIds: string[]
  passenger: PassengerForm
  voucher: Voucher | null
  termsAccepted: boolean
  paymentMethod: PaymentMethodId
  /** Set once the booking is created; the seats stay held until it ends or is paid. */
  hold: BookingHold | null
}

const initialState: BookingState = {
  query: DEFAULT_SEARCH_QUERY,
  selectedTripId: null,
  selectedTrip: null,
  seatIds: DEFAULT_SEAT_IDS,
  passenger: DEFAULT_PASSENGER,
  voucher: null,
  termsAccepted: true,
  paymentMethod: 'qr',
  hold: null,
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
    },
    // A booking is created from the trip, seat, passenger and fare, so changing any of them
    // makes an existing hold stale: it is dropped and a new booking is made at checkout.
    /** Adds the seat, or removes it when it is already chosen. Past the limit nothing is added. */
    toggleSeat(state, action: PayloadAction<string>) {
      const id = action.payload
      if (state.seatIds.includes(id)) {
        state.seatIds = state.seatIds.filter((seatId) => seatId !== id)
      } else if (state.seatIds.length < MAX_SEATS_PER_BOOKING) {
        state.seatIds.push(id)
      }
      state.hold = null
    },
    updatePassenger(state, action: PayloadAction<Partial<PassengerForm>>) {
      state.passenger = { ...state.passenger, ...action.payload }
      state.hold = null
    },
    applyVoucher(state, action: PayloadAction<Voucher | null>) {
      state.voucher = action.payload
      state.hold = null
    },
    holdStarted(state, action: PayloadAction<BookingHold>) {
      state.hold = action.payload
    },
    holdCleared(state) {
      state.hold = null
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
  updatePassenger,
  applyVoucher,
  setTermsAccepted,
  setPaymentMethod,
  holdStarted,
  holdCleared,
} = bookingSlice.actions
export default bookingSlice.reducer
