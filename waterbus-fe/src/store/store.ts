import { configureStore } from '@reduxjs/toolkit'
import bookingReducer from '../features/booking/bookingSlice'
import ticketsReducer from '../features/tickets/ticketsSlice'

// Feature slices are registered here as their branches land.
export const store = configureStore({
  reducer: {
    booking: bookingReducer,
    tickets: ticketsReducer,
  },
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
