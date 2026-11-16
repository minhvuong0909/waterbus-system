import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import { fetchAllPages } from '../../../services/paging'
import type { AdminTrip, AlertTrip, CaptainActionHint, LiveBoatPosition, OperationLog, OperationType, IncidentAlert } from '../types'
import type { TripStatus, TripType } from '../../trips/types'

// Các thao tác vận hành dùng API thật: chuyến và trạng thái, theo dõi tàu và sự cố.
// Cập nhật tức thời cho cùng dữ liệu này đến từ hub theo dõi tàu (xem useFleetHub).

interface BoatLatestPositionDto {
  boatId: string
  tripId: string | null
  latitude: number
  longitude: number
  speedKnots: number | null
  observedAt: string
}

interface IncidentSummaryDto {
  id: string
  severity: string | null
  description: string | null
  occurredAt: string | null
  affectedTripCount: number
  pendingDecisionCount?: number
  routeCode?: string | null
  boatCode?: string | null
  source?: string
  resolvedAt?: string | null
}

/** Payload của sự kiện hub `boatPosition` (lưu ý `lat` / `lng`, khác với ảnh chụp REST). */
export interface BoatPositionEvent {
  boatId: string
  tripId: string | null
  lat: number
  lng: number
  speedKnots: number | null
  observedAt: string
}

/** Payload của sự kiện hub `tripStatusChanged`. */
export interface TripStatusEvent {
  tripId: string
  boatId: string | null
  status: TripStatus
  previousStatus: TripStatus
  operation: OperationType
  changedAt: string
}

/** Payload của sự kiện hub `incidentRaised` (SOS của thuyền trưởng). */
export interface IncidentRaisedEvent {
  incidentId: string
  incidentType: string
  severity: string | null
  tripId: string | null
  boatId: string | null
  message: string
  occurredAt: string
}

export const getLatestBoatPositions = () => apiRequest<BoatLatestPositionDto[]>(API_ENDPOINTS.tracking.boats)

export function fromPositionEvent(event: BoatPositionEvent): LiveBoatPosition {
  return {
    boatId: event.boatId,
    tripId: event.tripId,
    latitude: event.lat,
    longitude: event.lng,
    speedKnots: event.speedKnots,
    observedAt: event.observedAt,
  }
}

const levelOf = (severity: string | null): 1 | 2 => (severity === 'High' ? 2 : 1)

interface IncidentSummaryFull extends IncidentSummaryDto {
  incidentType: string
}

interface IncidentDetailDto {
  affectedTrips: {
    tripId: string
    operationDecision: string | null
    departureTime?: string
    /** Backend gửi số của enum ở đây (1 = Scheduled … 8 = Terminated). */
    tripStatus?: number | string
    decisionAt?: string | null
  }[]
}

const TRIP_STATUS_BY_NUMBER: TripStatus[] = [
  'Scheduled', 'Boarding', 'EnRoute', 'Suspended', 'Arrived', 'Completed', 'Cancelled', 'Terminated',
]

/** Trạng thái chuyến gửi dạng số của enum hoặc tên. */
function tripStatusOf(value: number | string | undefined): TripStatus | undefined {
  if (typeof value === 'number') return TRIP_STATUS_BY_NUMBER[value - 1]
  return TRIP_STATUS_BY_NUMBER.find((status) => status === value)
}

export const INCIDENT_KINDS = ['Safety', 'Technical', 'Other'] as const
const KINDS = INCIDENT_KINDS

/**
 * Mọi sự cố đang mở, mới nhất trước (chủ yếu là SOS của thuyền trưởng). Backend đã loại sự cố thời tiết cũ khỏi danh sách
 * mặc định. Đọc hết các trang
 * (danh sách tóm tắt đã có `affectedTripCount`/`pendingDecisionCount`, đủ để hiển thị số chuyến chờ quyết định).
 * Danh sách chuyến của từng sự cố tải riêng qua `getIncidentTrips` khi Admin mở sự cố đó, không gọi N+1 mỗi lần tải lại.
 */
export async function getOpenIncidents(incidentType?: string): Promise<IncidentAlert[]> {
  const items = await fetchAllPages<IncidentSummaryFull>(API_ENDPOINTS.incidents.list, { status: 'Open', incidentType })
  return items.map(toAlert)
}

function toAlert(i: IncidentSummaryFull): IncidentAlert {
  return {
    id: i.id,
    level: levelOf(i.severity),
    message: i.description ?? '',
    issuedAt: i.occurredAt ?? new Date().toISOString(),
    kind: KINDS.find((k) => k === i.incidentType) ?? 'Other',
    affectedTripCount: i.affectedTripCount,
    pendingDecisionCount: i.pendingDecisionCount,
    routeCode: i.routeCode ?? null,
    boatCode: i.boatCode ?? null,
    source: i.source,
    resolvedAt: i.resolvedAt ?? null,
  }
}

/**
 * Một trang sự cố đã đóng (BE `Status = Resolved`), mới nhất trước, để Admin xem lại quyết định đã ghi. Phân trang ở
 * server vì danh sách này tăng mãi.
 */
export async function getClosedIncidents(pageNumber: number, pageSize = 10): Promise<{ items: IncidentAlert[]; totalCount: number }> {
  const page = await apiRequest<{ items: IncidentSummaryFull[]; totalCount: number }>(API_ENDPOINTS.incidents.list, {
    query: { status: 'Resolved', pageNumber, pageSize },
  })
  return { items: page.items.map(toAlert), totalCount: page.totalCount }
}

/** Các chuyến một sự cố ảnh hưởng và quyết định của Admin cho từng chuyến (null = chưa quyết định). */
export async function getIncidentTrips(incidentId: string): Promise<AlertTrip[]> {
  const detail = await apiRequest<IncidentDetailDto>(API_ENDPOINTS.incidents.detail(incidentId))
  return detail.affectedTrips.map((t) => ({
    tripId: t.tripId,
    decision: t.operationDecision,
    departureTime: t.departureTime,
    tripStatus: tripStatusOf(t.tripStatus),
    decisionAt: t.decisionAt ?? null,
  }))
}

export const resolveIncident = (incidentId: string) =>
  apiRequest<{ incidentId: string; status: string }>(API_ENDPOINTS.incidents.resolve(incidentId), { method: 'POST' })

/** Knots (BE) to km/h (UI). */
export const knotsToKmh = (knots: number | null | undefined) => (knots == null ? null : Math.round(knots * 1.852 * 10) / 10)

// ---- Chuyến cho vận hành ----------------------------------------------------

interface TripOperationItemDto {
  id: string
  routeId: string
  routeCode: string
  routeName: string
  boatId: string | null
  boatCode: string | null
  tripType: TripType
  status: TripStatus
  departureTime: string
  arrivalTime: string
  allowedOperations: OperationType[]
  captainActions?: CaptainActionHint[] | null
}

/** Khoảng thời gian và bộ lọc của `getOperationTrips`; backend cho tối đa 31 ngày giữa `from` và `to`. */
export interface OperationTripsQuery {
  /** ISO 8601 */
  from?: string
  /** ISO 8601 */
  to?: string
  statuses?: TripStatus[]
}

/** Hai giờ khởi hành cách nhau dưới mức này thì gom chung một lần gọi (backend cho tối đa 31 ngày, trần 200 chuyến). */
const AROUND_CLUSTER_MS = 3 * 24 * 60 * 60 * 1000
const AROUND_MARGIN_MS = 60_000

/**
 * Các chuyến vận hành quanh những giờ khởi hành cho trước. Dùng cho chuyến của một sự cố nằm ngoài cửa sổ mặc định của
 * `getOperationTrips` (12 giờ trước đến 7 ngày tới): vẫn lấy được để Admin quyết định. Giờ gần nhau gom chung một lần gọi.
 */
export async function getOperationTripsAround(departureTimes: string[]): Promise<AdminTrip[]> {
  const times = departureTimes.map((iso) => Date.parse(iso)).filter(Number.isFinite).sort((a, b) => a - b)
  const clusters: { from: number; to: number }[] = []
  for (const time of times) {
    const last = clusters.at(-1)
    if (last && time - last.from < AROUND_CLUSTER_MS) last.to = time
    else clusters.push({ from: time, to: time })
  }
  const lists = await Promise.all(
    clusters.map((c) =>
      getOperationTrips({
        from: new Date(c.from - AROUND_MARGIN_MS).toISOString(),
        to: new Date(c.to + AROUND_MARGIN_MS).toISOString(),
      }),
    ),
  )
  return lists.flat()
}

/**
 * Các chuyến kèm thao tác backend cho phép trên từng chuyến lúc này. Không có query: từ 12 giờ trước đến 7 ngày tới
 * (mặc định của backend).
 */
export async function getOperationTrips(query?: OperationTripsQuery): Promise<AdminTrip[]> {
  const items = await apiRequest<TripOperationItemDto[]>(API_ENDPOINTS.trips.operations, query ? { query: { ...query } } : undefined)
  return items.map((t) => ({
    id: t.id,
    routeId: t.routeId,
    boatId: t.boatId ?? '',
    departureTime: t.departureTime,
    arrivalTime: t.arrivalTime,
    tripType: t.tripType,
    status: t.status,
    routeCode: t.routeCode,
    routeName: t.routeName,
    boatCode: t.boatCode,
    allowedOperations: t.allowedOperations,
    captainActions: t.captainActions ?? undefined,
  }))
}

interface TripOperationEventDto {
  id: string
  tripId: string
  action: string
  note: string | null
  occurredAt: string
  actedBy: string | null
}

export async function getOperationLogs(): Promise<OperationLog[]> {
  const events = await apiRequest<TripOperationEventDto[]>(API_ENDPOINTS.trips.operationEvents, { query: { take: 50 } })
  return events.map((e) => ({
    id: e.id,
    type: e.action as OperationType,
    tripId: e.tripId,
    reason: e.note ?? '',
    by: e.actedBy ?? '—',
    at: e.occurredAt,
  }))
}

/**
 * Đổi trạng thái chuyến ở backend. Backend kiểm tra ai được làm gì: thuyền trưởng mở đón khách, xuất bến và cập bến
 * trên tàu của mình; Admin quyết định mọi việc khác. Truyền `incidentId` để ghi quyết định vào sự cố. SOS của
 * thuyền trưởng tạo sự cố và giữ nguyên chuyến.
 */
export async function operateTrip(
  tripId: string,
  type: OperationType,
  reason: string,
  incidentId?: string,
): Promise<{ status: TripStatus | null }> {
  if (type === 'SOS') {
    await apiRequest(API_ENDPOINTS.trips.sos(tripId), { method: 'POST', body: { note: reason } })
    return { status: null }
  }
  const result = await apiRequest<{ status: TripStatus }>(API_ENDPOINTS.trips.status(tripId), {
    method: 'POST',
    body: { operation: type, reason: reason || null, incidentId: incidentId ?? null },
  })
  return { status: result.status }
}
