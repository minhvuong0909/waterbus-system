import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import RevenuePage from './RevenuePage'
import { MemoryRouter } from 'react-router-dom'
import { getRevenueReport } from '../../features/admin/services/revenueService'
import type { RevenueReport } from '../../features/admin/types'
import { saveBlob } from '../../utils/download'

vi.mock('../../features/admin/services/revenueService', () => ({ getRevenueReport: vi.fn() }))
vi.mock('../../utils/download', () => ({ saveBlob: vi.fn() }))
vi.mock('../../features/admin/components/RevenueChart', () => ({ default: ({ summary }: { summary: string }) => <div role="img" aria-label={summary} /> }))
vi.mock('../../i18n', () => ({ t: (key: string, values?: Record<string, string | number>) => key.replace(/\{(\w+)\}/g, (_, name: string) => String(values?.[name] ?? name)), numberLocale: () => 'vi-VN' }))

const fixture: RevenueReport = {
  from: '2026-11-14', to: '2026-11-16', granularity: 'Day',
  totals: { collected: 40000, refunded: 15000, net: 25000, paymentCount: 2, refundCount: 1 },
  points: [
    { periodStart: '2026-11-14', collected: 0, refunded: 0, net: 0 },
    { periodStart: '2026-11-15', collected: 40000, refunded: 0, net: 40000 },
    { periodStart: '2026-11-16', collected: 0, refunded: 15000, net: -15000 },
  ],
}

beforeEach(() => { vi.clearAllMocks(); vi.mocked(getRevenueReport).mockResolvedValue(fixture) })
afterEach(cleanup)

describe('RevenuePage', () => {
  it('shows a negative refund period and keeps zero periods available for reconciliation', async () => {
    render(<MemoryRouter><RevenuePage /></MemoryRouter>)
    await screen.findByText('Revenue breakdown')
    const table = screen.getByRole('table')
    expect(within(table).getByText('-15.000 VND')).toBeTruthy()
    expect(within(table).queryByText('14/11/2026')).toBeNull()
    fireEvent.click(screen.getByRole('checkbox', { name: 'Only periods with activity' }))
    expect(within(screen.getByRole('table')).getByText('14/11/2026')).toBeTruthy()
    expect(screen.getByText('2 of 3 periods have cash activity; all other periods are zero.')).toBeTruthy()
  })

  it('validates a custom range before requesting it, then requests the exact dates and grouping', async () => {
    render(<MemoryRouter><RevenuePage /></MemoryRouter>)
    await screen.findByText('Revenue breakdown')
    fireEvent.click(screen.getByLabelText('Period'))
    fireEvent.click(await screen.findByRole('option', { name: /Custom date range/ }))
    fireEvent.change(screen.getByLabelText('From date'), { target: { value: '2026-11-16' } })
    fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-01' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply range' }))
    expect(screen.getByRole('alert').textContent).toContain('The end date must be on or after the start date.')
    expect(getRevenueReport).toHaveBeenCalledTimes(1)
    fireEvent.change(screen.getByLabelText('To date'), { target: { value: '2026-11-16' } })
    fireEvent.click(screen.getByRole('button', { name: 'Apply range' }))
    await waitFor(() => expect(getRevenueReport).toHaveBeenLastCalledWith('2026-11-16', '2026-11-16', 'Day'))
    fireEvent.click(screen.getByLabelText('Group'))
    fireEvent.click(await screen.findByRole('option', { name: /By month/ }))
    await waitFor(() => expect(getRevenueReport).toHaveBeenLastCalledWith('2026-11-16', '2026-11-16', 'Month'))
  })

  it('downloads the complete report even when the table only shows active periods', async () => {
    render(<MemoryRouter><RevenuePage /></MemoryRouter>)
    await screen.findByText('Revenue breakdown')
    fireEvent.click(screen.getByRole('button', { name: 'Download CSV' }))
    expect(saveBlob).toHaveBeenCalledWith(expect.any(Blob), 'revenue_2026-11-14_2026-11-16_day.csv')
  })

  it('keeps the filters available for retry when the server fails', async () => {
    vi.mocked(getRevenueReport).mockRejectedValueOnce(new Error('unavailable'))
    render(<MemoryRouter><RevenuePage /></MemoryRouter>)
    await screen.findByRole('alert')
    expect(screen.getByLabelText('Period')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Download CSV' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))
    await screen.findByText('Revenue breakdown')
  })

  it('shows the empty state for no completed transactions', async () => {
    vi.mocked(getRevenueReport).mockResolvedValue({ ...fixture, totals: { collected: 0, refunded: 0, net: 0, paymentCount: 0, refundCount: 0 },
      points: fixture.points.map((point) => ({ ...point, collected: 0, refunded: 0, net: 0 })) })
    render(<MemoryRouter><RevenuePage /></MemoryRouter>)
    expect(await screen.findByText('No completed transactions')).toBeTruthy()
    expect(screen.queryByRole('img')).toBeNull()
  })
})
