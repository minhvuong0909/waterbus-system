import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../services/apiClient'
import { createShift, listScannerStaff, listShifts, revokeShift } from '../../features/admin/services/scannerAssignmentService'
import type { StaffShift } from '../../features/admin/services/scannerAssignmentService'
import { getStations } from '../../features/admin/services/stationService'
import type { AdminStation } from '../../features/admin/types'
import StaffStationShiftsPage from './StaffStationShiftsPage'

vi.mock('../../features/admin/services/scannerAssignmentService')
vi.mock('../../features/admin/services/stationService')

const shift: StaffShift = {
  id: 'shift1', staffAccountId: 'staff1', staffName: 'An', stationId: 'station1', stationName: 'Bạch Đằng',
  activeFrom: '2090-01-01T00:00:00Z', activeUntil: '2090-01-01T05:00:00Z', revokedAt: null,
}

const click = (name: string) => fireEvent.click(screen.getByRole('button', { name }))
const change = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } })

beforeEach(() => {
  vi.mocked(getStations).mockResolvedValue([{ id: shift.stationId, name: shift.stationName, isActive: true } as AdminStation])
  vi.mocked(listScannerStaff).mockResolvedValue([{ id: shift.staffAccountId, fullName: 'An', staffCode: 'ST01', email: null }])
  vi.mocked(listShifts).mockResolvedValue({ items: [shift], pageNumber: 1, totalCount: 1, totalPages: 1 })
})

afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('Staff station shifts', () => {
  it('validates local shift times, posts UTC, and reports overlapping shifts', async () => {
    vi.mocked(createShift).mockRejectedValue(new ApiError(409, 'Staff shift overlaps another shift'))
    render(<StaffStationShiftsPage />)
    await screen.findByRole('option', { name: /ST01/ })
    change('Staff account', shift.staffAccountId)
    change('Station', shift.stationId)
    change('Start', '2090-01-01T07:00')
    change('End', '2090-01-01T06:59')
    click('Create shift')
    expect(screen.getByText(/Enter an end time/)).toBeTruthy()
    expect(createShift).not.toHaveBeenCalled()
    change('End', '2090-01-01T12:00')
    click('Create shift')
    expect(await screen.findByText('Staff shift overlaps another shift')).toBeTruthy()
    expect(createShift).toHaveBeenCalledWith({
      staffAccountId: shift.staffAccountId, stationId: shift.stationId,
      activeFrom: '2090-01-01T00:00:00.000Z', activeUntil: '2090-01-01T05:00:00.000Z',
    })
  })

  it('confirms revocation and keeps the shift in history', async () => {
    vi.mocked(revokeShift).mockResolvedValue(undefined)
    render(<StaffStationShiftsPage />)
    fireEvent.click(await screen.findByRole('button', { name: 'Revoke shift' }))
    expect(revokeShift).not.toHaveBeenCalled()
    vi.mocked(listShifts).mockResolvedValue({ items: [{ ...shift, revokedAt: '2089-12-31T00:00:00Z' }],
      pageNumber: 1, totalCount: 1, totalPages: 1 })
    click('Confirm revoke')
    await waitFor(() => expect(revokeShift).toHaveBeenCalledWith(shift.id))
    expect(await screen.findByText(/· Revoked/)).toBeTruthy()
    expect(screen.getByText('An · Bạch Đằng')).toBeTruthy()
  })
})
