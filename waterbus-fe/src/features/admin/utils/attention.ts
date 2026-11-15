import { ROUTES } from '../../../routes/routes'
import type { AdminTrip, IncidentAlert } from '../../operations/types'
import { pendingCountOf } from './alertUtils'

/** Một việc Admin cần xử lý trên trang Tổng quan; `label` là chuỗi nguồn tiếng Anh có `{count}`. */
export interface AttentionItem {
  key: 'decisions' | 'suspended' | 'noBoat' | 'reconcile' | 'pendingRefunds'
  icon: string
  count: number
  label: string
  action: string
  to: string
  /** `urgent`: ảnh hưởng chuyến/khách ngay (quyết định sự cố, chuyến chưa có tàu nên chưa bán được). */
  tone: 'urgent' | 'normal'
}

/** Trang Chuyến lọc sẵn các chuyến chưa có tàu. */
const TRIPS_WITHOUT_BOAT = `${ROUTES.adminTrips}?filter=no-boat`
/** Trang Chuyến lọc sẵn các chuyến đang tạm ngưng. */
const SUSPENDED_TRIPS = `${ROUTES.adminTrips}?filter=Suspended`
/** Tab của trang Hoàn tiền. */
const refundsTab = (tab: 'pending' | 'reconcile') => `${ROUTES.adminRefunds}?tab=${tab}`

/** Chuyến còn theo lịch mà chưa gán tàu: chưa mở bán (BR-SCH-04). */
export const isWithoutBoat = (trip: Pick<AdminTrip, 'status' | 'boatId'>) => trip.status === 'Scheduled' && !trip.boatId

/**
 * Việc cần xử lý, việc gấp trước; chỉ trả về việc có số lượng > 0. Số hoàn tiền là `undefined` khi không đọc được
 * (khi đó bỏ qua, không làm hỏng cả trang Tổng quan).
 */
export function attentionItems({
  trips,
  alerts,
  pendingRefunds,
  reconcile,
}: {
  trips: Pick<AdminTrip, 'status' | 'boatId'>[]
  alerts: Pick<IncidentAlert, 'pendingDecisionCount' | 'trips'>[]
  pendingRefunds?: number
  reconcile?: number
}): AttentionItem[] {
  const items: AttentionItem[] = [
    {
      key: 'decisions',
      icon: 'warning',
      count: alerts.reduce((sum, alert) => sum + pendingCountOf(alert), 0),
      label: '{count} trips are waiting for your decision on an alert',
      action: 'Decide',
      to: ROUTES.adminIncidents,
      tone: 'urgent',
    },
    {
      key: 'suspended',
      icon: 'pause',
      count: trips.filter((trip) => trip.status === 'Suspended').length,
      label: '{count} trips are suspended and need a decision',
      action: 'Review trips',
      to: SUSPENDED_TRIPS,
      tone: 'urgent',
    },
    {
      key: 'noBoat',
      icon: 'directions_boat',
      count: trips.filter(isWithoutBoat).length,
      label: '{count} trips have no boat and are not on sale',
      action: 'Assign boats',
      to: TRIPS_WITHOUT_BOAT,
      tone: 'urgent',
    },
    {
      key: 'reconcile',
      icon: 'swap_horiz',
      count: reconcile ?? 0,
      label: '{count} payments or refunds need reconciling',
      action: 'Reconcile',
      to: refundsTab('reconcile'),
      tone: 'normal',
    },
    {
      key: 'pendingRefunds',
      icon: 'receipt_long',
      count: pendingRefunds ?? 0,
      label: '{count} refunds are waiting for staff',
      action: 'Open refunds',
      to: refundsTab('pending'),
      tone: 'normal',
    },
  ]
  return items.filter((item) => item.count > 0)
}
