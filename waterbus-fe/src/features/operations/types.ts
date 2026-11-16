import type { TripStatus, TripType } from '../trips/types'

/** Một chuyến như danh sách vận hành trả về (BE `TripOperationItemDto`). */
export interface AdminTrip {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  /** '' khi chưa gán tàu. */
  boatId: string
  boatCode: string | null
  /** ISO 8601 */
  departureTime: string
  arrivalTime: string
  tripType: TripType
  status: TripStatus
  /** Những gì backend cho phép với chuyến này lúc này (BE `TripLifecycle`). */
  allowedOperations: OperationType[]
  /** Trạng thái từng nút của thuyền trưởng kèm lý do khi chưa bấm được (BE `CaptainOperationRules`); chỉ có cho thuyền trưởng. */
  captainActions?: CaptainActionHint[]
  /** Chuyến đang có SOS còn mở (chỉ có cho thuyền trưởng): tàu dừng chờ Admin xử lý. */
  openSos?: boolean
}

/** Một nút của thuyền trưởng: bấm được hay chưa, và vì sao chưa. */
export interface CaptainActionHint {
  operation: OperationType
  allowed: boolean
  /** NotYet | OpenBoardingFirst | NoPosition | NotAtLastStop | JourneyNotDone | WrongStatus */
  reason: string | null
  /** Với `NotYet`: giờ được mở đón khách (ISO 8601). */
  availableAt: string | null
}

/** Một sự cố đang mở hiện dạng cảnh báo vận hành, chủ yếu là báo khẩn cấp (SOS) của thuyền trưởng. */
export interface IncidentAlert {
  id: string
  /** 1 = cảnh báo, 2 = dừng vận hành (mức BE Medium = 1, High = 2). */
  level: 1 | 2
  message: string
  /** ISO 8601 */
  issuedAt: string
  /** BE `IncidentType`. */
  kind?: 'Safety' | 'Technical' | 'Other'
  /** Số chuyến liên kết với cảnh báo (BE `IncidentTrip`), nếu biết. */
  affectedTripCount?: number
  /** Số chuyến liên kết mà Admin chưa quyết định (BE `PendingDecisionCount`). */
  pendingDecisionCount?: number
  /** Tuyến của sự cố, vd "RT01", nếu có. */
  routeCode?: string | null
  /** BE `Source`: `Captain` là báo cáo khẩn cấp của thuyền trưởng. */
  source?: string
  /** Các chuyến liên kết và quyết định của Admin cho từng chuyến (null = chưa quyết định). Chỉ có khi yêu cầu. */
  trips?: AlertTrip[]
}

/** Một chuyến bị cảnh báo ảnh hưởng (BE `AffectedTripDto`). */
export interface AlertTrip {
  tripId: string
  decision: string | null
  departureTime?: string
  tripStatus?: TripStatus
  decisionAt?: string | null
}

/** Việc Admin hoặc thuyền trưởng đã làm với một chuyến, lưu làm nhật ký (BE `TripOperationEvent`). */
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

/** Vị trí gần nhất của một tàu, từ `GET /tracking/boats` hoặc sự kiện hub `boatPosition`. */
export interface LiveBoatPosition {
  boatId: string
  /** Có giá trị khi tàu đang chạy một chuyến (Boarding, EnRoute hoặc Suspended). */
  tripId: string | null
  latitude: number
  longitude: number
  speedKnots: number | null
  /** ISO 8601 */
  observedAt: string
}

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
  /** Số ghế đang hoạt động đã thiết lập trên tàu; tối đa `totalSeats`. */
  configuredSeats?: number
}
