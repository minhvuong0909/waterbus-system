import { describe, expect, it } from 'vitest'
import { formatAxisMoney, formatPeriod, formatReportDate, revenueCsv, revenueRange, revenueRangeError } from './revenue'

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

describe('report range and export', () => {
  it('accepts the inclusive 366-day boundary and rejects a longer or reversed range', () => {
    expect(revenueRangeError('2024-01-01', '2024-12-31')).toBeNull()
    expect(revenueRangeError('2024-01-01', '2025-01-01')).toBe('Choose a range of up to 366 days.')
    expect(revenueRangeError('2026-11-16', '2026-11-15')).toBe('The end date must be on or after the start date.')
    expect(revenueRangeError('2026-02-30', '2026-03-01')).toBe('Choose valid start and end dates.')
    expect(revenueRangeError('', '2026-03-01')).toBe('Choose valid start and end dates.')
    expect(formatReportDate('2026-11-16')).toBe('16/11/2026')
  })

  it('exports zero periods and negative net amounts without replacing them with formatted display text', () => {
    const csv = revenueCsv({ from: '2026-11-15', to: '2026-11-16', granularity: 'Day',
      totals: { collected: 0, refunded: 15000, net: -15000, paymentCount: 0, refundCount: 1 }, points: [
        { periodStart: '2026-11-15', collected: 0, refunded: 0, net: 0 },
        { periodStart: '2026-11-16', collected: 0, refunded: 15000, net: -15000 },
      ] }, ['Period', 'Collected', 'Refunded', 'Net'])
    expect(csv.startsWith('\uFEFF')).toBe(true)
    expect(csv).toContain('"2026-11-15",0,0,0')
    expect(csv).toContain('"2026-11-16",0,15000,-15000')
  })
})
