import { ROLES } from '../../constant/roles'
import { ROUTES } from '../../routes/routes'

/** Nhóm của sidebar: việc chạy trong ngày, nhân sự, danh mục cấu hình, tiền, nội dung. */
export type AdminNavGroup = 'Operations' | 'Personnel' | 'Catalogue' | 'Finance' | 'Content'

export interface AdminNavItem {
  to: string
  label: string
  icon: string
  group: AdminNavGroup
  /** Các vai trò được mở trang. */
  roles: readonly string[]
}

const { admin } = ROLES

/** Thứ tự nhóm trên sidebar. */
export const ADMIN_NAV_GROUPS: AdminNavGroup[] = ['Operations', 'Personnel', 'Catalogue', 'Finance', 'Content']

/**
 * Điều hướng khu vực quản trị. Trong nhóm Danh mục, thứ tự đi theo phụ thuộc dữ liệu: bến → tuyến → lịch chạy,
 * tàu → hạng ghế → giá vé.
 */
export const ADMIN_NAV: AdminNavItem[] = [
  { to: ROUTES.admin, label: 'Overview', icon: 'dashboard', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminRadar, label: 'Live Operations', icon: 'radar', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminTrips, label: 'Trips', icon: 'directions_boat', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminIncidents, label: 'Incidents', icon: 'warning', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminCheckIn, label: 'Check-in', icon: 'how_to_reg', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminScanners, label: 'Scanners', icon: 'qr_code_scanner', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminStaffShifts, label: 'Staff shifts', icon: 'schedule', group: 'Operations', roles: [admin] },
  { to: ROUTES.adminCaptains, label: 'Captains', icon: 'person', group: 'Personnel', roles: [admin] },
  { to: ROUTES.adminStaffAccounts, label: 'Staff accounts', icon: 'badge', group: 'Personnel', roles: [admin] },
  { to: ROUTES.adminStations, label: 'Stations', icon: 'anchor', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminRoutes, label: 'Routes', icon: 'directions', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminSchedules, label: 'Schedules', icon: 'calendar_today', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminBoats, label: 'Boats', icon: 'sailing', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminSeatClasses, label: 'Seat classes', icon: 'event_seat', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminFares, label: 'Fares', icon: 'payments', group: 'Catalogue', roles: [admin] },
  { to: ROUTES.adminRevenue, label: 'Revenue', icon: 'account_balance', group: 'Finance', roles: [admin] },
  { to: ROUTES.adminRefunds, label: 'Refunds', icon: 'receipt_long', group: 'Finance', roles: [admin] },
  { to: ROUTES.adminAudioGuides, label: 'Audio guides', icon: 'campaign', group: 'Content', roles: [admin] },
]
