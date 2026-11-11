import { describe, expect, it } from 'vitest'
import { manageLinkPath, manageLinkUrl, parseManageLink, ticketStatusLabel } from './utils'

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

describe('manage link', () => {
  it('round-trips a full link and a bare path', () => {
    expect(manageLinkPath('o-1', 'a+b/c')).toBe('/manage/a%2Bb%2Fc?orderId=o-1')
    expect(parseManageLink(manageLinkUrl('o-1', 'a+b/c'))).toEqual({
      orderId: 'o-1',
      accessToken: 'a+b/c',
    })
    expect(parseManageLink('  /manage/tok?orderId=o-2 ')).toEqual({
      orderId: 'o-2',
      accessToken: 'tok',
    })
  })

  it('rejects text that is not a manage link', () => {
    expect(parseManageLink('')).toBeNull()
    expect(parseManageLink('WB123456')).toBeNull()
    expect(parseManageLink('/manage/tok')).toBeNull()
    expect(parseManageLink('/tickets?orderId=o1')).toBeNull()
  })
})
