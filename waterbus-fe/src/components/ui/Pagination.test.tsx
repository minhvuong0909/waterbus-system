import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import Pagination from './Pagination'

afterEach(cleanup)

describe('Pagination', () => {
  it('moves one page forward and back', () => {
    const onChange = vi.fn()
    render(<Pagination pageNumber={2} totalPages={5} onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
    fireEvent.click(screen.getByRole('button', { name: 'Previous page' }))
    expect(onChange.mock.calls).toEqual([[3], [1]])
  })

  it('cannot go past the first or the last page, nor while busy', () => {
    const onChange = vi.fn()
    const { rerender } = render(<Pagination pageNumber={1} totalPages={2} onChange={onChange} />)
    expect((screen.getByRole('button', { name: 'Previous page' }) as HTMLButtonElement).disabled).toBe(true)
    rerender(<Pagination pageNumber={2} totalPages={2} onChange={onChange} />)
    expect((screen.getByRole('button', { name: 'Next page' }) as HTMLButtonElement).disabled).toBe(true)
    rerender(<Pagination pageNumber={1} totalPages={2} onChange={onChange} busy />)
    expect((screen.getByRole('button', { name: 'Next page' }) as HTMLButtonElement).disabled).toBe(true)
    expect(onChange).not.toHaveBeenCalled()
  })

  it('is hidden when there is a single page', () => {
    render(<Pagination pageNumber={1} totalPages={1} onChange={vi.fn()} />)
    expect(screen.queryByRole('navigation')).toBeNull()
  })
})
