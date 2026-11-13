import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import { getOperationTrips } from '../../operations/services/operationsService'
import { listRefundCandidates, requestRefundsForTrip } from '../services/refundService'
import type { RefundCandidate } from '../types'
import type { AdminTrip } from '../../operations/types'
import RefundTripPanel from './RefundTripPanel'

vi.mock('../../operations/services/operationsService')
vi.mock('../services/refundService')

const trip = (id: string, status: AdminTrip['status'], departureTime: string): AdminTrip => ({
  id, routeId: 'rt', routeCode: 'RT01', routeName: 'Bạch Đằng – Linh Đông', boatId: 'b', boatCode: 'SWB-01',
  departureTime, arrivalTime: departureTime, tripType: 'Commuter', status, allowedOperations: [],
})

const candidate = (id: string, code: string, amount: number): RefundCandidate => ({
  bookingId: id, bookingCode: code, orderId: 'o-' + id, purchaserName: 'Nguyễn An', purchaserEmail: `${code}@example.com`,
  purchaserPhone: null, amount, currency: 'VND', ticketCodes: ['T-' + code], reason: 'Chuyến bị hủy trước khi phục vụ đoạn đi này.',
})

const candidates = (items: RefundCandidate[]) => ({ items, totalCount: items.length, pageNumber: 1, totalPages: 1 })

beforeEach(() => {
  vi.mocked(getOperationTrips).mockResolvedValue([
    trip('t-ok', 'Scheduled', '2026-11-13T01:00:00Z'),
    trip('t-cancelled', 'Cancelled', '2026-11-12T01:00:00Z'),
    trip('t-terminated', 'Terminated', '2026-11-11T01:00:00Z'),
    trip('t-done', 'Completed', '2026-11-10T01:00:00Z'),
  ])
  vi.mocked(listRefundCandidates).mockResolvedValue(candidates([candidate('b1', 'BK1', 40000), candidate('b2', 'BK2', 60000)]))
})

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  setActiveLocale('en')
})

const select = () => screen.getByLabelText('Trip') as HTMLSelectElement

describe('RefundTripPanel', () => {
  it('offers only cancelled or terminated trips, newest first', async () => {
    render(<RefundTripPanel onCreated={() => {}} />)

    await waitFor(() => expect(select().disabled).toBe(false))
    const values = Array.from(select().options).map((o) => o.value)
    expect(values).toEqual(['', 't-cancelled', 't-terminated'])
  })

  it('searches a month of cancelled or terminated trips, not just the operations window', async () => {
    render(<RefundTripPanel onCreated={() => {}} />)
    await waitFor(() => expect(select().disabled).toBe(false))

    const [query] = vi.mocked(getOperationTrips).mock.calls[0]
    expect(query?.statuses).toEqual(['Cancelled', 'Terminated'])
    const days = (new Date(query!.to!).getTime() - new Date(query!.from!).getTime()) / 86_400_000
    expect(days).toBeLessThanOrEqual(31)                                  // BE từ chối khoảng dài hơn 31 ngày
    expect(new Date(query!.from!).getTime()).toBeLessThan(Date.now() - 7 * 86_400_000)   // thấy cả chuyến huỷ từ tuần trước
  })

  it('says so when no trip needs refunds', async () => {
    vi.mocked(getOperationTrips).mockResolvedValue([trip('t-ok', 'Scheduled', '2026-11-13T01:00:00Z')])
    render(<RefundTripPanel onCreated={() => {}} />)

    expect(await screen.findByText('No cancelled or terminated trips.')).toBeTruthy()
  })

  it('shows the bookings to refund with their total once a trip is chosen', async () => {
    render(<RefundTripPanel onCreated={() => {}} />)
    await waitFor(() => expect(select().disabled).toBe(false))

    fireEvent.change(select(), { target: { value: 't-cancelled' } })

    expect(await screen.findByText('BK1')).toBeTruthy()
    expect(screen.getByText('BK2')).toBeTruthy()
    expect(listRefundCandidates).toHaveBeenCalledWith('t-cancelled')
    expect(screen.getByText(/2 bookings/)).toBeTruthy()
  })

  it('asks for confirmation before creating, then reports what was created and skipped', async () => {
    const onCreated = vi.fn()
    vi.mocked(requestRefundsForTrip).mockResolvedValue({
      tripId: 't-cancelled', requested: 1, skipped: 1, created: [],
      skippedItems: [{ bookingId: 'b2', bookingCode: 'BK2', reason: 'Chưa có giao dịch thanh toán thành công.' }],
    })
    render(<RefundTripPanel onCreated={onCreated} />)
    await waitFor(() => expect(select().disabled).toBe(false))
    fireEvent.change(select(), { target: { value: 't-cancelled' } })
    await screen.findByText('BK1')

    fireEvent.click(screen.getByRole('button', { name: 'Create refund requests' }))
    expect(requestRefundsForTrip).not.toHaveBeenCalled()          // mới chỉ hỏi xác nhận
    expect(screen.getByText('Tickets and bookings are cancelled as soon as the requests are created.')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Yes, create the refund requests' }))

    await waitFor(() => expect(requestRefundsForTrip).toHaveBeenCalledWith('t-cancelled'))
    expect(await screen.findByText('Created 1 refund requests, skipped 1.')).toBeTruthy()
    expect(screen.getByText('Chưa có giao dịch thanh toán thành công.')).toBeTruthy()
    expect(onCreated).toHaveBeenCalledTimes(1)
  })

  it('can back out of the confirmation without creating anything', async () => {
    render(<RefundTripPanel onCreated={() => {}} />)
    await waitFor(() => expect(select().disabled).toBe(false))
    fireEvent.change(select(), { target: { value: 't-terminated' } })
    await screen.findByText('BK1')

    fireEvent.click(screen.getByRole('button', { name: 'Create refund requests' }))
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(requestRefundsForTrip).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Create refund requests' })).toBeTruthy()
  })

  it('offers nothing to create when no booking is waiting', async () => {
    vi.mocked(listRefundCandidates).mockResolvedValue(candidates([]))
    render(<RefundTripPanel onCreated={() => {}} />)
    await waitFor(() => expect(select().disabled).toBe(false))

    fireEvent.change(select(), { target: { value: 't-cancelled' } })

    expect(await screen.findByText('No bookings are waiting for a refund.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create refund requests' })).toBeNull()
  })

  it('shows the server message when creating fails, and does not report success', async () => {
    const onCreated = vi.fn()
    vi.mocked(requestRefundsForTrip).mockRejectedValue(new Error('down'))
    render(<RefundTripPanel onCreated={onCreated} />)
    await waitFor(() => expect(select().disabled).toBe(false))
    fireEvent.change(select(), { target: { value: 't-cancelled' } })
    await screen.findByText('BK1')

    fireEvent.click(screen.getByRole('button', { name: 'Create refund requests' }))
    fireEvent.click(screen.getByRole('button', { name: 'Yes, create the refund requests' }))

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(onCreated).not.toHaveBeenCalled()
  })

  it('shows an error when the trips cannot be loaded', async () => {
    vi.mocked(getOperationTrips).mockRejectedValue(new Error('down'))
    render(<RefundTripPanel onCreated={() => {}} />)

    expect(await screen.findByRole('alert')).toBeTruthy()
  })
})
