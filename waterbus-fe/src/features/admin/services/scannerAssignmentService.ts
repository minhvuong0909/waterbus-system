import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { Paged } from '../types/refunds'

export interface ScannerStaff {
  id: string
  fullName: string
  staffCode: string | null
  email: string | null
}

export type ScannerStatus = 'Unbound' | 'PendingActivation' | 'Active' | 'Suspended' | 'Revoked'

export interface ScannerDevice {
  id: string
  deviceCode: string
  label: string | null
  isActive: boolean
  stationId: string | null
  stationName: string | null
  status: ScannerStatus
  invitationExpiresAt: string | null
  activeAssignmentCount: number
  checkInEventCount: number
}

export interface AssignScannerRequest {
  deviceId: string
  tripStopCallId: string
  activeFrom: string
  activeUntil: string
}

export interface ScannerAssignment {
  assignmentId: string
  deviceCode: string
  isPrimaryOffline: boolean
  activeFrom: string
  activeUntil: string | null
}

export interface StaffShiftDraft {
  staffAccountId: string
  stationId: string
  activeFrom: string
  activeUntil: string
}

export interface StaffShift extends StaffShiftDraft {
  id: string
  staffName: string
  stationName: string
  revokedAt: string | null
}

export interface ScannerAuditEvent {
  id: string
  tripStopCallId: string
  staffAccountId: string
  ticketId: string
  outcome: string
  occurredAtDevice: string
  receivedAtServer: string | null
  reviewedAt: string | null
  reviewNote: string | null
}

const devices = API_ENDPOINTS.admin.scannerDevices
const shifts = API_ENDPOINTS.admin.staffStationShifts

async function listOptions<T>(url: string, query: Record<string, string | undefined> = {}): Promise<T[]> {
  const load = (pageNumber: number) => apiRequest<Paged<T>>(url, { query: { ...query, pageNumber, pageSize: 100 } })
  const first = await load(1)
  const options = [...first.items]
  for (let page = 2; page <= first.totalPages; page++) options.push(...(await load(page)).items)
  return options
}

export const listScannerStaff = () => listOptions<ScannerStaff>(API_ENDPOINTS.checkIns.scannerStaff)
export const listScannerDevices = (stationId: string) => listOptions<ScannerDevice>(devices, { stationId, status: 'Active' })

export const listDevicesPage = (pageNumber: number, search: string, stationId: string, status: string) =>
  apiRequest<Paged<ScannerDevice>>(devices, {
    query: { pageNumber, pageSize: 20, search: search || undefined, stationId: stationId || undefined, status: status || undefined },
  })

export const registerScannerDevice = (stationId: string, label: string | null) =>
  apiRequest<string>(devices, { method: 'POST', body: { stationId, label } })

/** `PUT /admin/scanner-devices/{id}`: đổi nhãn gợi nhớ của máy (mã máy và bến giữ nguyên). */
export const updateScannerLabel = (id: string, label: string) =>
  apiRequest<void>(`${devices}/${id}`, { method: 'PUT', body: { label: label.trim() || null } })

export const requestScannerInvitation = (id: string) =>
  apiRequest<{ activation: string; expiresAt: string }>(API_ENDPOINTS.admin.scannerDeviceAction(id, 'invitation'), { method: 'POST' })

export const cancelScannerInvitation = (id: string) =>
  apiRequest<void>(API_ENDPOINTS.admin.scannerDeviceAction(id, 'invitation'), { method: 'DELETE' })

export const changeScannerState = (id: string, action: 'activate' | 'deactivate' | 'revoke' | 'transfer', stationId?: string) =>
  apiRequest<void>(API_ENDPOINTS.admin.scannerDeviceAction(id, action), { method: 'POST', body: stationId ? { stationId } : undefined })

export const listScannerAudit = (id: string, pageNumber: number) =>
  apiRequest<Paged<ScannerAuditEvent>>(API_ENDPOINTS.admin.scannerDeviceAction(id, 'audit'), { query: { pageNumber, pageSize: 20 } })

export const reviewScannerEvent = (scannerId: string, eventId: string, approve: boolean, reason: string) =>
  apiRequest<void>(API_ENDPOINTS.admin.scannerDeviceAction(scannerId, `audit/${eventId}/review`), {
    method: 'POST', body: { approve, reason },
  })

export const listShifts = (pageNumber: number) =>
  apiRequest<Paged<StaffShift>>(shifts, { query: { pageNumber, pageSize: 20 } })

export const createShift = (body: StaffShiftDraft) =>
  apiRequest<string>(shifts, { method: 'POST', body })

export const revokeShift = (id: string) =>
  apiRequest<void>(API_ENDPOINTS.admin.revokeStaffShift(id), { method: 'POST' })

export const listStopAssignments = (callId: string, pageNumber: number) =>
  apiRequest<Paged<ScannerAssignment>>(API_ENDPOINTS.checkIns.stopAssignments(callId), { query: { pageNumber, pageSize: 20 } })

export const assignScanner = (body: AssignScannerRequest) =>
  apiRequest<string>(API_ENDPOINTS.checkIns.assignments, { method: 'POST', body })

export const revokeScannerAssignment = (id: string) =>
  apiRequest<void>(API_ENDPOINTS.checkIns.revokeAssignment(id), { method: 'POST' })
