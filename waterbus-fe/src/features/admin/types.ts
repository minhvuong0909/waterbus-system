import type { StationDto, TripStatus, TripType } from '../trips/types'

/** Station as the backend returns it (`GET /api/v1/stations`). */
export type AdminStation = StationDto

/** Boat (BE `BoatDto`). */
export interface AdminBoat {
  id: string
  code: string
  name: string
  registrationNumber: string
  totalSeats: number
  manufacturer: string
  yearBuilt: number
  isActive: boolean
  captainUserId: string | null
}

/** A trip as the operations list returns it (BE `TripOperationItemDto`). */
export interface AdminTrip {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  /** '' while no boat is assigned. */
  boatId: string
  boatCode: string | null
  /** ISO 8601 */
  departureTime: string
  arrivalTime: string
  tripType: TripType
  status: TripStatus
  /** What the backend allows right now for this trip (BE `TripLifecycle`). */
  allowedOperations: OperationType[]
}

/** An open incident shown as a warning: weather alerts from the weather guard, or a captain's SOS. */
export interface WeatherAlert {
  id: string
  /** 1 = warning, 2 = stop operations (BE severity Medium = 1, High = 2). */
  level: 1 | 2
  message: string
  /** ISO 8601 */
  issuedAt: string
  /** BE `IncidentType`. */
  kind?: 'Weather' | 'Safety' | 'Technical' | 'Other'
  /** Trips linked to the alert (BE `IncidentTrip`), when known. */
  affectedTripCount?: number
  /** Linked trips the admin has not decided about yet (BE `PendingDecisionCount`). */
  pendingDecisionCount?: number
  /** Route the alert is about, e.g. "RT01" (weather alerts are per route). */
  routeCode?: string | null
  /** Who raised it: `WeatherService` for the weather guard, otherwise a person (captain SOS). */
  source?: string
  /** The linked trips and what the admin decided for each (null = not decided yet). Only on request. */
  trips?: AlertTrip[]
}

/** A trip an alert affects (BE `AffectedTripDto`). */
export interface AlertTrip {
  tripId: string
  decision: string | null
  departureTime?: string
  tripStatus?: TripStatus
  decisionAt?: string | null
}

/** What an admin or a captain did to a trip, kept as an audit trail (BE `TripOperationEvent`). */
export type OperationType =
  | 'OpenBoarding'
  | 'Start'
  | 'Arrive'
  | 'Complete'
  | 'Suspend'
  | 'Resume'
  | 'Cancel'
  | 'Terminate'
  | 'NoAction'
  | 'SOS'

export interface OperationLog {
  id: string
  type: OperationType
  tripId: string | null
  reason: string
  by: string
  /** ISO 8601 */
  at: string
}

export type NewStation = Pick<
  AdminStation,
  'code' | 'name' | 'address' | 'latitude' | 'longitude' | 'orderIndex'
>

export type NewBoat = Pick<AdminBoat, 'code' | 'name' | 'registrationNumber' | 'manufacturer' | 'yearBuilt' | 'totalSeats'>

/** Last known position of a boat, from `GET /tracking/boats` or the `boatPosition` hub event. */
export interface LiveBoatPosition {
  boatId: string
  /** Set while the boat runs a trip (Boarding, EnRoute or Suspended). */
  tripId: string | null
  latitude: number
  longitude: number
  speedKnots: number | null
  /** ISO 8601 */
  observedAt: string
}

/** Result of `POST /incidents/weather-check`. */
export interface WeatherCheckResult {
  routesChecked: number
  incidentsCreated: number
  incidentsUpdated: number
  /** Alerts the guard closed because the weather is calm again. */
  incidentsResolved?: number
  observationFailures: number
}

/** Phần tiền hoàn của một Ticket (BE `RefundTicketAllocationDto`). */
export interface RefundTicketAllocation {
  ticketId: string
  ticketCode: string
  amount: number
  reason: string
}

/** Kết quả `POST /refunds/request` (BE `RequestRefundResultDto`). */
export interface RefundRequestResult {
  refundTransactionId: string
  orderId: string
  bookingId: string
  amount: number
  currency: string
  status: string
  gateway: string
  /** Cổng thanh toán không hoàn được: Admin phải hoàn tay rồi xác nhận kèm bằng chứng (BR-RF-10). */
  requiresManualAction: boolean
  tickets: RefundTicketAllocation[]
  /** Chế độ Demo: hoàn tiền mô phỏng, KHÔNG có tiền thật được chuyển. */
  isDemo?: boolean
  notice?: string | null
}

/** Body of `POST /refunds/confirm-manual` (BE `ConfirmManualRefundRequest`). */
export interface ManualRefundInput {
  refundTransactionId: string
  evidence: string
  gatewayReference: string | null
  confirmedAmount: number
}

/** Kết quả `POST /refunds/confirm-manual` (BE `ConfirmManualRefundResultDto`). */
export interface ManualRefundResult {
  refundTransactionId: string
  amount: number
  status: string
  completedAt: string | null
  isDemo?: boolean
  notice?: string | null
}

/** Trang kết quả của BE (`PaginatedList<T>`). */
export interface Paged<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  totalPages: number
}

/** Booking của chuyến bị huỷ/chấm dứt cần hoàn (BE `RefundCandidateResponse`). */
export interface RefundCandidate {
  bookingId: string
  bookingCode: string
  orderId: string
  purchaserName: string
  purchaserEmail: string
  purchaserPhone: string | null
  amount: number
  currency: string
  ticketCodes: string[]
  reason: string
}

/** Kết quả `POST /refunds/batch-request` (BE `BatchRefundRequestResultResponse`). */
export interface BatchRefundRequestResult {
  tripId: string
  requested: number
  skipped: number
  created: { bookingId: string; bookingCode: string; refundTransactionId: string; amount: number; status: string }[]
  skippedItems: { bookingId: string; bookingCode: string; reason: string }[]
}

/** Khoản hoàn Manual đang chờ nhân viên hoàn (BE `PendingManualRefund`). */
export interface PendingRefund {
  refundId: string
  bookingCode: string
  orderId: string
  purchaserName: string
  purchaserEmail: string
  purchaserPhone: string | null
  amount: number
  currency: string
  ticketCodes: string[]
  routeName: string | null
  departureTime: string | null
  /** Tiền về sau khi đơn hết hạn (không có vé), không phải do chuyến bị huỷ. */
  isLatePayment: boolean
  tripStatus: string | null
  merchantReference: string | null
  paymentGatewayReference: string | null
  paidAt: string | null
  status: string
  requestedAt: string
  lastExportedAt: string | null
}

/** Kết quả `POST /refunds/confirm-manual/batch` (BE `BatchConfirmManualRefundsResponse`). */
export interface BatchConfirmResult {
  confirmed: number
  failed: number
  items: { refundTransactionId: string; succeeded: boolean; status: string | null; error: string | null }[]
}
