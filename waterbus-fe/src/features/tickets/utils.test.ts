import { describe, expect, it } from 'vitest'
import { ticketStatusLabel } from './utils'

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
