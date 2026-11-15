import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { setActiveLocale } from '../../../i18n/translate'
import type { FareRule } from '../types'
import FareRuleForm from './FareRuleForm'

const fetchMock = vi.fn()
const body = () => JSON.parse(String((fetchMock.mock.calls[0][1] as RequestInit).body))

// Mốc gốc có giây, trong khi ô nhập datetime-local chỉ tới phút.
const rule: FareRule = {
  id: 'f1', tripType: 'Commuter', seatClassId: 'c1', seatClassCode: 'STD', seatClassName: 'Standard', price: 15000,
  currency: 'VND', effectiveFrom: '2026-10-20T15:05:37+00:00', effectiveTo: null, isActive: true, status: 'Active',
}

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  fetchMock.mockImplementation(async () => new Response('{}', { status: 200 }))
})
afterEach(() => {
  cleanup()
  fetchMock.mockReset()
  vi.unstubAllGlobals()
  setActiveLocale('en')
})

describe('FareRuleForm', () => {
  it('sends the original start of a fare in effect so only its end changes', async () => {
    render(<FareRuleForm rule={rule} seatClasses={[]} onDone={() => {}} onCancel={() => {}} />)
    fireEvent.change(screen.getByLabelText(/^Ends/), { target: { value: '2030-01-01T10:00' } })
    fireEvent.click(screen.getByRole('button', { name: /^Save/ }))
    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(body()).toMatchObject({ price: 15000, effectiveFrom: rule.effectiveFrom })
  })

  it('refuses to extend or clear the end of a fare in effect', () => {
    const ending = { ...rule, effectiveTo: '2027-01-01T03:00:00+00:00' }
    render(<FareRuleForm rule={ending} seatClasses={[]} onDone={() => {}} onCancel={() => {}} />)
    fireEvent.change(screen.getByLabelText(/^Ends/), { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: /^Save/ }))
    expect(screen.getByText(/can only be brought earlier/i)).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects a price with more than two decimals', () => {
    render(<FareRuleForm seatClasses={[{ id: 'c1', name: 'Standard' } as never]} onDone={() => {}} onCancel={() => {}} />)
    fireEvent.change(screen.getByLabelText(/^Price/), { target: { value: '10.005' } })
    fireEvent.change(screen.getByLabelText(/^Starts/), { target: { value: '2030-01-01T10:00' } })
    fireEvent.click(screen.getByRole('button', { name: /^Save/ }))
    expect(screen.getByText(/at most 2 decimals/i)).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
