import { getTripDetail } from '../../trips/services/tripService'
import { accountOrderPath, groupByBooking, manageLinkPath, manageLinkUrl, ticketStatusLabel } from '../utils'
import type { ManageBooking, Ticket, TicketWallet } from '../types'
import { API_ENDPOINTS } from '../../../constant/apiEndpoints'
import { ApiError, apiRequest } from '../../../services/apiClient'
import { loadBooking } from '../../booking/bookingStorage'
import { getCheckout, getBookingQr } from '../../booking/services/bookingService'
import type { TripDetailDto } from '../../trips/types'
import { toTrip } from '../../trips/utils'
import { toVietnamDate } from '../../../utils/time'
import { forgetOrder, loadSavedOrders, saveOrder } from '../orderStorage'
import { isPassenger } from '../../authentication/utils'
import { loadSession } from '../../../services/authStorage'
import { getAccountOrders } from './orderAccessService'

interface OrderRef {
  orderId: string
  accessToken: string | null
}

/** Số đơn của tài khoản được đọc cho "Vé của tôi"; đơn cũ hơn nằm trong lịch sử đơn hàng. */
const ACCOUNT_WALLET_ORDERS = 20

const signedInAsPassenger = () => isPassenger(loadSession()?.user ?? null)

/**
 * Đơn mà trang quản lý đặt chỗ được yêu cầu mở:
 * - link quản lý vé: token trong đường dẫn, mã đơn trong query `orderId` (BR-ID-02);
 * - `?orderId=` không kèm token: đơn thuộc tài khoản Passenger đang đăng nhập (BR-ID-04);
 * - không có gì: đơn vừa đặt trong tab này.
 */
function requestedOrder(token?: string): OrderRef | null {
  const orderId = new URLSearchParams(window.location.search).get('orderId')
  if (token) return orderId ? { orderId, accessToken: token } : null
  if (orderId) return signedInAsPassenger() ? { orderId, accessToken: null } : null
  return sessionOrder()
}

/**
 * Đơn vừa đặt trong tab này (giữ qua lượt chuyển sang VNPay). Token đã lưu trên thiết bị được ưu
 * tiên vì nó là token mới nhất sau khi khách đổi link.
 */
function sessionOrder(): OrderRef | null {
  const order = loadBooking().order
  if (!order?.orderId) return null
  const saved = loadSavedOrders().find((x) => x.orderId === order.orderId)
  return { orderId: order.orderId, accessToken: saved?.accessToken ?? order.accessToken ?? null }
}

/** True khi trang quản lý đặt chỗ có đơn để mở mà không cần khách dán link. */
export function hasOrderToManage(token?: string): boolean {
  return requestedOrder(token) !== null
}

/**
 * Các đơn đã có vé của tài khoản Passenger đang đăng nhập (BR-ID-04). Chúng được mở bằng JWT của
 * chủ đơn nên không cần token Guest, và hiện trên mọi thiết bị đã đăng nhập.
 */
async function accountOrders(): Promise<OrderRef[]> {
  if (!signedInAsPassenger()) return []
  const page = await getAccountOrders(1, ACCOUNT_WALLET_ORDERS)
  return page.items
    .filter((order) => order.bookings.some((booking) => booking.ticketCount > 0))
    .map((order) => ({ orderId: order.orderId, accessToken: null }))
}

/**
 * Mọi đơn mở được ở đây, không trùng: đơn của tài khoản, đơn của tab này, rồi các đơn Guest đã lưu
 * trên thiết bị. Đơn vừa thuộc tài khoản vừa có trên thiết bị thì mở bằng tài khoản.
 */
async function knownOrders(): Promise<OrderRef[]> {
  const refs: OrderRef[] = []
  for (const ref of [...(await accountOrders()), sessionOrder(), ...loadSavedOrders()]) {
    if (ref && !refs.some((x) => x.orderId === ref.orderId)) refs.push(ref)
  }
  return refs
}

/** Đơn không còn mở được: không tồn tại hoặc token bị thu hồi/hết hạn. */
const isGone = (error: unknown) =>
  error instanceof ApiError && (error.status === 403 || error.status === 404)

/**
 * Vé của mọi đơn mở được. Một đơn không còn mở được thì bị bỏ khỏi thiết bị thay vì làm hỏng
 * cả danh sách; lỗi mạng/máy chủ vẫn báo ra để người dùng thử lại.
 */
async function knownTickets(): Promise<Ticket[]> {
  const refs = await knownOrders()
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
          bookingId: booking.bookingId,
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
          manageLink: accessToken ? manageLinkPath(orderId, accessToken) : accountOrderPath(orderId),
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

/** The tickets of every known order: upcoming soonest first, past most recent first. */
export async function getTicketWallet(): Promise<TicketWallet> {
  const tickets = await knownTickets()
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
  const tickets = await knownTickets()
  // The success page may select an id that is not a backend ticket id: show the first ticket then.
  const ticket = (id ? tickets.find((t) => t.id === id) : undefined) ?? tickets[0]
  if (!ticket) throw new ApiError(404, 'Chưa có vé đã phát cho đơn hàng nào trên thiết bị này.')
  return ticket
}

/**
 * The order behind a Manage Order secure link (`token` + `?orderId=`, BR-ID-02), an order of the
 * signed-in account (`?orderId=` alone, BR-ID-04) or, with neither, the order of this tab.
 */
export async function getManageBooking(token?: string): Promise<ManageBooking> {
  const ref = requestedOrder(token)
  if (!ref) throw new ApiError(404, 'Link quản lý vé thiếu mã đơn hàng.')
  const tickets = await ticketsOfOrder(ref)
  if (!tickets[0]) throw new ApiError(404, 'Đơn hàng chưa có vé đã phát.')
  const departure = (ticket: Ticket) => `${ticket.dateId} ${ticket.departTime}`
  return {
    bookings: groupByBooking(tickets)
      .map((group) => ({ bookingId: group[0].bookingId, bookingCode: group[0].bookingRef, tickets: group }))
      .sort((a, b) => departure(a.tickets[0]).localeCompare(departure(b.tickets[0]))),
    orderId: ref.orderId,
    accessToken: ref.accessToken,
    manageUrl: ref.accessToken ? manageLinkUrl(ref.orderId, ref.accessToken) : undefined,
  }
}
