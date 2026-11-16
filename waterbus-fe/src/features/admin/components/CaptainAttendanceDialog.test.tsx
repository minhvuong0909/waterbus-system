import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getTripAttendance, recordTripAttendance } from '../services/personnelService'
import type { TripAttendance } from '../types/personnel'
import CaptainAttendanceDialog from './CaptainAttendanceDialog'

vi.mock('../services/personnelService')

const names = new Map([['cap1', 'Trần Thuyền']])
const base: TripAttendance = {
  tripId: 'trip1', boatId: 'boat1', captainId: 'cap1', assignmentVersion: 'ver-1', requiredAt: '2026-11-17T03:10:00Z',
  state: 'Missing', attendance: null,
}
const show = (data: TripAttendance, onDone = vi.fn()) => {
  vi.mocked(getTripAttendance).mockResolvedValue(data)
  render(<CaptainAttendanceDialog tripId="trip1" tripLabel="Tuyến 1 · 10:00" captainNames={names} onDone={onDone} onClose={vi.fn()} />)
  return onDone
}

afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('Captain attendance dialog', () => {
  it('records attendance with the assignment version it just read, and only with a reason', async () => {
    vi.mocked(recordTripAttendance).mockResolvedValue({} as never)
    const onDone = show(base)
    await screen.findByText('Trần Thuyền')
    expect(screen.getByText('Overdue')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Record attendance' }))
    expect(recordTripAttendance).not.toHaveBeenCalled()
    fireEvent.change(screen.getByLabelText('Reason'), { target: { value: 'Thấy tại bến' } })
    fireEvent.click(screen.getByRole('button', { name: 'Record attendance' }))
    await waitFor(() => expect(recordTripAttendance).toHaveBeenCalledWith('trip1', { captainId: 'cap1', assignmentVersion: 'ver-1', reason: 'Thấy tại bến' }))
    await waitFor(() => expect(onDone).toHaveBeenCalled())
  })

  it('shows an already recorded attendance read-only', async () => {
    show({ ...base, state: 'Late', attendance: { id: 'a', tripId: 'trip1', boatId: 'boat1', captainId: 'cap1', assignmentVersion: 'ver-1', recordedByAdminId: 'adm', recordedAt: '2026-11-17T03:30:00Z', requiredAt: base.requiredAt!, isLate: true, reason: 'Đến muộn' } })
    await screen.findByText('Đến muộn')
    expect(screen.getByText('Present (late)')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Record attendance' })).toBeNull()
  })

  it('cannot record without a configured deadline or without a Captain', async () => {
    show({ ...base, state: 'NotConfigured', requiredAt: null })
    await screen.findByText('Not configured')
    expect(screen.queryByRole('button', { name: 'Record attendance' })).toBeNull()
    cleanup()
    show({ ...base, captainId: null, assignmentVersion: null, state: 'Pending' })
    await screen.findByText(/has no Captain/)
    expect(screen.queryByRole('button', { name: 'Record attendance' })).toBeNull()
  })
})
