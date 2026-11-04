import { getTripDetail } from '../../trips/services/tripService'
import { manageLinkPath, manageLinkUrl, ticketStatusLabel } from '../utils'
import type { ManageBooking, Ticket, TicketWallet } from '../types'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { loadBooking } from '../../booking/bookingStorage'
import { getCheckout, getBookingQr } from '../../booking/services/bookingService'
import type { TripDetailDto } from '../../trips/types'
import { toTrip } from '../../trips/utils'
import { toVietnamDate } from '../../../utils/time'
import { forgetOrder, loadSavedOrders, saveOrder } from '../orderStorage'

interface OrderRef {
  orderId: string
  accessToken: string | null
}

/** Đơn mở bằng link quản lý vé: token nằm trong đường dẫn, mã đơn trong query `orderId`. */
function linkOrder(token: string): OrderRef | null {
  const orderId = new URLSearchParams(window.location.search).get('orderId')
  return orderId ? { orderId, accessToken: token } : null
}

/** Đơn vừa đặt trong tab này (giữ qua lượt chuyển sang VNPay). */
function sessionOrder(): OrderRef | null {
  const order = loadBooking().order
  return order?.orderId ? { orderId: order.orderId, accessToken: order.accessToken ?? null } : null
}

/** True khi trang quản lý đặt chỗ có đơn để mở mà không cần khách dán link. */
export function hasOrderToManage(token?: string): boolean {
  return token ? linkOrder(token) !== null : sessionOrder() !== null
}

/**
 * Mọi đơn trên thiết bị: đơn của tab này rồi các đơn đã lưu, không trùng. Backend chưa có API
 * lịch sử theo tài khoản (BR-ID-04 chưa làm), nên đây là "Vé của tôi" của cả Guest lẫn Passenger.
 */
function deviceOrders(): OrderRef[] {
  const refs: OrderRef[] = []
  for (const ref of [sessionOrder(), ...loadSavedOrders()]) {
    if (ref && !refs.some((x) => x.orderId === ref.orderId)) refs.push(ref)
  }
  return refs
}

/** Đơn không còn mở được: không tồn tại hoặc token bị thu hồi/hết hạn. */
const isGone = (error: unknown) =>
  error instanceof ApiError && (error.status === 403 || error.status === 404)

/**
 * Vé của mọi đơn trên thiết bị. Một đơn không còn mở được thì bị bỏ khỏi thiết bị thay vì làm hỏng
 * cả danh sách; lỗi mạng/máy chủ vẫn báo ra để người dùng thử lại.
 */
async function deviceTickets(): Promise<Ticket[]> {
  const refs = deviceOrders()
  const results = await Promise.allSettled(refs.map(ticketsOfOrder))
  const tickets: Ticket[] = []
  results.forEach((result, i) => {
    if (result.status === 'fulfilled') tickets.push(...result.value)
    else if (isGone(result.reason)) forgetOrder(refs[i].orderId)
    else throw result.reason
  })
  return tickets
}

/** Chỉ hiện Ticket đã được backend phát cho một đơn. */
async function ticketsOfOrder({ orderId, accessToken }: OrderRef): Promise<Ticket[]> {
  const order = await getCheckout(orderId, accessToken)
  const paid = order.bookings.filter((booking) => booking.tickets.length > 0)

  // One trip is read once, even when several bookings of the order ride it.
  const details = new Map<string, Promise<TripDetailDto>>()
  const detailOf = (tripId: string) => {
    if (!details.has(tripId))
      details.set(tripId, apiRequest<TripDetailDto>(API_ENDPOINTS.trips.detail(tripId)))
    return details.get(tripId) as Promise<TripDetailDto>
  }

  const tickets = await Promise.all(
    paid.map(async (booking) => {
      const raw = await detailOf(booking.tripId)
      const boarding = raw.stops.find((s) => s.callId === booking.boardingCallId)
      const alighting = raw.stops.find((s) => s.callId === booking.disembarkingCallId)
      if (!boarding || !alighting) throw new ApiError(409, 'Không tìm thấy điểm dừng của vé.')
      const [trip, qr] = await Promise.all([
        getTripDetail(
          toTrip({
            id: raw.tripId,
            routeName: raw.routeName,
            departureStationName: boarding.stationName,
            arrivalStationName: alighting.stationName,
            departureTime: raw.departureTime,
            arrivalTime: raw.arrivalTime,
            durationMinutes: Math.round(
              (Date.parse(raw.arrivalTime) - Date.parse(raw.departureTime)) / 60000,
            ),
            tripType: raw.tripType,
            basePrice: 0,
            availableSeatsCount: 0,
          }),
          raw.tripId,
          {
            tripType: 'one-way',
            originId: boarding.stationId,
            destinationId: alighting.stationId,
            dateId: toVietnamDate(new Date(raw.departureTime)),
          },
        ),
        booking.status === 'Confirmed'
          ? getBookingQr(orderId, booking.bookingId, accessToken)
          : null,
      ])
      return booking.tickets.map(
        (ticket): Ticket => ({
          id: ticket.ticketId,
          tripId: raw.tripId,
          ticketCode: ticket.ticketCode,
          qrPayload: ticket.status === 'Valid' ? qr?.payload : undefined,
          tripCode: raw.boat?.code ?? raw.tripId,
          lineName: raw.routeName,
          routeTag: raw.tripType,
          bookingRef: booking.bookingCode,
          status: ticketStatusLabel(ticket.status, ticket.boardingStatus),
          departTime: trip.departTime,
          arriveTime: trip.arriveTime,
          durationMins: trip.durationMins,
          departPier: boarding.stationName,
          departShort: boarding.stationName,
          departDistrict: '',
          arrivePier: alighting.stationName,
          arriveShort: alighting.stationName,
          arriveDistrict: '',
          dateId: trip.dateId,
          dateLabel: trip.dateFull,
          dateShort: trip.dateShort,
          passenger: ticket.passengerName,
          seat: ticket.seatCode,
          seatNote: '',
          windowSeat: false,
          completed: ['Completed', 'Terminated'].includes(raw.status),
          fareVnd: ticket.paidFare,
          vesselNote: raw.boat?.name ?? '',
          gate: boarding.stationName,
          manageLink: accessToken ? manageLinkPath(orderId, accessToken) : undefined,
        }),
      )
    }),
  )
  // Đơn đã có vé thì ghi nhớ trên thiết bị, để đóng tab hay mở lại từ link vẫn thấy trong "Vé của tôi".
  if (accessToken && paid.length > 0) saveOrder(orderId, accessToken)
  return tickets.flat()
}

/** Khoá sắp xếp theo giờ khởi hành (ngày `YYYY-MM-DD` rồi giờ `HH:mm`, giờ Việt Nam). */
const departureKey = (ticket: Ticket) => `${ticket.dateId} ${ticket.departTime}`

/** The tickets of every order on this device: upcoming soonest first, past most recent first. */
export async function getTicketWallet(): Promise<TicketWallet> {
  const tickets = await deviceTickets()
  const upcoming = tickets
    .filter((t) => !t.completed)
    .sort((a, b) => departureKey(a).localeCompare(departureKey(b)))
  const past = tickets
    .filter((t) => t.completed)
    .sort((a, b) => departureKey(b).localeCompare(departureKey(a)))
  return { next: upcoming[0], later: upcoming.slice(1), past }
}

/** The ticket with this id; the first ticket of the order when `id` is not one of them. */
export async function getTicket(id: string | null): Promise<Ticket> {
  const tickets = await deviceTickets()
  // The success page may select an id that is not a backend ticket id: show the first ticket then.
  const ticket = (id ? tickets.find((t) => t.id === id) : undefined) ?? tickets[0]
  if (!ticket) throw new ApiError(404, 'Chưa có vé đã phát cho đơn hàng nào trên thiết bị này.')
  return ticket
}

/**
 * The order behind a Manage Order secure link (`token` + `?orderId=`, BR-ID-02) or, without a
 * token, the order of this tab. The backend has no booking lookup by account or booking code yet.
 */
export async function getManageBooking(token?: string): Promise<ManageBooking> {
  const ref = token ? linkOrder(token) : sessionOrder()
  if (!ref) throw new ApiError(404, 'Link quản lý vé thiếu mã đơn hàng.')
  const tickets = await ticketsOfOrder(ref)
  if (!tickets[0]) throw new ApiError(404, 'Đơn hàng chưa có vé đã phát.')
  return {
    bookingCode: tickets[0].bookingRef,
    ticket: tickets[0],
    orderId: ref.orderId,
    manageUrl: ref.accessToken ? manageLinkUrl(ref.orderId, ref.accessToken) : undefined,
  }
}
