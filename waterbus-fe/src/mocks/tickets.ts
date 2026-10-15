import { seatFeeFor, seatPosition, seatZone } from '../features/booking/utils'
import type { ManageBooking, Ticket, TicketWallet } from '../features/tickets/types'
import { describeDate } from '../features/trips/utils'
import { t } from '../i18n'
import { toIsoDate } from '../utils/time'
import { PIER_OPTIONS } from './tripSearch'
import { SEAT_MAP } from './booking'

// Sample tickets for a traveler who has not booked anything yet. They follow what the backend
// issues: boat SWB-01 on route RT01 (Bạch Đằng - Linh Đông, 45 minutes), seats and fares of the
// boat layout, and booking / ticket codes in the backend's format. Dates are relative to today, so
// they are built when asked for (in the active language) rather than once at load time.

const ORIGIN = PIER_OPTIONS[0]
const DESTINATION = PIER_OPTIONS[PIER_OPTIONS.length - 1]
const BASE_FARE_VND = 15000
const DURATION_MINS = 45
const PASSENGER = 'Nguyen Van An'

interface Sample {
  id: string
  /** Days from today (negative = past). */
  day: number
  time: string
  seat: string
  bookingSuffix: string
}

function ticketOf({ id, day, time, seat: seatCode, bookingSuffix }: Sample): Ticket {
  const date = new Date()
  date.setDate(date.getDate() + day)
  const dateInfo = describeDate(toIsoDate(date))
  const seat = SEAT_MAP.seats.find((s) => s.code === seatCode)
  const [hours, minutes] = time.split(':').map(Number)
  const arrival = hours * 60 + minutes + DURATION_MINS
  const clock = (total: number) =>
    `${String(Math.floor(total / 60) % 24).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
  const completed = day < 0

  return {
    id,
    tripCode: 'SWB-01',
    lineName: t('Smart Waterbus • {line}', { line: 'Bạch Đằng - Linh Đông' }),
    routeTag: t('Main Route'),
    bookingRef: `WB${toIsoDate(date).replace(/-/g, '')}${time.replace(':', '')}00${bookingSuffix}`,
    ticketCode: `TK${bookingSuffix.padStart(2, '0')}${String(10000 + Number(bookingSuffix) * 7).padStart(6, '0')}`,
    status: completed ? t('Completed') : t('Confirmed'),
    completed,
    departTime: time,
    departPier: ORIGIN.pierName,
    departShort: ORIGIN.shortName,
    departDistrict: ORIGIN.district,
    arriveTime: clock(arrival),
    arrivePier: DESTINATION.pierName,
    arriveShort: DESTINATION.shortName,
    arriveDistrict: DESTINATION.district,
    durationMins: DURATION_MINS,
    dateLabel: dateInfo.fullLabel,
    dateId: dateInfo.id,
    dateShort: dateInfo.label,
    passenger: PASSENGER,
    seat: t('Seat {id}', { id: seatCode }),
    seatNote: `${seatPosition(seat)} • ${seatZone(seat)}`,
    windowSeat: Boolean(seat?.window),
    fareVnd: BASE_FARE_VND + seatFeeFor(seat, BASE_FARE_VND),
    vesselNote: 'Saigon Waterbus 01 SWB-01',
    gate: ORIGIN.shortName,
  }
}

export function buildTicketWallet(): TicketWallet {
  return {
    next: ticketOf({ id: 'tkt-sample-next', day: 1, time: '09:00', seat: 'S01', bookingSuffix: '11' }),
    later: [
      ticketOf({ id: 'tkt-sample-later-1', day: 3, time: '12:00', seat: 'F03', bookingSuffix: '12' }),
      ticketOf({ id: 'tkt-sample-later-2', day: 5, time: '07:00', seat: 'O05', bookingSuffix: '13' }),
    ],
    past: [
      ticketOf({ id: 'tkt-sample-past-1', day: -4, time: '09:00', seat: 'S12', bookingSuffix: '14' }),
      ticketOf({ id: 'tkt-sample-past-2', day: -6, time: '12:00', seat: 'S20', bookingSuffix: '15' }),
    ],
  }
}

export function buildAllTickets(): Ticket[] {
  const { next, later, past } = buildTicketWallet()
  return [next, ...later, ...past]
}

export function buildManageBooking(): ManageBooking {
  const { next } = buildTicketWallet()
  return {
    bookingCode: next.bookingRef,
    ticket: next,
    voucherCredit: [
      { code: 'WELCOME-RIVER', description: 'Welcome credit for first-time riders', valueVnd: 10000 },
    ],
  }
}

// Canned assistant replies. Keys match the quick-action chips.
export const ASSISTANT_REPLIES: Record<string, string> = {
  'Change Trip':
    'I can help you review available options. Try "View Available Trips" to pick another departure today.',
  'Refund Help':
    'You can request a refund from this page with "Request Refund". The pier team confirms every request.',
  'Explain Voucher':
    'Vouchers and credits are applied at checkout. You currently have one welcome credit on this booking.',
  'Trip Status':
    'Vessel SWB-01 is operating on schedule.',
}

export const ASSISTANT_FALLBACK =
  'Thanks for your question. A Pier Concierge can confirm the details; actions always need your confirmation.'
