/** BE `CaptainStatus`: API trả và nhận dạng số, không phải chuỗi. */
export const CAPTAIN_STATUS = { Active: 1, Suspended: 2, Inactive: 3 } as const
export type CaptainStatus = (typeof CAPTAIN_STATUS)[keyof typeof CAPTAIN_STATUS]

/** BE `CaptainResponse`. */
export interface Captain {
  id: string
  fullName: string
  email: string
  phone: string | null
  licenseNumber: string | null
  /** Ngày hết hạn giấy phép, `YYYY-MM-DD`. */
  licenseExpiresOn: string | null
  status: CaptainStatus
  boatId: string | null
  /** Đã gửi lời mời nhưng Captain chưa đặt mật khẩu. */
  invitationPending: boolean
  emailConfirmed: boolean
}

export interface CaptainDraft {
  fullName: string
  email: string
  phone: string
  licenseNumber: string
  licenseExpiresOn: string
}

/** BE `StaffAccountResponse`. */
export interface StaffAccount {
  id: string
  fullName: string
  email: string
  phone: string | null
  staffCode: string | null
  isActive: boolean
  invitationPending: boolean
  emailConfirmed: boolean
}

export interface StaffAccountDraft {
  fullName: string
  email: string
  phone: string
  staffCode: string
}

/** Trạng thái điểm danh Captain của một chuyến (BR-CAP-09/12). */
export type AttendanceState = 'Pending' | 'Missing' | 'Present' | 'Late' | 'NotConfigured'

/** BE `CaptainAttendanceResponse`: bản ghi Admin xác nhận Captain có mặt. */
export interface AttendanceRecord {
  id: string
  tripId: string
  boatId: string
  captainId: string
  assignmentVersion: string
  recordedByAdminId: string
  recordedAt: string
  requiredAt: string
  isLate: boolean
  reason: string
}

/** BE `CaptainTripAttendanceResponse`. `assignmentVersion` phải gửi lại khi ghi nhận để không xác nhận nhầm sau khi đổi phân công. */
export interface TripAttendance {
  tripId: string
  boatId: string | null
  captainId: string | null
  assignmentVersion: string | null
  requiredAt: string | null
  state: AttendanceState
  attendance: AttendanceRecord | null
}

/** Cảnh báo vận hành Captain (BR-CAP-10); không tự đổi trạng thái chuyến. */
export interface CaptainAlert {
  id: string
  tripId: string | null
  boatId: string
  captainId: string | null
  type: string
  message: string
  occurredAt: string
  resolvedAt: string | null
  resolvedByAdminId: string | null
}

/** Một dòng nhật ký thao tác với Captain (BR-CAP-07). */
export interface CaptainAuditEvent {
  id: string
  captainId: string
  boatId: string | null
  tripId: string | null
  actedByCaptainId: string | null
  actedByAdminId: string | null
  occurredAt: string
  action: string
  oldValues: string | null
  newValues: string
  reason: string
}
