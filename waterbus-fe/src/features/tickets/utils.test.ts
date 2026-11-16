import { describe, expect, it } from 'vitest'
import type { Ticket } from './types'
import { bookingFare, bookingQrPayload, bookingStatusLabel, manageLinkPath, manageLinkUrl, parseManageLink, splitUpcoming, ticketStatusLabel } from './utils'

describe('ticketStatusLabel', () => {
  it('shows a valid ticket as confirmed', () => {
    expect(ticketStatusLabel('Valid', 'NotCheckedIn')).toBe('Confirmed')
  })

  it('shows a checked-in ticket as boarded', () => {
    expect(ticketStatusLabel('Valid', 'CheckedIn')).toBe('Checked in at pier')
  })

  it('tells a completed no-show apart from a completed ride', () => {
    expect(ticketStatusLabel('Completed', 'NoShow')).toBe('No-show')
    expect(ticketStatusLabel('Completed', 'CheckedIn')).toBe('Checked in at pier')
  })

  it('passes an unknown status through instead of hiding it', () => {
    expect(ticketStatusLabel('Weird', 'NotCheckedIn')).toBe('Weird')
  })
})

const ORDER = '3f2c1a9e-5b7d-4c11-9a2e-0d6f8b7c4a10'

describe('manage link', () => {
  it('round-trips a full link and a bare path', () => {
    expect(manageLinkPath(ORDER, 'a+b/c')).toBe(`/manage/a%2Bb%2Fc?orderId=${ORDER}`)
    expect(parseManageLink(manageLinkUrl(ORDER, 'a+b/c'))).toEqual({
      orderId: ORDER,
      accessToken: 'a+b/c',
    })
    expect(parseManageLink(`  /manage/tok?orderId=${ORDER} `)).toEqual({
      orderId: ORDER,
      accessToken: 'tok',
    })
  })

  it('rejects text that is not a manage link', () => {
    expect(parseManageLink('')).toBeNull()
    expect(parseManageLink('WB123456')).toBeNull()
    expect(parseManageLink('/manage/tok')).toBeNull()
    expect(parseManageLink('/tickets?orderId=o1')).toBeNull()
    expect(parseManageLink('/manage/tok?orderId=not-a-guid')).toBeNull()
  })
})

describe('splitUpcoming', () => {
  const ticket = (id: string, bookingRef: string) => ({ id, bookingRef }) as Ticket

  it('keeps every ticket of the nearest booking together, even when bookings interleave', () => {
    // 2 vé của đơn B và 3 vé của đơn A cùng giờ đi: trước đây chỉ vé đầu tiên vào "kế tiếp".
    const { next, later } = splitUpcoming([
      ticket('a1', 'A'), ticket('b1', 'B'), ticket('a2', 'A'), ticket('a3', 'A'), ticket('b2', 'B'),
    ])
    expect(next.map((x) => x.id)).toEqual(['a1', 'a2', 'a3'])
    expect(later.map((x) => x.id)).toEqual(['b1', 'b2'])
  })

  it('returns empty lists when there is no upcoming ticket', () => {
    expect(splitUpcoming([])).toEqual({ next: [], later: [] })
  })

  it('has nothing later when all tickets belong to one booking', () => {
    expect(splitUpcoming([ticket('a1', 'A')])).toEqual({ next: [ticket('a1', 'A')], later: [] })
  })
})

describe('booking-level helpers', () => {
  const mk = (id: string, extra: Partial<Ticket> = {}) => ({ id, bookingRef: 'A', status: 'Confirmed', fareVnd: 10000, ...extra }) as Ticket

  it('uses the one QR of the booking and sums the fares', () => {
    const tickets = [mk('a', { qrPayload: 'qr' }), mk('b', { qrPayload: 'qr' })]
    expect(bookingQrPayload(tickets)).toBe('qr')
    expect(bookingFare(tickets)).toBe(20000)
    expect(bookingQrPayload([mk('c')])).toBeUndefined()
  })

  it('shows one status for equal tickets and a count when they differ', () => {
    expect(bookingStatusLabel([mk('a'), mk('b')])).toBe('Confirmed')
    expect(bookingStatusLabel([mk('a', { status: 'Checked in at pier', boardingStatus: 'CheckedIn' }), mk('b')])).toBe('1 of 2 checked in')
  })
})
