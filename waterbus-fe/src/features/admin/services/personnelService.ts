import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type {
  AttendanceRecord, Captain, CaptainAlert, CaptainAuditEvent, CaptainDraft, CaptainStatus, StaffAccount, StaffAccountDraft, TripAttendance,
} from '../types/personnel'
import type { Paged } from '../types/refunds'

const PAGE_SIZE = 20
const admin = API_ENDPOINTS.admin

// ---------- Captain (BR-CAP-01/02/07: Admin tạo, mời kích hoạt, đổi trạng thái; mọi thay đổi cần lý do) ----------

export const listCaptains = (page: number, search: string, status: CaptainStatus | '') =>
  apiRequest<Paged<Captain>>(admin.captains, {
    query: { page, pageSize: PAGE_SIZE, search: search.trim() || undefined, status: status || undefined },
  })

/** `POST /captains`: không có mật khẩu, Captain tự đặt qua email kích hoạt. */
export const createCaptain = (draft: CaptainDraft) => apiRequest<string>(admin.captains, { method: 'POST', body: draft })

export const updateCaptain = (id: string, draft: CaptainDraft, reason: string) =>
  apiRequest<void>(admin.captain(id), { method: 'PUT', body: { ...draft, reason: reason.trim() } })

export const changeCaptainStatus = (id: string, status: CaptainStatus, reason: string) =>
  apiRequest<void>(admin.captainAction(id, 'status'), { method: 'PUT', body: { status, reason: reason.trim() } })

export const resendCaptainInvitation = (id: string, reason: string) =>
  apiRequest<void>(admin.captainAction(id, 'invitation'), { method: 'POST', body: { reason: reason.trim() } })

/** Mọi Captain (danh sách ngắn) để hiện tên trong cảnh báo, nhật ký và chọn khi điểm danh. */
export async function listAllCaptains(): Promise<Captain[]> {
  const captains: Captain[] = []
  for (let page = 1; ; page++) {
    const result = await apiRequest<Paged<Captain>>(admin.captains, { query: { page, pageSize: 100 } })
    captains.push(...result.items)
    if (page >= result.totalPages) return captains
  }
}

/** `PUT /boats/{id}/captain`: gán Captain cho tàu, hoặc gỡ khi `captainId` là null. Cần lý do (BR-CAP-04/07). */
export const assignBoatCaptain = (boatId: string, captainId: string | null, reason: string) =>
  apiRequest<void>(admin.boatCaptain(boatId), { method: 'PUT', body: { captainId, reason: reason.trim() } })

export const getTripAttendance = (tripId: string) => apiRequest<TripAttendance>(API_ENDPOINTS.trips.captainAttendance(tripId))

/** Admin ghi nhận Captain có mặt; giờ ghi nhận do server tự đặt (BR-CAP-11). */
export const recordTripAttendance = (tripId: string, body: { captainId: string; assignmentVersion: string; reason: string }) =>
  apiRequest<AttendanceRecord>(API_ENDPOINTS.trips.captainAttendance(tripId), {
    method: 'POST', body: { ...body, reason: body.reason.trim() },
  })

export const listCaptainAlerts = (page: number) =>
  apiRequest<Paged<CaptainAlert>>(admin.captainAlerts, { query: { page, pageSize: PAGE_SIZE } })

export const resolveCaptainAlert = (id: string) => apiRequest<void>(admin.resolveCaptainAlert(id), { method: 'POST' })

export const listCaptainAudit = (page: number) =>
  apiRequest<Paged<CaptainAuditEvent>>(admin.captainAudit, { query: { page, pageSize: PAGE_SIZE } })

// ---------- Staff ----------

export const listStaffAccounts = (page: number, search: string, isActive: boolean | '') =>
  apiRequest<Paged<StaffAccount>>(admin.staffAccounts, {
    query: { page, pageSize: PAGE_SIZE, search: search.trim() || undefined, isActive: isActive === '' ? undefined : isActive },
  })

const staffBody = (draft: StaffAccountDraft) => ({ ...draft, staffCode: draft.staffCode.trim() || null })

export const createStaffAccount = (draft: StaffAccountDraft) =>
  apiRequest<string>(admin.staffAccounts, { method: 'POST', body: staffBody(draft) })

export const updateStaffAccount = (id: string, draft: StaffAccountDraft) =>
  apiRequest<void>(admin.staffAccount(id), { method: 'PUT', body: staffBody(draft) })

export const changeStaffAccountStatus = (id: string, isActive: boolean) =>
  apiRequest<void>(admin.staffAccountAction(id, 'status'), { method: 'PUT', body: { isActive } })

export const resendStaffInvitation = (id: string, reason: string) =>
  apiRequest<void>(admin.staffAccountAction(id, 'invitation'), { method: 'POST', body: { reason: reason.trim() } })
