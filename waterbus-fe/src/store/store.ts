import { configureStore } from '@reduxjs/toolkit'
import authenticationReducer, { signedOut } from '../features/authentication/authenticationSlice'
import bookingReducer from '../features/booking/bookingSlice'
import ticketsReducer from '../features/tickets/ticketsSlice'
import { setUnauthorizedHandler } from '../services/apiClient'
import { clearSession } from '../services/authStorage'
import { saveBooking } from '../features/booking/bookingStorage'

// Feature slices are registered here as their branches land.
export const store = configureStore({
  reducer: {
    authentication: authenticationReducer,
    booking: bookingReducer,
    tickets: ticketsReducer,
  },
})

// An expired or revoked token (401) ends the session wherever the request came from.
setUnauthorizedHandler(() => {
  clearSession()
  store.dispatch(signedOut())
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch

// Keep the booking in progress across the redirect to the payment gateway and back.
let savedBooking = store.getState().booking
store.subscribe(() => {
  const { booking } = store.getState()
  if (booking !== savedBooking) {
    savedBooking = booking
    saveBooking(booking)
  }
})
