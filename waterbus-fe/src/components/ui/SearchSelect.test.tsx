import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import SearchSelect from './SearchSelect'
import FilterChips from './FilterChips'

beforeEach(() => {
  // jsdom không cung cấp phép đo layout; popup thực dùng ResizeObserver của trình duyệt.
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} })
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
const options = [
  { value: 'one', label: 'Bạch Đằng – Linh Đông', description: '13/11 10:30', badge: 'SWB-01' },
  { value: 'two', label: 'Linh Đông – Bạch Đằng', description: '14/11 08:00', badge: 'SWB-02' },
]
describe('searchable trip selection', () => {
  it('searches without accents and selects using the keyboard', async () => {
    const change = vi.fn()
    render(<SearchSelect label="Trip" value="" onChange={change} options={options} />)
    const input = screen.getByRole('combobox')
    fireEvent.change(input, { target: { value: 'bach dang' } })
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(2))
    fireEvent.change(input, { target: { value: 'SWB-02' } })
    await waitFor(() => expect(screen.getAllByRole('option')).toHaveLength(1))
    fireEvent.keyDown(input, { key: 'ArrowDown' })
    fireEvent.keyDown(input, { key: 'Enter' })
    await waitFor(() => expect(change).toHaveBeenCalledWith('two'))
  })
  it('shows no matches and closes with Escape without changing the selection', async () => {
    const change = vi.fn()
    render(<SearchSelect label="Trip" value="one" onChange={change} options={options} />)
    const input = screen.getByRole('combobox')
    fireEvent.change(input, { target: { value: 'missing boat' } })
    await waitFor(() => expect(screen.getByRole('status')).toBeTruthy())
    fireEvent.keyDown(input, { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).toBeNull())
    expect(change).not.toHaveBeenCalled()
  })
  it('exposes the current short filter and changes it with a button', () => {
    const change = vi.fn()
    render(<FilterChips label="Status" value="all" onChange={change} options={[{ value: 'all', label: 'All' }, { value: 'active', label: 'Active only' }]} />)
    expect(screen.getByRole('button', { pressed: true })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { pressed: false }))
    expect(change).toHaveBeenCalledWith('active')
  })
})
