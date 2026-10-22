import type { Seat } from '../booking/types'
import type { SearchQuery, Trip } from '../trips/types'

export interface Ticket {
  id: string
  /** Vessel/trip code, e.g. "SWB-01". */
  tripCode: string
  lineName: string
  /** Short label next to the trip code, e.g. "Return Route". */
  routeTag: string
  bookingRef: string
  /** Code encoded in the boarding QR. */
  ticketCode: string
  qrPayload?: string
  /** Display text, already in the active language. */
  status: string
  departTime: string
  departPier: string
  /** Short name without "Pier", e.g. "Bach Dang". */
  departShort: string
  departDistrict: string
  arriveTime: string
  arrivePier: string
  arriveShort: string
  arriveDistrict: string
  durationMins: number
  dateLabel: string
  /** The travel day as `YYYY-MM-DD`. */
  dateId: string
  /** Date without the year, e.g. "Dec 15". */
  dateShort: string
  passenger: string
  seat: string
  seatNote: string
  /** True for a window seat. */
  windowSeat: boolean
  /** True once the journey has been made. */
  completed: boolean
  /** Vòng đời vé (BR v2.0 mục 13). Chưa có thì xem như `Valid`. */
  lifecycle?: 'Valid' | 'Cancelled' | 'Terminated' | 'Completed'
  /** Kết quả check-in hiện hành tại bến lên (BR-OFF-05). Chưa có thì xem như `NotCheckedIn`. */
  boardingStatus?: 'NotCheckedIn' | 'CheckedIn' | 'NoShow'
  /** Tiền hoàn của vé này (BR-RF), kèm trạng thái xử lý. Chỉ có khi chuyến bị huỷ/kết thúc sớm. */
  refund?: { amountVnd: number; state: 'Requested' | 'Processing' | 'Succeeded' | 'Failed' }
  fareVnd: number
  vesselNote: string
  gate: string
}

/**
 * What is stored for a booking made in this session. The display ticket is rebuilt from it in the
 * active language every time, so switching language never leaves half-translated tickets behind.
 */
export interface BookedSnapshot {
  id: string
  tripId: string
  /** The trip chosen from the search results; null when only an id is known (mock trips). */
  selectedTrip: Trip | null
  query: SearchQuery
  /** One ticket is issued per seat. */
  seats: Seat[]
  /** What each ticket cost, in the order of `seats`. */
  fares: number[]
  /** Name of the passenger on each seat, in the order of `seats`. */
  passengerNames: string[]
}

export interface TicketWallet {
  next?: Ticket
  later: Ticket[]
  past: Ticket[]
}

export interface ManageBooking {
  bookingCode: string
  ticket: Ticket
}
