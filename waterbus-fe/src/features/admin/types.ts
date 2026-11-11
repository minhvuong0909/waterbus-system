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

// ---- Danh mục: hạng ghế, ghế của tàu, giá vé, tuyến, lịch chạy -------------------------------

/** BE `SeatClassResponse`. `seatCount` là số ghế đang dùng hạng này. */
export interface SeatClass {
  id: string
  code: string
  name: string
  description: string | null
  isActive: boolean
  seatCount: number
}

export interface SeatClassInput {
  code: string
  name: string
  description: string | null
}

/** BE `SeatInput` / `UpdateBoatSeatRequest`. */
export interface SeatInput {
  seatCode: string
  seatClassId: string
  rowNumber: number
  columnNumber: number
}

/** BE `BoatSeatResponse`. */
export interface BoatSeat extends SeatInput {
  id: string
  boatId: string
  seatClassCode: string
  seatClassName: string
  isActive: boolean
}

/** BE `BoatSeatInventoryResponse`: số ghế hoạt động không bao giờ vượt `totalSeats` của tàu. */
export interface BoatSeatInventory {
  boatId: string
  totalSeats: number
  activeSeats: number
  state: 'NotConfigured' | 'Partial' | 'Complete'
  seats: BoatSeat[]
}

/** BE `FareRuleAdminResponse` (BR-FARE-01: giá phẳng theo loại chuyến và hạng ghế). */
export interface FareRule {
  id: string
  tripType: TripType
  seatClassId: string
  seatClassCode: string
  seatClassName: string
  price: number
  currency: string
  /** ISO 8601 */
  effectiveFrom: string
  effectiveTo: string | null
  isActive: boolean
  /** Tính theo giờ hiện tại của máy chủ. */
  status: 'Inactive' | 'Upcoming' | 'Active' | 'Expired'
}

export interface FareRuleInput {
  tripType: TripType
  seatClassId: string
  price: number
  effectiveFrom: string
  effectiveTo: string | null
}

/** BE `Route.ServiceType`; tuyến `Regular` chạy chuyến `Commuter`. */
export type ServiceType = 'Regular' | 'Sightseeing'

/** BE `RouteStopInput`: thứ tự trong danh sách là thứ tự ghé bến. */
export interface RouteStopInput {
  stationId: string
  canBoard: boolean
  canAlight: boolean
}

/** BE `RouteStopResponse`. */
export interface RouteStop extends RouteStopInput {
  id: string
  sequenceNo: number
  stationName: string
}

interface RouteFields {
  code: string
  name: string
  serviceType: ServiceType
  estimatedDurationMinutes: number
  distanceKm: number
}

/** BE `RouteListItemResponse`. */
export interface RouteListItem extends RouteFields {
  id: string
  isActive: boolean
  departureStationName: string
  arrivalStationName: string
  stopCount: number
}

/** BE `RouteDetailResponse`. `structureLocked`: đã có lịch/chuyến dùng tuyến nên không đổi được bến và loại dịch vụ. */
export interface RouteDetail extends RouteFields {
  id: string
  isActive: boolean
  structureLocked: boolean
  stops: RouteStop[]
}

export interface RouteInput extends RouteFields {
  stops: RouteStopInput[]
}

export type ScheduleDirection = 'Outbound' | 'Inbound'

/** BE `ScheduleStopInput`: mốc phút tính từ giờ khởi hành (BR-STOP-01). */
export interface ScheduleStopInput {
  routeStopId: string
  arrivalOffsetMin: number | null
  departureOffsetMin: number | null
}

/** BE `ScheduleStopResponse`. */
export interface ScheduleStop extends ScheduleStopInput {
  id: string
  visitOrder: number
  sequenceNo: number
  stationId: string
  stationName: string
}

interface ScheduleFields {
  tripType: TripType
  direction: ScheduleDirection
  /** Giờ Việt Nam dạng `HH:mm:ss`. */
  departureTime: string
  /** Ngày chạy theo ISO, 1 = Thứ Hai … 7 = Chủ Nhật, ví dụ "1,2,3,4,5". */
  daysOfWeek: string
  /** `YYYY-MM-DD` */
  effectiveFrom: string
  effectiveTo: string | null
}

/** BE `ScheduleListItemResponse`. */
export interface ScheduleListItem extends ScheduleFields {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  isActive: boolean
  stopCount: number
}

/** BE `ScheduleDetailResponse`. `structureLocked`: lịch đã sinh chuyến nên không đổi được điểm dừng (BR-SCH-06). */
export interface ScheduleDetail extends ScheduleListItem {
  structureLocked: boolean
  stops: ScheduleStop[]
}

export interface ScheduleInput extends ScheduleFields {
  routeId: string
  stops: ScheduleStopInput[]
}

/** BE `GenerateTripsRequest`: tối đa 31 ngày mỗi lần, ngày theo giờ Việt Nam. */
export interface GenerateTripsInput {
  scheduleId: string
  fromDate: string
  toDate: string
  salesCloseMinutesBeforeDeparture: number | null
  checkInOpenMinutesBeforeDeparture: number | null
}

/** BE `GenerateTripsResponse`. */
export interface GenerateTripsResult {
  created: { tripId: string; departureTime: string; arrivalTime: string }[]
  skippedExisting: number
  skippedPast: number
  skippedNotOperatingDay: number
}

/** BE `CreateAdHocTripRequest`: `departureTime` is an ISO instant, the stops are in calling order. */
export interface AdHocTripInput {
  routeId: string
  tripType: TripType
  departureTime: string
  stops: ScheduleStopInput[]
  salesCloseMinutesBeforeDeparture: number | null
  checkInOpenMinutesBeforeDeparture: number | null
}

/** BE `AssignTripBoatResponse`. `remappedReservations`: sold seats moved to the same seat of the new boat. */
export interface AssignBoatResult {
  tripId: string
  boatId: string
  previousBoatId: string | null
  remappedReservations: number
}
