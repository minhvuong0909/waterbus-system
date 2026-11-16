import { describe, expect, it } from 'vitest'
import { formatAxisMoney, formatPeriod, revenueRange } from './revenue'

describe('revenueRange', () => {
  it('counts today as the last day of the range', () => {
    expect(revenueRange(7, new Date('2026-11-16T05:00:00Z'))).toEqual({ from: '2026-11-10', to: '2026-11-16' })
  })

  it('uses the Vietnam date even when UTC is still the previous day', () => {
    // 16/11 18:00 UTC = 17/11 01:00 giờ Việt Nam.
    expect(revenueRange(1, new Date('2026-11-16T18:00:00Z'))).toEqual({ from: '2026-11-17', to: '2026-11-17' })
  })
})

describe('formatPeriod', () => {
  it('shows day/month for days and month/year for months', () => {
    expect(formatPeriod('2026-11-05', 'Day')).toBe('05/11')
    expect(formatPeriod('2026-11-01', 'Month')).toBe('11/2026')
  })
})

describe('formatAxisMoney', () => {
  it('shortens millions and thousands and keeps small amounts whole', () => {
    expect(formatAxisMoney(1_500_000)).toBe('1,5M')
    expect(formatAxisMoney(25_000)).toBe('25K')
    expect(formatAxisMoney(0)).toBe('0')
    expect(formatAxisMoney(500)).toBe('500')
  })
})
