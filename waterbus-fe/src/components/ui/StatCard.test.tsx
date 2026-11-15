import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import StatCard from './StatCard'

afterEach(cleanup)

describe('StatCard', () => {
  it('links to where the number comes from', () => {
    render(
      <MemoryRouter>
        <StatCard label="Trips today" value={20} to="/admin/trips" />
      </MemoryRouter>,
    )
    expect(screen.getByRole('link').getAttribute('href')).toBe('/admin/trips')
    expect(screen.getByText('20')).toBeTruthy()
  })

  it('highlights a number that needs attention only when it is above zero', () => {
    const { rerender } = render(<StatCard label="Open alerts" value={2} attention />)
    expect(screen.getByText('2').className).toContain('text-coral-glow')
    rerender(<StatCard label="Open alerts" value={0} attention />)
    expect(screen.getByText('0').className).not.toContain('text-coral-glow')
  })

  it('is plain text without a link', () => {
    render(<StatCard label="Open alerts" value={1} />)
    expect(screen.queryByRole('link')).toBeNull()
  })
})
