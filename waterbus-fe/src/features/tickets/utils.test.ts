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
