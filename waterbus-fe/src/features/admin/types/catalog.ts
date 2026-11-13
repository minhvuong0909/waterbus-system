import type { TripType } from '../../trips/types'

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
