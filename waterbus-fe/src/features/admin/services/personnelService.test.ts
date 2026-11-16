import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  changeCaptainStatus, changeStaffAccountStatus, createStaffAccount, listCaptains, listStaffAccounts, resendCaptainInvitation,
  updateCaptain,
} from './personnelService'

const fetchMock = vi.fn()
const noContent = () => new Response(null, { status: 204 })
const page = () => new Response(JSON.stringify({ items: [], pageNumber: 1, totalPages: 1, totalCount: 0 }))
const urlOf = (call: number) => new URL(String(fetchMock.mock.calls[call][0]), 'http://localhost')
const bodyOf = (call: number) => JSON.parse(fetchMock.mock.calls[call][1].body)

afterEach(() => { fetchMock.mockReset(); vi.unstubAllGlobals() })

describe('personnelService', () => {
  it('lists captains with the backend paging and numeric status filter, dropping empty filters', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockImplementation(async () => page())
    await listCaptains(2, '  an ', 2)
    expect(urlOf(0).pathname).toBe('/api/v1/captains')
    expect(Object.fromEntries(urlOf(0).searchParams)).toEqual({ page: '2', pageSize: '20', search: 'an', status: '2' })
    await listCaptains(1, '', '')
    expect([...urlOf(1).searchParams.keys()].sort()).toEqual(['page', 'pageSize'])
  })

  it('sends the captain status as the BE enum number together with the trimmed reason', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockImplementation(async () => noContent())
    await changeCaptainStatus('c1', 3, '  nghỉ việc ')
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(urlOf(0).pathname).toBe('/api/v1/captains/c1/status')
    expect(bodyOf(0)).toEqual({ status: 3, reason: 'nghỉ việc' })
  })

  it('puts the reason beside the captain profile on update and on resend', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockImplementation(async () => noContent())
    const draft = { fullName: 'A', email: 'a@x.vn', phone: '0901234567', licenseNumber: 'L1', licenseExpiresOn: '2030-01-01' }
    await updateCaptain('c1', draft, ' đổi số ')
    expect(bodyOf(0)).toEqual({ ...draft, reason: 'đổi số' })
    await resendCaptainInvitation('c1', 'gửi lại')
    expect(urlOf(1).pathname).toBe('/api/v1/captains/c1/invitation')
    expect(fetchMock.mock.calls[1][1].method).toBe('POST')
  })

  it('creates staff with a null code when none is typed and never sends a password', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockResolvedValue(new Response(JSON.stringify('new-id')))
    await createStaffAccount({ fullName: 'B', email: 'b@x.vn', phone: '0901234567', staffCode: '  ' })
    expect(urlOf(0).pathname).toBe('/api/v1/staff-accounts')
    expect(bodyOf(0)).toEqual({ fullName: 'B', email: 'b@x.vn', phone: '0901234567', staffCode: null })
  })

  it('filters staff by the active flag only when one is chosen', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockImplementation(async () => page())
    await listStaffAccounts(1, '', false)
    expect(urlOf(0).searchParams.get('isActive')).toBe('false')
    await listStaffAccounts(1, '', '')
    expect(urlOf(1).searchParams.has('isActive')).toBe(false)
  })

  it('switches staff login with PUT status', async () => {
    vi.stubGlobal('fetch', fetchMock)
    fetchMock.mockImplementation(async () => noContent())
    await changeStaffAccountStatus('s1', false)
    expect(fetchMock.mock.calls[0][1].method).toBe('PUT')
    expect(urlOf(0).pathname).toBe('/api/v1/staff-accounts/s1/status')
    expect(bodyOf(0)).toEqual({ isActive: false })
  })
})
