import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { setActiveLocale } from '../../i18n/translate'
import DataTable from './DataTable'
import type { Column } from './DataTable'

interface Row {
  id: string
  name: string
}

const columns: Column<Row>[] = [{ header: 'Name', render: (r) => r.name }]
const makeRows = (n: number): Row[] => Array.from({ length: n }, (_, i) => ({ id: String(i + 1), name: `Trip ${i + 1}` }))

afterEach(() => {
  cleanup()
  setActiveLocale('en')
})

const table = (rows: Row[], pageSize?: number) => (
  <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} pageSize={pageSize} />
)

describe('DataTable paging', () => {
  it('shows every row and no pager when no page size is given', () => {
    render(table(makeRows(30)))

    expect(screen.getAllByRole('row')).toHaveLength(31)                       // tiêu đề + 30 dòng
    expect(screen.queryByRole('navigation')).toBeNull()
  })

  it('shows only one page of a long list, with the page counter', () => {
    render(table(makeRows(25), 10))

    expect(screen.getAllByRole('row')).toHaveLength(11)
    expect(screen.getByText('Trip 1')).toBeTruthy()
    expect(screen.queryByText('Trip 11')).toBeNull()
    expect(screen.getByText(/Page 1 of 3/)).toBeTruthy()
  })

  it('moves to the next and previous page, and disables the buttons at the ends', () => {
    render(table(makeRows(25), 10))
    const previous = screen.getByRole('button', { name: 'Previous' }) as HTMLButtonElement
    const next = screen.getByRole('button', { name: 'Next' }) as HTMLButtonElement
    expect(previous.disabled).toBe(true)

    fireEvent.click(next)
    expect(screen.getByText('Trip 11')).toBeTruthy()
    expect(screen.queryByText('Trip 1')).toBeNull()

    fireEvent.click(next)
    expect(screen.getByText('Trip 25')).toBeTruthy()                           // trang cuối chỉ có 5 dòng
    expect(screen.getAllByRole('row')).toHaveLength(6)
    expect(next.disabled).toBe(true)

    fireEvent.click(previous)
    expect(screen.getByText(/Page 2 of 3/)).toBeTruthy()
  })

  it('has no pager when everything fits on one page', () => {
    render(table(makeRows(10), 10))

    expect(screen.queryByRole('navigation')).toBeNull()
    expect(screen.getAllByRole('row')).toHaveLength(11)
  })

  it('falls back to the last page when a filter leaves fewer rows', () => {
    const { rerender } = render(table(makeRows(25), 10))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    fireEvent.click(screen.getByRole('button', { name: 'Next' }))              // đang ở trang 3

    rerender(table(makeRows(12), 10))                                           // lọc còn 12 dòng, chỉ có 2 trang

    expect(screen.getByText(/Page 2 of 2/)).toBeTruthy()
    expect(screen.getByText('Trip 11')).toBeTruthy()
  })

  it('shows the empty message instead of a pager for no rows', () => {
    render(<DataTable columns={columns} rows={[]} rowKey={(r: Row) => r.id} pageSize={10} empty="Nothing here yet." />)

    expect(screen.getByText('Nothing here yet.')).toBeTruthy()
    expect(screen.queryByRole('navigation')).toBeNull()
  })
})
