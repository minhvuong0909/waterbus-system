import { change } from '../../test/fields'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../services/apiClient'
import {
  cancelScannerInvitation, changeScannerState, listDevicesPage, registerScannerDevice, requestScannerInvitation,
} from '../../features/admin/services/scannerAssignmentService'
import type { ScannerDevice } from '../../features/admin/services/scannerAssignmentService'
import { getStations } from '../../features/admin/services/stationService'
import type { AdminStation } from '../../features/admin/types'
import ScannerManagementPage from './ScannerManagementPage'

vi.mock('../../features/admin/services/scannerAssignmentService')
vi.mock('../../features/admin/services/stationService')

const station = { id: 'station-a', name: 'Bạch Đằng', code: 'BD', isActive: true } as AdminStation
const scanner: ScannerDevice = {
  id: 'scanner-a', deviceCode: 'SC-BD-001', label: 'Gate 1', stationId: station.id, stationName: station.name,
  status: 'PendingActivation', isActive: false, activeAssignmentCount: 0, checkInEventCount: 0, invitationExpiresAt: null,
}

const click = (name: string) => fireEvent.click(screen.getByRole('button', { name }))

beforeEach(() => {
  vi.mocked(getStations).mockResolvedValue([station])
  vi.mocked(listDevicesPage).mockResolvedValue({ items: [scanner], pageNumber: 1, totalCount: 21, totalPages: 2 })
})

afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('Scanner management', () => {
  it('creates a station-bound device without a client code and displays a revocable activation QR', async () => {
    vi.mocked(registerScannerDevice).mockResolvedValue(scanner.id)
    vi.mocked(requestScannerInvitation).mockResolvedValue({
      activation: 'synthetic-test-activation', expiresAt: '2090-01-01T00:00:00Z',
    })
    vi.mocked(cancelScannerInvitation).mockResolvedValue(undefined)
    render(<ScannerManagementPage />)

    await screen.findByLabelText('Station', { exact: true })
    expect((screen.getByRole('button', { name: 'Create scanner' }) as HTMLButtonElement).disabled).toBe(true)
    change('Station', station.id)
    change('Device label (optional)', 'Gate 1')
    click('Create scanner')
    await screen.findByText(/Scanner created/)
    expect(registerScannerDevice).toHaveBeenCalledWith(station.id, 'Gate 1')
    expect(screen.queryByLabelText('Device code', { exact: true })).toBeNull()

    fireEvent.click(await screen.findByRole('button', { name: 'Generate activation QR' }))
    await screen.findByRole('img', { name: 'One-time scanner activation QR' })
    expect(screen.queryByText('synthetic-test-activation')).toBeNull()
    click('Revoke invitation')
    await waitFor(() => expect(screen.queryByRole('img', { name: 'One-time scanner activation QR' })).toBeNull())
    expect(cancelScannerInvitation).toHaveBeenCalledWith(scanner.id)
  })

  it('includes filters with pagination and surfaces a lifecycle error', async () => {
    vi.mocked(changeScannerState).mockRejectedValue(new ApiError(409, 'Scanner is busy'))
    render(<ScannerManagementPage />)
    await screen.findByText(scanner.deviceCode + ' · Gate 1')
    change('Search device code or label', 'SC-BD')
    change('Filter by station', station.id)
    change('Filter by status', 'PendingActivation')
    fireEvent.click(await screen.findByRole('button', { name: 'Next page' }))
    await waitFor(() => expect(listDevicesPage).toHaveBeenLastCalledWith(2, 'SC-BD', station.id, 'PendingActivation'))
    fireEvent.click(await screen.findByRole('button', { name: 'Details' }))
    click('Revoke scanner')
    expect(changeScannerState).not.toHaveBeenCalled()
    click('Confirm')
    expect(await screen.findByText('Scanner is busy')).toBeTruthy()
  })
})
