import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AssistantApiCard } from '../types'
import AssistantApiCards from './AssistantApiCards'
import { setActiveLocale } from '../../../i18n/translate'

const card: AssistantApiCard = {
  tripId: 'trip-id',
  boardingStationId: 'departure-id',
  disembarkingStationId: 'arrival-id',
  routeName: 'Bạch Đằng - Bình An',
  departureStationName: 'Bạch Đằng',
  arrivalStationName: 'Bình An',
  departureTime: '2026-10-20T01:00:00Z',
  availableSeatsCount: 12,
  basePrice: 0,
  isPriceVerified: false,
  bookingUrl: null,
  isDevelopmentPreview: true,
}

const renderCards = (cards: AssistantApiCard[], onSelect = vi.fn(), selectedId: string | null = null) =>
  render(<MemoryRouter><AssistantApiCards cards={cards} onSelect={onSelect} selectedId={selectedId} /></MemoryRouter>)

describe('AssistantApiCards', () => {
  afterEach(() => { cleanup(); setActiveLocale('en') })

  it('shows the departure in Vietnam time, the seats left and opens the chosen trip', () => {
    const onSelect = vi.fn()
    renderCards([card], onSelect)

    expect(screen.getByText('08:00')).toBeTruthy() // 01:00 UTC
    expect(screen.getByText('12 Seats Left')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Choose trip A' }))
    expect(onSelect).toHaveBeenCalledWith(card)
  })

  it('shows the API zero fare only in the internal preview', () => {
    renderCards([card])
    expect(screen.getByText('0 VND')).toBeTruthy()
    cleanup()
    renderCards([{ ...card, isDevelopmentPreview: false }])
    expect(screen.queryByText('0 VND')).toBeNull()
    expect(screen.getByText('Price shown when you choose a seat')).toBeTruthy()
  })

  it('shows a fare only once it is verified or in the development preview', () => {
    renderCards([{ ...card, basePrice: 15000, isDevelopmentPreview: false }])
    expect(screen.queryByText(/15\.000/)).toBeNull()
    cleanup()

    renderCards([{ ...card, basePrice: 15000, isPriceVerified: true }])
    expect(screen.getByText('15.000 VND')).toBeTruthy()
  })

  it('marks the selected trip and offers no choice on a sold-out one', () => {
    renderCards([card, { ...card, tripId: 'full', availableSeatsCount: 0 }], vi.fn(), 'trip-id')

    expect(screen.getByRole('button', { name: 'Choose trip A' }).getAttribute('aria-pressed')).toBe('true')
    expect(screen.getByText('Sold out')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Choose trip B' })).toBeNull()
  })

  it('does not offer a booking action when neither station ids nor a link exist', () => {
    renderCards([{ ...card, boardingStationId: null }])

    expect(screen.queryByRole('button', { name: 'Choose trip A' })).toBeNull()
  })

  it('shows English seat and action labels in the English interface', () => {
    setActiveLocale('en')
    render(<MemoryRouter><AssistantApiCards cards={[card]} onSelect={vi.fn()} /></MemoryRouter>)

    expect(screen.getByText('12 Seats Left')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Choose trip A' })).toBeTruthy()
  })
})
