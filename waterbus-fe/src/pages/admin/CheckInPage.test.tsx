import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { getTripStops, setCheckInWindow } from '../../features/admin/services/checkInService'
import {
  assignScanner, listScannerDevices, listScannerStaff, listStopAssignments, registerScannerDevice, revokeScannerAssignment,
} from '../../features/admin/services/scannerAssignmentService'
import type { ScannerAssignment } from '../../features/admin/services/scannerAssignmentService'
import { getOperationTrips } from '../../features/operations/services/operationsService'
import type { AdminTrip } from '../../features/operations/types'
import type { TripStopDto } from '../../features/trips/types'
import { ApiError } from '../../services/apiClient'
import CheckInPage from './CheckInPage'

vi.mock('../../features/admin/services/checkInService')
vi.mock('../../features/admin/services/scannerAssignmentService')
vi.mock('../../features/operations/services/operationsService')

const trip: AdminTrip = {
  id: 'trip-1', routeId: 'route-1', routeCode: 'R1', routeName: 'River route',
  boatId: 'boat-1', boatCode: 'B1', departureTime: '2090-01-01T02:00:00Z', arrivalTime: '2090-01-01T03:00:00Z',
  tripType: 'Commuter', status: 'Scheduled', allowedOperations: [],
}
const stop: TripStopDto = {
  callId: 'call-1', stationId: 'station-1', stationName: 'River station', visitOrder: 1,
  canBoard: true, canAlight: false, status: 'Pending', estimatedDepartureTime: trip.departureTime,
  checkInOpenAt: '2090-01-01T01:00:00Z', checkInCloseAt: '2090-01-01T01:50:00Z',
}
const scanner = { id: 'device-1', deviceCode: 'SC-01', label: 'Gate 1', isActive: true }
const staff = { id: 'staff-1', fullName: 'Nguyen An', staffCode: 'ST01', email: null }
const assignment: ScannerAssignment = {
  assignmentId: 'assignment-1', staffAccountId: staff.id, staffName: staff.fullName, deviceCode: scanner.deviceCode,
  activeFrom: stop.checkInOpenAt!, activeUntil: stop.checkInCloseAt!, isPrimaryOffline: true,
}
const page = (items: ScannerAssignment[]) => ({ items, pageNumber: 1, totalCount: items.length, totalPages: 1 })
const change = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } })
const click = (name: string) => fireEvent.click(screen.getByRole('button', { name }))

beforeEach(() => {
  vi.mocked(getOperationTrips).mockResolvedValue([trip, { ...trip, id: 'trip-2', routeName: 'Second route' }])
  vi.mocked(getTripStops).mockResolvedValue([stop, {
    ...stop, callId: 'terminal', stationName: 'Terminal', visitOrder: 2, canBoard: false,
  }])
  vi.mocked(listScannerStaff).mockResolvedValue([staff])
  vi.mocked(listScannerDevices).mockResolvedValue([scanner, { ...scanner, id: 'inactive', deviceCode: 'OLD', isActive: false }])
  vi.mocked(listStopAssignments).mockResolvedValue(page([]))
})

afterEach(() => {
  cleanup()
  vi.resetAllMocks()
})

async function chooseStop() {
  render(<CheckInPage />)
  await screen.findByLabelText('Trip')
  change('Trip', trip.id)
  const boarding = await screen.findByLabelText('Boarding stop')
  expect(within(boarding).queryByRole('option', { name: /Terminal/ })).toBeNull()
  change('Boarding stop', stop.callId)
  await screen.findByText('No current or upcoming assignments at this stop.')
}

async function selectAssignmentOptions() {
  await screen.findByRole('option', { name: /Nguyen An/ })
  expect(screen.queryByRole('option', { name: /OLD/ })).toBeNull()
  change('Staff account', staff.id)
  change('Scanner device', scanner.id)
}

describe('Admin check-in scanner assignments', () => {
  it('keeps window controls and serializes a confirmed assignment in UTC with one POST', async () => {
    vi.mocked(setCheckInWindow).mockResolvedValue({
      tripStopCallId: stop.callId, checkInOpenAt: '2090-01-01T01:10:00.000Z', checkInCloseAt: '2090-01-01T01:50:00.000Z',
    })
    let resolveCreate!: (value: Awaited<ReturnType<typeof assignScanner>>) => void
    vi.mocked(assignScanner).mockImplementation(() => new Promise<Awaited<ReturnType<typeof assignScanner>>>((resolve) => { resolveCreate = resolve }))
    await chooseStop()
    change('Opens', '2090-01-01T08:10')
    click('Save')
    await screen.findByText('Check-in window saved.')
    expect(setCheckInWindow).toHaveBeenCalledWith(stop.callId, '2090-01-01T01:10:00.000Z', '2090-01-01T01:50:00.000Z')
    await selectAssignmentOptions()
    expect((screen.getByLabelText('Active from') as HTMLInputElement).value).toBe('2090-01-01T08:10')
    fireEvent.click(screen.getByLabelText('Primary Offline scanner'))
    click('Review assignment')
    expect(screen.getByText(/River route.*Nguyen An.*SC-01/)).toBeTruthy()
    const button = screen.getByRole('button', { name: 'Confirm assignment' })
    fireEvent.click(button)
    fireEvent.click(button)
    const body = {
      tripStopCallId: stop.callId, staffAccountId: staff.id, deviceId: scanner.id, isPrimaryOffline: true,
      activeFrom: '2090-01-01T01:10:00.000Z', activeUntil: '2090-01-01T01:50:00.000Z',
    }
    expect(assignScanner).toHaveBeenCalledTimes(1)
    expect(assignScanner).toHaveBeenCalledWith(body)
    vi.mocked(listStopAssignments).mockResolvedValue(page([assignment]))
    await act(async () => resolveCreate({ assignmentId: assignment.assignmentId, ...body }))
    expect(await screen.findByText(/Created assignment assignment-1/)).toBeTruthy()
    expect(await screen.findByText(assignment.assignmentId)).toBeTruthy()
  })

  it('validates device codes, surfaces duplicate errors, and refreshes active devices after registration', async () => {
    vi.mocked(listScannerDevices).mockResolvedValue([])
    vi.mocked(registerScannerDevice)
      .mockRejectedValueOnce(new ApiError(400, 'Device code already exists'))
      .mockResolvedValueOnce(scanner.id)
    await chooseStop()
    click('Register a scanner')
    change('Scanner code', 'bad code!')
    click('Register a scanner')
    expect(registerScannerDevice).not.toHaveBeenCalled()
    change('Scanner code', scanner.deviceCode)
    change('Device label', 'Gate 1')
    click('Register a scanner')
    await screen.findByText('Device code already exists')
    vi.mocked(listScannerDevices).mockResolvedValue([scanner])
    click('Register a scanner')
    await screen.findByRole('option', { name: /SC-01/ })
    expect(registerScannerDevice).toHaveBeenLastCalledWith('SC-01', 'Gate 1')
    expect((screen.getByLabelText('Scanner device') as HTMLSelectElement).value).toBe(scanner.id)
  })

  it('rejects an invalid end time and refreshes the list after an uncertain create without retrying it', async () => {
    vi.mocked(assignScanner).mockRejectedValue(new ApiError(0, 'Offline'))
    await chooseStop()
    await selectAssignmentOptions()
    change('Active until (optional)', '2090-01-01T07:00')
    click('Review assignment')
    expect(screen.getByText(/Enter a start time/)).toBeTruthy()
    expect(assignScanner).not.toHaveBeenCalled()
    change('Active until (optional)', '')
    click('Review assignment')
    click('Confirm assignment')
    expect(await screen.findByText(/server may have saved/)).toBeTruthy()
    await waitFor(() => expect(listStopAssignments).toHaveBeenCalledTimes(2))
    expect(assignScanner).toHaveBeenCalledTimes(1)
    expect(assignScanner).toHaveBeenCalledWith(expect.objectContaining({ activeUntil: null }))
  })

  it('shows API conflicts and revokes only the confirmed assignment before refreshing', async () => {
    vi.mocked(listStopAssignments).mockResolvedValueOnce(page([assignment]))
    vi.mocked(assignScanner).mockRejectedValue(new ApiError(409, 'Primary already assigned'))
    vi.mocked(revokeScannerAssignment).mockResolvedValue({
      assignmentId: assignment.assignmentId, activeUntil: assignment.activeFrom,
    })
    render(<CheckInPage />)
    await screen.findByLabelText('Trip')
    change('Trip', trip.id)
    await screen.findByLabelText('Boarding stop')
    change('Boarding stop', stop.callId)
    await screen.findByText(assignment.assignmentId)
    await selectAssignmentOptions()
    click('Review assignment')
    click('Confirm assignment')
    expect(await screen.findByText('Primary already assigned')).toBeTruthy()
    click('Revoke assignment')
    expect(screen.getByText('Revoke assignment assignment-1 for scanner SC-01?')).toBeTruthy()
    expect(revokeScannerAssignment).not.toHaveBeenCalled()
    click('Confirm revoke')
    expect(revokeScannerAssignment).toHaveBeenCalledWith(assignment.assignmentId)
    expect(await screen.findByText('No current or upcoming assignments at this stop.')).toBeTruthy()
  })

  it('removes old stop actions as soon as the trip changes', async () => {
    vi.mocked(listStopAssignments).mockResolvedValueOnce(page([assignment]))
    render(<CheckInPage />)
    await screen.findByLabelText('Trip')
    change('Trip', trip.id)
    await screen.findByLabelText('Boarding stop')
    change('Boarding stop', stop.callId)
    await screen.findByText(assignment.assignmentId)
    click('Revoke assignment')
    let resolveStops!: (stops: TripStopDto[]) => void
    vi.mocked(getTripStops).mockImplementationOnce(() => new Promise<TripStopDto[]>((resolve) => { resolveStops = resolve }))
    change('Trip', 'trip-2')
    expect(screen.queryByLabelText('Boarding stop')).toBeNull()
    expect(screen.queryByRole('button', { name: 'Confirm revoke' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Save' })).toBeNull()
    await act(async () => resolveStops([{ ...stop, callId: 'call-2', stationName: 'Other station' }]))
    expect(screen.getByRole('option', { name: /Other station/ })).toBeTruthy()
    expect(revokeScannerAssignment).not.toHaveBeenCalled()
  })
})
