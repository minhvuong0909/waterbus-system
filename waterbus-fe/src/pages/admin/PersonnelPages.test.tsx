import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
  assignBoatCaptain, changeCaptainStatus, changeStaffAccountStatus, createCaptain, createStaffAccount, listAllCaptains, listCaptainAlerts,
  listCaptainAudit, listCaptains, listStaffAccounts, resolveCaptainAlert,
} from '../../features/admin/services/personnelService'
import { getBoats } from '../../features/operations/services/fleetService'
import type { AdminBoat } from '../../features/operations/types'
import type { Captain, StaffAccount } from '../../features/admin/types/personnel'
import CaptainsPage from './CaptainsPage'
import StaffAccountsPage from './StaffAccountsPage'

vi.mock('../../features/admin/services/personnelService')
vi.mock('../../features/operations/services/fleetService')

const captain: Captain = {
  id: 'c1', fullName: 'Trần Thuyền', email: 'cap@waterbus.vn', phone: '0901234567', licenseNumber: 'L-1',
  licenseExpiresOn: '2030-01-01', status: 1, boatId: null, invitationPending: false, emailConfirmed: true,
}
const pendingCaptain: Captain = { ...captain, id: 'c2', fullName: 'Chờ Kích', status: 3, invitationPending: true, emailConfirmed: false }
const boat = (id: string, code: string, captainUserId: string | null = null): AdminBoat => ({
  id, code, name: `Tàu ${code}`, registrationNumber: 'R', totalSeats: 40, manufacturer: '', yearBuilt: 2025, isActive: true, captainUserId,
})
const staff: StaffAccount = {
  id: 's1', fullName: 'Lê Soát', email: 'staff@waterbus.vn', phone: '0907654321', staffCode: 'ST01',
  isActive: true, invitationPending: false, emailConfirmed: true,
}
const pendingStaff: StaffAccount = { ...staff, id: 's2', fullName: 'Mới Mời', isActive: false, invitationPending: true, emailConfirmed: false }
const paged = <T,>(items: T[]) => ({ items, pageNumber: 1, totalPages: 1, totalCount: items.length })

const click = (name: string | RegExp) => fireEvent.click(screen.getByRole('button', { name }))
const change = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } })
const rowOf = (name: string) => screen.getByText(name).closest('tr') as HTMLElement

beforeEach(() => {
  vi.mocked(listCaptains).mockResolvedValue(paged([captain, pendingCaptain]))
  vi.mocked(listAllCaptains).mockResolvedValue([captain, pendingCaptain])
  vi.mocked(getBoats).mockResolvedValue([boat('boat1', 'SWB-01'), boat('boat2', 'SWB-02')])
  vi.mocked(listStaffAccounts).mockResolvedValue(paged([staff, pendingStaff]))
})
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('Captains page', () => {
  it('flags a captain who has not accepted the invitation and offers to resend it only for them', async () => {
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    expect(within(rowOf('Chờ Kích')).getByText('Awaiting activation')).toBeTruthy()
    expect(within(rowOf('Chờ Kích')).getByRole('button', { name: /Resend invitation/ })).toBeTruthy()
    expect(within(rowOf('Trần Thuyền')).queryByRole('button', { name: /Resend invitation/ })).toBeNull()
  })

  it('validates the form locally and creates the captain without any password field', async () => {
    vi.mocked(createCaptain).mockResolvedValue('new-id')
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    click('Add captain')
    expect(screen.queryByLabelText(/password/i)).toBeNull()
    click('Create captain')
    expect(createCaptain).not.toHaveBeenCalled()
    expect(screen.getByText('Name is required.')).toBeTruthy()
    change('Full name', 'Nguyễn Mới')
    change('Email', 'moi@waterbus.vn')
    change('Phone', '0912345678')
    change('License number', 'L-9')
    change('License expires on', '2031-05-06')
    click('Create captain')
    await waitFor(() => expect(createCaptain).toHaveBeenCalledWith({
      fullName: 'Nguyễn Mới', email: 'moi@waterbus.vn', phone: '0912345678', licenseNumber: 'L-9', licenseExpiresOn: '2031-05-06',
    }))
    await waitFor(() => expect(listCaptains).toHaveBeenCalledTimes(2)) // danh sách tải lại sau khi lưu
  })

  it('requires a reason before changing the status, then sends the numeric status', async () => {
    vi.mocked(changeCaptainStatus).mockResolvedValue(undefined)
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    fireEvent.click(within(rowOf('Trần Thuyền')).getByRole('button', { name: /Change status/ }))
    fireEvent.change(screen.getByLabelText('New status'), { target: { value: '2' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'Change status' }).at(-1)!)
    await screen.findByText('Enter a reason.')
    expect(changeCaptainStatus).not.toHaveBeenCalled()
    change('Reason', 'Vi phạm quy trình')
    fireEvent.click(screen.getAllByRole('button', { name: 'Change status' }).at(-1)!)
    await waitFor(() => expect(changeCaptainStatus).toHaveBeenCalledWith('c1', 2, 'Vi phạm quy trình'))
  })
})

describe('Captain boat assignment, alerts and activity log', () => {
  it('assigns a captain to a boat only with a reason', async () => {
    vi.mocked(assignBoatCaptain).mockResolvedValue(undefined)
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    fireEvent.click(within(rowOf('Trần Thuyền')).getByRole('button', { name: /Assign boat/ }))
    change('Boat', 'boat2')
    fireEvent.click(screen.getByRole('button', { name: 'Assign to boat' }))
    await screen.findByText('Enter a reason.')
    expect(assignBoatCaptain).not.toHaveBeenCalled()
    change('Reason', 'Phân công tàu mới')
    fireEvent.click(screen.getByRole('button', { name: 'Assign to boat' }))
    await waitFor(() => expect(assignBoatCaptain).toHaveBeenCalledWith('boat2', 'c1', 'Phân công tàu mới'))
  })

  it('shows the boat of a captain and removes them from it by sending a null captain', async () => {
    vi.mocked(listCaptains).mockResolvedValue(paged([{ ...captain, boatId: 'boat1' }]))
    vi.mocked(getBoats).mockResolvedValue([boat('boat1', 'SWB-01', 'c1'), boat('boat2', 'SWB-02')])
    vi.mocked(assignBoatCaptain).mockResolvedValue(undefined)
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    expect(within(rowOf('Trần Thuyền')).getByText('SWB-01')).toBeTruthy()
    fireEvent.click(within(rowOf('Trần Thuyền')).getByRole('button', { name: /Change boat/ }))
    change('Boat', '')
    change('Reason', 'Nghỉ phép')
    fireEvent.click(screen.getByRole('button', { name: 'Remove from boat' }))
    await waitFor(() => expect(assignBoatCaptain).toHaveBeenCalledWith('boat1', null, 'Nghỉ phép'))
  })

  it('names the captain already driving a boat in the choice list', async () => {
    vi.mocked(getBoats).mockResolvedValue([boat('boat1', 'SWB-01', 'c2'), boat('boat2', 'SWB-02')])
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    fireEvent.click(within(rowOf('Trần Thuyền')).getByRole('button', { name: /Assign boat/ }))
    expect(screen.getByRole('option', { name: /SWB-01.*Chờ Kích/ })).toBeTruthy()
  })

  it('lists open alerts and resolves one after confirmation', async () => {
    vi.mocked(listCaptainAlerts).mockResolvedValue(paged([
      { id: 'a1', tripId: 't1', boatId: 'boat1', captainId: 'c1', type: 'CaptainMissing', message: 'Chưa có mặt', occurredAt: '2026-11-16T01:00:00Z', resolvedAt: null, resolvedByAdminId: null },
      { id: 'a2', tripId: 't2', boatId: 'boat1', captainId: 'c1', type: 'CaptainMissing', message: 'Đã xong', occurredAt: '2026-11-15T01:00:00Z', resolvedAt: '2026-11-15T02:00:00Z', resolvedByAdminId: 'x' },
    ]))
    vi.mocked(resolveCaptainAlert).mockResolvedValue(undefined)
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    fireEvent.click(screen.getByRole('tab', { name: 'Alerts' }))
    await screen.findByText('Chưa có mặt')
    expect(screen.getAllByRole('button', { name: /Mark resolved/ })).toHaveLength(1)
    fireEvent.click(screen.getByRole('button', { name: /Mark resolved/ }))
    fireEvent.click(screen.getAllByRole('button', { name: 'Mark resolved' }).at(-1)!)
    await waitFor(() => expect(resolveCaptainAlert).toHaveBeenCalledWith('a1'))
  })

  it('shows who did what in the activity log, telling the captain apart from the admin', async () => {
    vi.mocked(listCaptainAudit).mockResolvedValue(paged([
      { id: 'e1', captainId: 'c1', boatId: null, tripId: null, actedByCaptainId: 'c1', actedByAdminId: null, occurredAt: '2026-11-16T01:00:00Z', action: 'InvitationAccepted', oldValues: null, newValues: '{}', reason: 'Email owner activated the account' },
      { id: 'e2', captainId: 'c1', boatId: null, tripId: null, actedByCaptainId: null, actedByAdminId: 'adm', occurredAt: '2026-11-16T00:00:00Z', action: 'Created', oldValues: null, newValues: '{}', reason: 'Admin tạo tài khoản' },
    ]))
    render(<CaptainsPage />)
    await screen.findByText('Trần Thuyền')
    fireEvent.click(screen.getByRole('tab', { name: 'Activity log' }))
    await screen.findByText('Admin tạo tài khoản')
    expect(screen.getByText('The Captain')).toBeTruthy()
    expect(screen.getAllByText('Admin').length).toBeGreaterThan(0)
  })
})

describe('Staff accounts page', () => {
  it('creates staff with an optional code and reloads the list', async () => {
    vi.mocked(createStaffAccount).mockResolvedValue('new-id')
    render(<StaffAccountsPage />)
    await screen.findByText('Lê Soát')
    click('Add staff')
    change('Full name', 'Phạm Mới')
    change('Email', 'pm@waterbus.vn')
    change('Phone', '0912345678')
    click('Create staff')
    await waitFor(() => expect(createStaffAccount).toHaveBeenCalledWith({
      fullName: 'Phạm Mới', email: 'pm@waterbus.vn', phone: '0912345678', staffCode: '',
    }))
    await waitFor(() => expect(listStaffAccounts).toHaveBeenCalledTimes(2))
  })

  it('does not offer the login switch for an account still waiting for activation', async () => {
    render(<StaffAccountsPage />)
    await screen.findByText('Lê Soát')
    expect(within(rowOf('Mới Mời')).queryByRole('button', { name: /login/ })).toBeNull()
    expect(within(rowOf('Mới Mời')).getByRole('button', { name: /Resend invitation/ })).toBeTruthy()
    expect(within(rowOf('Lê Soát')).getByRole('button', { name: /Turn off login/ })).toBeTruthy()
  })

  it('turns login off only after confirmation', async () => {
    vi.mocked(changeStaffAccountStatus).mockResolvedValue(undefined)
    render(<StaffAccountsPage />)
    await screen.findByText('Lê Soát')
    fireEvent.click(within(rowOf('Lê Soát')).getByRole('button', { name: /Turn off login/ }))
    expect(changeStaffAccountStatus).not.toHaveBeenCalled()
    fireEvent.click(screen.getAllByRole('button', { name: 'Turn off login' }).at(-1)!)
    await waitFor(() => expect(changeStaffAccountStatus).toHaveBeenCalledWith('s1', false))
  })
})
