import { change } from '../../test/fields'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import DataTable from './DataTable'

afterEach(cleanup)
const rows = Array.from({ length: 25 }, (_, id) => ({ id: String(id), name: `Boat ${id}` }))
const columns = [{ header: 'Boat', render: (row: typeof rows[number]) => row.name }]
const rowKey = (row: typeof rows[number]) => row.id

describe('admin table pagination', () => {
  it('pages through all results and resets after filtering', () => {
    const { rerender } = render(<DataTable paginate rows={rows} columns={columns} rowKey={rowKey} />)
    expect(screen.queryByText('Boat 10')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Next page' }))
    expect(screen.queryByText('Boat 0')).toBeNull()
    expect(screen.getByText('Boat 10')).toBeTruthy()
    rerender(<DataTable paginate rows={rows.slice(0, 2)} columns={columns} rowKey={rowKey} />)
    expect(screen.getByText('Boat 0')).toBeTruthy()
    expect(screen.queryByRole('navigation')).toBeNull()
  })
  it('changes page size and leaves server-paged tables untouched', () => {
    const { rerender } = render(<DataTable paginate rows={rows} columns={columns} rowKey={rowKey} />)
    change('Per page', '50')
    expect(screen.getByText('Boat 24')).toBeTruthy()
    rerender(<DataTable rows={rows} columns={columns} rowKey={rowKey} />)
    expect(screen.queryByLabelText('Per page')).toBeNull()
    expect(screen.getByText('Boat 24')).toBeTruthy()
  })
})
