import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { apiRequest } from '../../../services/apiClient'
import type { Paged } from '../types/refunds'

export interface ScannerStaff {
  id: string
  fullName: string
  staffCode: string | null
  email: string | null
}

export interface ScannerDevice {
  id: string
  deviceCode: string
  label: string | null
  isActive: boolean
}

export interface AssignScannerRequest {
  deviceId: string
  staffAccountId: string
  tripStopCallId: string
  isPrimaryOffline: boolean
  activeFrom: string
  activeUntil: string | null
}

export interface ScannerAssignment {
  assignmentId: string
  staffAccountId: string
  staffName: string | null
  deviceCode: string
  isPrimaryOffline: boolean
  activeFrom: string
  activeUntil: string | null
}

async function listOptions<T>(url: string, activeOnly = false): Promise<T[]> {
  const load = (pageNumber: number) => apiRequest<Paged<T>>(url, {
    query: { pageNumber, pageSize: 100, isActive: activeOnly || undefined },
  })
  const first = await load(1)
  const options = [...first.items]
  for (let page = 2; page <= first.totalPages; page++) {
    options.push(...(await load(page)).items)
  }
  return options
}

export const listScannerStaff = () => listOptions<ScannerStaff>(API_ENDPOINTS.checkIns.scannerStaff)
export const listScannerDevices = () => listOptions<ScannerDevice>(API_ENDPOINTS.admin.scannerDevices, true)

export const registerScannerDevice = (deviceCode: string, label: string | null) =>
  apiRequest<string>(API_ENDPOINTS.admin.scannerDevices, { method: 'POST', body: { deviceCode, label } })

export const listStopAssignments = (callId: string, pageNumber: number) =>
  apiRequest<Paged<ScannerAssignment>>(API_ENDPOINTS.checkIns.stopAssignments(callId), { query: { pageNumber, pageSize: 20 } })

export const assignScanner = (body: AssignScannerRequest) =>
  apiRequest<AssignScannerRequest & { assignmentId: string }>(API_ENDPOINTS.checkIns.assignments, { method: 'POST', body })

export const revokeScannerAssignment = (id: string) =>
  apiRequest<{ assignmentId: string; activeUntil: string }>(API_ENDPOINTS.checkIns.revokeAssignment(id), { method: 'POST' })
