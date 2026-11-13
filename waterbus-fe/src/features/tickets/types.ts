export interface Ticket {
  id: string
  /** Backend trip id, used to follow the trip on the tracking page. */
  tripId: string
  /** Vessel/trip code, e.g. "SWB-01". */
  tripCode: string
  lineName: string
  /** Short label next to the trip code, e.g. "Return Route". */
  routeTag: string
  bookingId: string
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
  /** Đường dẫn Manage Order secure link của đơn chứa vé này (BR-ID-02). */
  manageLink?: string
}

export interface TicketWallet {
  next?: Ticket
  later: Ticket[]
  past: Ticket[]
}

/** One booking of an order: the tickets of one trip, one per passenger. */
export interface ManagedBooking {
  bookingId: string
  bookingCode: string
  tickets: Ticket[]
}

export interface ManageBooking {
  /** Every booking of the order in travel order: one for a one-way order, two for a round trip. */
  bookings: ManagedBooking[]
  orderId: string
  /** Token Guest đang dùng để mở đơn; null khi đơn được mở bằng tài khoản Passenger sở hữu nó. */
  accessToken: string | null
  /** Link quản lý vé để khách lưu lại; không có khi đơn không có token Guest. */
  manageUrl?: string
}

/** Token của một secure link vừa cấp (BE `AccessTokenResponse`); backend chỉ trả một lần. */
export interface AccessLink {
  accessToken: string
  expiresAt: string
}

/** Một trang kết quả (BE `PaginatedList<T>`). */
export interface Page<T> {
  items: T[]
  pageNumber: number
  totalPages: number
  totalCount: number
}

/** BE `MyOrderBookingDto`. */
export interface AccountOrderBooking {
  bookingId: string
  bookingCode: string
  tripId: string
  routeName: string
  departureTime: string
  bookingStatus: string
  paymentStatus: string
  ticketCount: number
}

/** Một đơn trong lịch sử của tài khoản Passenger (BE `MyOrderSummaryDto`). */
export interface AccountOrder {
  orderId: string
  orderStatus: string
  purchaseMode: 'OneWay' | 'RoundTrip'
  quotedTotal: number
  currency: string
  createdAt: string
  confirmedAt: string | null
  bookings: AccountOrderBooking[]
}

/** BE `TripAccessTicketDto`. */
export interface SharedTripPassenger {
  passengerName: string
  seatCode: string
  ticketStatus: string
  boardingStatus: string
}

/**
 * Hành trình xem qua Passenger TripAccess Link (BE `TripAccessDto`, BR-ID-05): không có giá,
 * thanh toán, thông tin người mua hay QR.
 */
export interface SharedTrip {
  bookingCode: string
  tripId: string
  routeName: string
  /** Giờ khởi hành và cập bến cuối của cả chuyến, không phải của riêng chặng đã đặt. */
  departureTime: string
  arrivalTime: string
  tripStatus: string
  boardingStation: string
  disembarkingStation: string
  bookingStatus: string
  tickets: SharedTripPassenger[]
}

/** Một đơn đã mua trên thiết bị này, kèm token Guest để mở lại (BR-ID-02). */
export interface SavedOrder {
  orderId: string
  accessToken: string
  /** ms since epoch, đơn mới nhất đứng đầu. */
  savedAt: number
}
