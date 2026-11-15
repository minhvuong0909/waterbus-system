import { useState } from 'react'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import Tabs from './Tabs'

afterEach(cleanup)

type Key = 'a' | 'b' | 'c'

function Harness({ start = 'a' as Key }: { start?: Key }) {
  const [value, setValue] = useState<Key>(start)
  return (
    <Tabs
      label="Steps"
      value={value}
      onChange={setValue}
      tabs={[
        { key: 'a', label: 'First' },
        { key: 'b', label: 'Second', count: 3 },
        { key: 'c', label: 'Third', count: 0 },
      ]}
    >
      <p>Panel {value}</p>
    </Tabs>
  )
}

describe('Tabs', () => {
  it('shows the panel of the tab that is clicked', () => {
    render(<Harness />)
    fireEvent.click(screen.getByRole('tab', { name: /Second/ }))
    expect(screen.getByRole('tab', { name: /Second/ }).getAttribute('aria-selected')).toBe('true')
    expect(screen.getByRole('tabpanel').textContent).toBe('Panel b')
  })

  it('moves with the arrow keys and wraps around at the ends', () => {
    render(<Harness start="c" />)
    fireEvent.keyDown(screen.getByRole('tablist'), { key: 'ArrowRight' })
    expect(screen.getByRole('tabpanel').textContent).toBe('Panel a')
    fireEvent.keyDown(screen.getByRole('tablist'), { key: 'ArrowLeft' })
    expect(screen.getByRole('tabpanel').textContent).toBe('Panel c')
  })

  it('shows a count only when there is something to count', () => {
    render(<Harness />)
    expect(screen.getByRole('tab', { name: /Second/ }).textContent).toBe('Second3')
    expect(screen.getByRole('tab', { name: /Third/ }).textContent).toBe('Third')
  })
})
