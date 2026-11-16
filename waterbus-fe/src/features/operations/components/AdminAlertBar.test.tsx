import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { loadDictionary, setActiveLocale } from '../../../i18n/translate'
import type { OpenAlertSummary } from '../hooks/useOpenAlertSummary'
import AdminAlertBar from './AdminAlertBar'

afterEach(() => {
  cleanup()
  setActiveLocale('en')
})

const show = (summary: OpenAlertSummary | null, pathname = '/admin/trips') =>
  render(
    <MemoryRouter>
      <AdminAlertBar summary={summary} pathname={pathname} />
    </MemoryRouter>,
  )

describe('AdminAlertBar', () => {
  it('shows nothing while no alert is open', () => {
    const { container } = show(null)
    expect(container.firstChild).toBeNull()
  })

  it('tells the admin on any page how many alerts are open and links to the Incidents page', () => {
    show({ open: 2, waiting: 3, level: 1 })

    expect(screen.getByRole('alert')).toBeTruthy()
    expect(screen.getByText('Open alerts: 2')).toBeTruthy()
    expect(screen.getByText('Trip decisions waiting: 3')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'View and handle' }).getAttribute('href')).toBe('/admin/incidents')
  })

  it('leaves out the waiting count when every trip has been decided', () => {
    show({ open: 1, waiting: 0, level: 2 })

    expect(screen.getByText('Open alerts: 1')).toBeTruthy()
    expect(screen.queryByText(/Trip decisions waiting/)).toBeNull()
  })

  it('stays hidden on the Incidents page, which lists the same alerts in full', () => {
    const { container } = show({ open: 2, waiting: 1, level: 1 }, '/admin/incidents')
    expect(container.firstChild).toBeNull()
  })

  it('stays hidden on the Overview, whose cards already show the open alerts', () => {
    expect(show({ open: 2, waiting: 1, level: 1 }, '/admin').container.firstChild).toBeNull()
    expect(show({ open: 2, waiting: 1, level: 1 }, '/admin/').container.firstChild).toBeNull()
  })

  it('reads in Vietnamese', async () => {
    await loadDictionary('vi')                                              // từ điển tiếng Việt nạp theo yêu cầu
    setActiveLocale('vi')
    show({ open: 1, waiting: 1, level: 1 })

    expect(screen.getByText('Cảnh báo đang mở: 1')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Xem và xử lý' })).toBeTruthy()
  })
})
