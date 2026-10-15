import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import type { BookedSnapshot } from './types'

export interface TicketsState {
  /** Which wallet ticket the detail page shows. null means "the next departure". */
  selectedTicketId: string | null
  /** Bookings made in this session, newest first. */
  booked: BookedSnapshot[]
}

const initialState: TicketsState = { selectedTicketId: null, booked: [] }

const ticketsSlice = createSlice({
  name: 'tickets',
  initialState,
  reducers: {
    selectTicket(state, action: PayloadAction<string>) {
      state.selectedTicketId = action.payload
    },
    /** Called when a payment completes: the new booking becomes the ticket shown by default. */
    addBooking(state, action: PayloadAction<BookedSnapshot>) {
      state.booked = [action.payload, ...state.booked.filter((b) => b.id !== action.payload.id)]
      state.selectedTicketId = action.payload.id
    },
  },
})

export const { selectTicket, addBooking } = ticketsSlice.actions
export default ticketsSlice.reducer
