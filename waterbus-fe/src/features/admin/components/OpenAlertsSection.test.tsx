import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { WeatherAlert } from '../../operations/types'
import OpenAlertsSection from './OpenAlertsSection'

vi.mock('./AlertCard', () => ({
  default: ({ alert }: { alert: WeatherAlert }) => <article data-testid="alert">{alert.id}</article>,
}))

afterEach(cleanup)

const alert = (id: string, kind: WeatherAlert['kind'] = 'Weather'): WeatherAlert => ({
  id,
  level: 1,
  message: '',
  issuedAt: '2026-11-15T06:00:00Z',
  kind,
})

const renderSection = (alerts: WeatherAlert[]) =>
  render(<OpenAlertsSection alerts={alerts} trips={[]} version={0} onClose={vi.fn()} onDecide={vi.fn()} />)

const shown = () => screen.queryAllByTestId('alert').map((x) => x.textContent)

describe('OpenAlertsSection', () => {
  it('filters the alerts by type', () => {
    renderSection([alert('w1'), alert('s1', 'Safety')])
    fireEvent.click(screen.getByRole('button', { name: 'Safety' }))
    expect(shown()).toEqual(['s1'])
  })

  it('shows ten alerts per page', () => {
    renderSection(Array.from({ length: 12 }, (_, i) => alert(`a${i}`)))
    expect(shown()).toHaveLength(10)
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
    expect(shown()).toEqual(['a10', 'a11'])
  })

  it('stays on the last page left when alerts are closed', () => {
    const { rerender } = renderSection(Array.from({ length: 12 }, (_, i) => alert(`a${i}`)))
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
    rerender(<OpenAlertsSection alerts={[alert('a0')]} trips={[]} version={1} onClose={vi.fn()} onDecide={vi.fn()} />)
    expect(shown()).toEqual(['a0'])
  })

  it('says when no alert matches the filter', () => {
    renderSection([alert('w1')])
    fireEvent.click(screen.getByRole('button', { name: 'Safety' }))
    expect(shown()).toEqual([])
    expect(screen.getByText('No open incidents match this filter.')).toBeTruthy()
  })
})
