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
  /** Mặc định: vé còn hiệu lực, chưa check-in (ngày đã qua = đã hoàn tất). */
  lifecycle?: Ticket['lifecycle']
  boardingStatus?: Ticket['boardingStatus']
  refund?: Ticket['refund']
  passenger?: string
}

function ticketOf({ id, day, time, seat: seatCode, bookingSuffix, lifecycle, boardingStatus, refund, passenger }: Sample): Ticket {
  const date = new Date()
  date.setDate(date.getDate() + day)
  const dateInfo = describeDate(toIsoDate(date))
  const seat = SEAT_MAP.seats.find((s) => s.code === seatCode)
  const [hours, minutes] = time.split(':').map(Number)
  const arrival = hours * 60 + minutes + DURATION_MINS
  const clock = (total: number) =>
    `${String(Math.floor(total / 60) % 24).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
  const state = lifecycle ?? (day < 0 ? 'Completed' : 'Valid')
  // Vé đã huỷ, kết thúc sớm hoặc hoàn tất không còn nằm ở danh sách "sắp tới".
  const completed = state !== 'Valid'
  const statusText: Record<NonNullable<Ticket['lifecycle']>, string> = {
    Valid: boardingStatus === 'CheckedIn' ? t('Checked in at pier') : t('Confirmed'),
    Completed: boardingStatus === 'NoShow' ? t('No-show') : t('Completed'),
    Cancelled: t('Trip cancelled'),
    Terminated: t('Trip ended early'),
  }

  return {
    id,
    tripCode: 'SWB-01',
    lineName: t('Smart Waterbus • {line}', { line: 'Bạch Đằng - Linh Đông' }),
    routeTag: t('Main Route'),
    bookingRef: `WB${toIsoDate(date).replace(/-/g, '')}${time.replace(':', '')}00${bookingSuffix}`,
    ticketCode: `TK${bookingSuffix.padStart(2, '0')}${String(10000 + Number(bookingSuffix) * 7).padStart(6, '0')}`,
    status: statusText[state],
    completed,
    lifecycle: state,
    boardingStatus: boardingStatus ?? 'NotCheckedIn',
    refund,
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
    passenger: passenger ?? PASSENGER,
    seat: t('Seat {id}', { id: seatCode }),
    seatNote: `${seatPosition(seat)} • ${seatZone(seat)}`,
    windowSeat: Boolean(seat?.window),
    fareVnd: BASE_FARE_VND + seatFeeFor(seat, BASE_FARE_VND),
    vesselNote: 'Saigon Waterbus 01 SWB-01',
    gate: ORIGIN.shortName,
  }
}

export function buildTicketWallet(): TicketWallet & { next: Ticket } {
  return {
    next: ticketOf({ id: 'tkt-sample-next', day: 1, time: '09:00', seat: 'S01', bookingSuffix: '11' }),
    later: [
      ticketOf({ id: 'tkt-sample-later-1', day: 3, time: '12:00', seat: 'F03', bookingSuffix: '12' }),
      ticketOf({ id: 'tkt-sample-later-2', day: 5, time: '07:00', seat: 'O05', bookingSuffix: '13' }),
      // Cùng một đặt chỗ, hai hành khách: một người đã check-in, một người chưa đến (BR-COM-09).
      ticketOf({ id: 'tkt-sample-later-3', day: 1, time: '15:30', seat: 'S05', bookingSuffix: '16', boardingStatus: 'CheckedIn', passenger: 'Tran Thi Binh' }),
      ticketOf({ id: 'tkt-sample-later-4', day: 1, time: '15:30', seat: 'S06', bookingSuffix: '16', passenger: 'Le Van Cuong' }),
    ],
    past: [
      ticketOf({ id: 'tkt-sample-past-1', day: -4, time: '09:00', seat: 'S12', bookingSuffix: '14', boardingStatus: 'CheckedIn' }),
      ticketOf({ id: 'tkt-sample-past-2', day: -6, time: '12:00', seat: 'S20', bookingSuffix: '15', boardingStatus: 'CheckedIn' }),
      // Khách không check-in đúng hạn: NoShow, không hoàn tiền (BR-RF-06).
      ticketOf({ id: 'tkt-sample-past-3', day: -2, time: '07:00', seat: 'S08', bookingSuffix: '17', boardingStatus: 'NoShow' }),
      // Chuyến bị huỷ trước khi chạy: hoàn 100% phần đã trả (BR-RF-01).
      ticketOf({ id: 'tkt-sample-past-4', day: -1, time: '12:00', seat: 'F01', bookingSuffix: '18', lifecycle: 'Cancelled', refund: { amountVnd: 15000 + 10000, state: 'Succeeded' } }),
      // Chuyến kết thúc sớm giữa hành trình, khoản hoàn đang chờ Admin xử lý thủ công (BR-RF-04, BR-RF-10).
      ticketOf({ id: 'tkt-sample-past-5', day: -3, time: '15:30', seat: 'S15', bookingSuffix: '19', lifecycle: 'Terminated', boardingStatus: 'CheckedIn', refund: { amountVnd: 15000, state: 'Processing' } }),
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
  }
}

// Canned assistant replies. Keys match the quick-action chips.
export const ASSISTANT_REPLIES: Record<string, string> = {
  'Refund Help':
    'If a trip is cancelled or ends early, the amount you paid for the affected tickets is refunded to your original payment method. Passengers who miss check-in are not refunded.',
  'Trip Status':
    'Vessel SWB-01 is operating on schedule.',
}

export const ASSISTANT_FALLBACK =
  'Thanks for your question. A Pier Concierge can confirm the details; actions always need your confirmation.'
