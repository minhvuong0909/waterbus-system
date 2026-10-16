import type { BookingState } from './bookingSlice'

const STORAGE_KEY = 'waterbus.booking'

// The traveler leaves the app for the VNPay page and comes back to a fresh page load, so the
// booking in progress (seats, names, the order and its guest token) is kept for the browser tab.
// Storage can throw (private mode, blocked site data), so every access is guarded.

export function loadBooking(): Partial<BookingState> {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as Partial<BookingState>) : {}
  } catch {
    return {}
  }
}

export function saveBooking(state: BookingState) {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch {
    // Not stored: the booking simply does not survive a reload.
  }
}
