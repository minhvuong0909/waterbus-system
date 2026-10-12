import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import { DEFAULT_PASSENGER, DEFAULT_SEAT_ID } from '../../mocks/booking'
import { DEFAULT_SEARCH_QUERY } from '../../mocks/tripSearch'
import type { SearchQuery } from '../trips/types'
import type { PaymentMethodId } from '../payment/types'
import type { PassengerForm, Voucher } from './types'

// Client-side booking state shared by the booking pages. Every field has a default,
// so any page still renders when opened directly by URL.
export interface BookingState {
  query: SearchQuery
  selectedTripId: string | null
  seatId: string
  passenger: PassengerForm
  voucher: Voucher | null
  termsAccepted: boolean
  paymentMethod: PaymentMethodId
}

const initialState: BookingState = {
  query: DEFAULT_SEARCH_QUERY,
  selectedTripId: null,
  seatId: DEFAULT_SEAT_ID,
  passenger: DEFAULT_PASSENGER,
  voucher: null,
  termsAccepted: true,
  paymentMethod: 'qr',
}

const bookingSlice = createSlice({
  name: 'booking',
  initialState,
  reducers: {
    setQuery(state, action: PayloadAction<Partial<SearchQuery>>) {
      state.query = { ...state.query, ...action.payload }
    },
    selectTrip(state, action: PayloadAction<string>) {
      state.selectedTripId = action.payload
    },
    selectSeat(state, action: PayloadAction<string>) {
      state.seatId = action.payload
    },
    updatePassenger(state, action: PayloadAction<Partial<PassengerForm>>) {
      state.passenger = { ...state.passenger, ...action.payload }
    },
    applyVoucher(state, action: PayloadAction<Voucher | null>) {
      state.voucher = action.payload
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
  selectSeat,
  updatePassenger,
  applyVoucher,
  setTermsAccepted,
  setPaymentMethod,
} = bookingSlice.actions
export default bookingSlice.reducer
