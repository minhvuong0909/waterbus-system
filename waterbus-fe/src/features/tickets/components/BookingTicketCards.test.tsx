import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Ticket } from '../types'
import NextDepartureCard from './NextDepartureCard'
import PastJourneyRow from './PastJourneyRow'
import QrModal from './QrModal'
import TicketBoardingPass from './TicketBoardingPass'
import UpcomingTripCard from './UpcomingTripCard'

afterEach(cleanup)

const ticket = (id: string, seat: string, passenger: string, extra: Partial<Ticket> = {}): Ticket => ({
  id, tripId: 'trip-1', tripCode: 'SWB-01', lineName: 'Tuyến 1', routeTag: 'Regular', bookingId: 'b1', bookingRef: 'BK-ABC123',
  ticketCode: `TC-${id}`, qrPayload: 'signed.booking.qr', status: 'Confirmed', departTime: '07:30', departPier: 'Bạch Đằng', departShort: 'Bạch Đằng',
  departDistrict: '', arriveTime: '08:10', arrivePier: 'Thủ Đức', arriveShort: 'Thủ Đức', arriveDistrict: '', durationMins: 40,
  dateLabel: 'Thứ Hai, 16/11', dateId: '2026-11-16', dateShort: '16/11', passenger, seat, seatNote: '', windowSeat: false, completed: false,
  boardingStatus: 'NotCheckedIn', fareVnd: 20000, vesselNote: 'Tàu 1', gate: 'Bạch Đằng', ...extra,
})
const three = [ticket('t1', 'A1', 'An'), ticket('t2', 'A2', 'Bình'), ticket('t3', 'A3', 'Chi')]
const qrs = () => screen.queryAllByRole('img', { name: 'Boarding QR' })
const inRouter = (node: React.ReactNode) => render(<MemoryRouter>{node}</MemoryRouter>)

describe('one QR per booking (BR-QR-01)', () => {
  it('shows a single QR on the next-departure card however many passengers the booking has', () => {
    inRouter(<NextDepartureCard tickets={three} onOpen={vi.fn()} />)
    expect(qrs()).toHaveLength(1)
    for (const name of ['An', 'Bình', 'Chi']) expect(screen.getByText(name)).toBeTruthy()
    expect(screen.getByText('BK-ABC123')).toBeTruthy()
    expect(screen.getByText('One QR for all 3 passengers')).toBeTruthy()
  })

  it('adds the fares of all tickets of the booking instead of showing one passenger fare', () => {
    inRouter(<NextDepartureCard tickets={three} onOpen={vi.fn()} />)
    expect(screen.getByText(/60[.,]000/)).toBeTruthy()
  })

  it('shows one QR and every passenger on the ticket detail pass and in the enlarged view', () => {
    inRouter(<TicketBoardingPass tickets={three} onEnlarge={vi.fn()} />)
    expect(qrs()).toHaveLength(1)
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    cleanup()
    inRouter(<QrModal tickets={three} onClose={vi.fn()} />)
    expect(qrs()).toHaveLength(1)
  })

  it('keeps the upcoming card free of QR codes and lists the passengers once', () => {
    inRouter(<UpcomingTripCard tickets={three} onOpen={vi.fn()} />)
    expect(qrs()).toHaveLength(0)
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
  })

  it('shows no QR when no ticket of the booking is valid any more', () => {
    inRouter(<NextDepartureCard tickets={[ticket('t1', 'A1', 'An', { qrPayload: undefined })]} onOpen={vi.fn()} />)
    expect(qrs()).toHaveLength(0)
  })

  it('summarises a finished booking as one row with one line per passenger', () => {
    const closed = [
      ticket('t1', 'A1', 'An', { completed: true, lifecycle: 'Completed', boardingStatus: 'CheckedIn', status: 'Checked in at pier' }),
      ticket('t2', 'A2', 'Bình', { completed: true, lifecycle: 'Completed', boardingStatus: 'NoShow', status: 'No-show' }),
    ]
    inRouter(<PastJourneyRow tickets={closed} onOpen={vi.fn()} />)
    expect(screen.getAllByText(/Bạch Đằng/).length).toBeGreaterThan(0)
    expect(screen.getByText('1 of 2 checked in')).toBeTruthy()
    expect(screen.getByText(/Bình/)).toBeTruthy()
  })
})
