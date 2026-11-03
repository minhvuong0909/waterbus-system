import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'
import { toVietnamDate } from '../../utils/time'
import type { Ticket } from './types'

/** True when the ticket is for today. */
export const isTicketToday = (ticket: Pick<Ticket, 'dateId'>): boolean =>
  ticket.dateId === toVietnamDate(new Date())

/** Label of a backend ticket for the traveler (`TicketStatus` and `BoardingStatus` of the BE). */
export function ticketStatusLabel(status: string, boardingStatus: string): string {
  if (status === 'CheckedIn' || boardingStatus === 'CheckedIn') return t('Checked in at pier')
  switch (status) {
    case 'Valid':
      return t('Confirmed')
    case 'Completed':
      return boardingStatus === 'NoShow' ? t('No-show') : t('Completed')
    case 'Cancelled':
      return t('Cancelled')
    case 'Terminated':
      return t('Trip ended early')
    case 'Expired':
      return t('Expired')
    default:
      return status
  }
}

/** Gom các vé cùng một đặt chỗ (cùng `bookingRef`), giữ thứ tự xuất hiện đầu tiên. */
export function groupByBooking(tickets: Ticket[]): Ticket[][] {
  const groups = new Map<string, Ticket[]>()
  for (const ticket of tickets) {
    const group = groups.get(ticket.bookingRef)
    if (group) group.push(ticket)
    else groups.set(ticket.bookingRef, [ticket])
  }
  return [...groups.values()]
}

/** Đường dẫn Manage Order secure link của một đơn: token trong path, mã đơn trong query. */
export function manageLinkPath(orderId: string, accessToken: string): string {
  return `${ROUTES.manageByLink.replace(
    ':token',
    encodeURIComponent(accessToken),
  )}?orderId=${encodeURIComponent(orderId)}`
}

/** Link đầy đủ để khách lưu lại hoặc gửi cho chính mình. */
export function manageLinkUrl(orderId: string, accessToken: string): string {
  return `${window.location.origin}${manageLinkPath(orderId, accessToken)}`
}

/**
 * Đọc mã đơn và token từ một link quản lý vé khách dán vào (đầy đủ hoặc chỉ phần đường dẫn).
 * Trả về null khi không phải link quản lý vé.
 */
export function parseManageLink(input: string): { orderId: string; accessToken: string } | null {
  const text = input.trim()
  if (!text) return null
  let url: URL
  try {
    url = new URL(text, window.location.origin)
  } catch {
    return null
  }
  const prefix = ROUTES.manageByLink.replace(':token', '')
  if (!url.pathname.startsWith(prefix)) return null
  const segment = url.pathname.slice(prefix.length).replace(/\/+$/, '')
  const orderId = url.searchParams.get('orderId')
  if (!segment || segment.includes('/') || !orderId) return null
  try {
    return { orderId, accessToken: decodeURIComponent(segment) }
  } catch {
    return null
  }
}
