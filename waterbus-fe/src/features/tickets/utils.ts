import { t } from '../../i18n'
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
