import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import CaptainPortalPage from './CaptainPortalPage'
import {
  getOperationTrips,
  operateTrip,
} from '../../features/operations/services/operationsService'
import {
  getCaptainAttendance,
  getCaptainBoat,
} from '../../features/captain/services/captainService'
import { getTrackedTrip } from '../../features/tracking/services/trackingService'
import type { AdminTrip } from '../../features/operations/types'

const state = vi.hoisted(() => ({ connected: true, fresh: true }))
vi.mock('../../features/authentication/hooks/useAuth', () => ({
  useAuth: () => ({ user: { fullName: 'Nguyễn Minh' }, signOut: vi.fn() }),
}))
vi.mock('../../features/operations/services/operationsService')
vi.mock('../../features/captain/services/captainService')
vi.mock('../../features/tracking/services/trackingService')
vi.mock('../../features/captain/hooks/useCaptainGps', () => ({
  useCaptainGps: () => ({
    ...state,
    sharing: true,
    toggleSharing: vi.fn(),
    status: 'live',
    error: null,
    position: {
      latitude: 10.8,
      longitude: 106.7,
      speedKnots: 12.5,
      observedAt: '2026-11-16T01:45:02Z',
    },
  }),
}))
vi.mock('../../features/captain/components/OnboardPlayerPanel', () => ({ default: () => <div>Loa trên tàu</div> }))
vi.mock('../../features/captain/components/CaptainMap', () => ({
  default: ({ fresh }: { fresh: boolean }) => (
    <div>{fresh ? 'Bản đồ trực tiếp' : 'Bản đồ vị trí cuối'}</div>
  ),
}))

const trip: AdminTrip = {
  id: 'trip-1',
  boatId: 'boat-1',
  boatCode: 'WB-01',
  routeId: 'route-1',
  routeCode: 'RT01',
  routeName: 'Bạch Đằng → Thanh Đa',
  departureTime: '2026-11-16T01:30:00Z',
  arrivalTime: '2026-11-16T02:10:00Z',
  tripType: 'Commuter',
  status: 'Scheduled',
  allowedOperations: ['OpenBoarding'],
}
const show = () =>
  render(
    <MemoryRouter>
      <CaptainPortalPage />
    </MemoryRouter>,
  )
const button = (name: string) => screen.getByRole('button', { name }) as HTMLButtonElement

beforeEach(() => {
  state.connected = true
  state.fresh = true
  vi.mocked(getOperationTrips).mockResolvedValue([trip])
  vi.mocked(getCaptainAttendance).mockResolvedValue({
    tripId: trip.id,
    boatId: trip.boatId,
    captainId: 'captain-1',
    state: 'Present',
    requiredAt: '2026-11-16T00:00:00Z',
    attendance: { recordedAt: '2026-11-15T23:40:00Z', isLate: false },
  })
  vi.mocked(getCaptainBoat).mockResolvedValue({ id: trip.boatId, totalSeats: 75 } as Awaited<
    ReturnType<typeof getCaptainBoat>
  >)
  vi.mocked(getTrackedTrip).mockResolvedValue({
    tripId: trip.id,
    routeName: trip.routeName,
    status: 'Scheduled',
    boat: null,
    departureTime: trip.departureTime,
    arrivalTime: trip.arrivalTime,
    stops: [
      {
        callId: 'c1',
        stationName: 'Bạch Đằng',
        visitOrder: 1,
        latitude: 10.7,
        longitude: 106.7,
        status: 'Planned',
        estimatedArrivalTime: null,
        estimatedDepartureTime: trip.departureTime,
      },
      {
        callId: 'c2',
        stationName: 'Thanh Đa',
        visitOrder: 2,
        latitude: 10.8,
        longitude: 106.8,
        status: 'Planned',
        estimatedArrivalTime: trip.arrivalTime,
        estimatedDepartureTime: null,
      },
    ],
  })
})
afterEach(() => {
  cleanup()
  vi.resetAllMocks()
})

describe('Captain operations dashboard', () => {
  it('uses the project logo, shows attendance and requires confirmation before changing a trip', async () => {
    vi.mocked(operateTrip).mockResolvedValue({ status: 'Boarding' })
    show()
    await screen.findByText('Xin chào, Captain Nguyễn Minh')
    await screen.findAllByText('Admin đã xác nhận có mặt lúc 06:40')
    expect(screen.getByRole('img', { name: 'Smart Waterbus' })).toBeTruthy()
    expect(button('Mở đón khách').disabled).toBe(false)
    expect(button('Khởi hành').disabled).toBe(true)
    expect(button('Đến bến cuối').disabled).toBe(true)
    fireEvent.click(button('Mở đón khách'))
    expect(operateTrip).not.toHaveBeenCalled()
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận' }))
    await screen.findByText('Mở đón khách')
    expect(operateTrip).toHaveBeenCalledWith(
      'trip-1',
      'OpenBoarding',
      'Thuyền trưởng mở đón khách.',
    )
  })

  it('locks trip actions and SOS offline, retaining the last position', async () => {
    state.connected = false
    state.fresh = false
    vi.mocked(getOperationTrips).mockResolvedValue([
      { ...trip, status: 'Suspended', allowedOperations: [] },
    ])
    show()
    await screen.findByText('Bản đồ vị trí cuối')
    expect(button('Gửi SOS').disabled).toBe(true)
    expect(button('Mở đón khách').disabled).toBe(true)
    expect(button('Khởi hành').disabled).toBe(true)
    expect(button('Đến bến cuối').disabled).toBe(true)
    expect(
      screen.getByText('Chuyến tạm ngưng theo quyết định Admin. Chờ hướng dẫn tiếp tục.'),
    ).toBeTruthy()
    expect(screen.getByText('Đang hiển thị vị trí cuối lúc 08:45:02')).toBeTruthy()
  })

  it('allows Arrive only when supplied by the backend, even while the trip is en route', async () => {
    vi.mocked(getOperationTrips).mockResolvedValue([
      { ...trip, status: 'EnRoute', allowedOperations: [] },
    ])
    show()
    await screen.findByText('Bản đồ trực tiếp')
    expect(button('Đến bến cuối').disabled).toBe(true)
    expect(screen.queryByText('42')).toBeNull()
  })

  it('keeps SOS failure visible and never reports success when the server rejects it', async () => {
    vi.mocked(operateTrip).mockRejectedValue(new Error('unreachable'))
    show()
    await screen.findByText('Bản đồ trực tiếp')
    fireEvent.click(button('Gửi SOS'))
    const dialog = screen.getByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText('Tình huống đang xảy ra (không bắt buộc)'), {
      target: { value: 'Sự cố kỹ thuật' },
    })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Gửi SOS' }))
    await screen.findAllByText(/Something went wrong|Có lỗi xảy ra/)
    expect(screen.queryByText(/Đã gửi SOS lúc/)).toBeNull()
    expect(operateTrip).toHaveBeenCalledWith(trip.id, 'SOS', 'Sự cố kỹ thuật')
  })
})
