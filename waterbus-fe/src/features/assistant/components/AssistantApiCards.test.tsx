import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { AssistantApiCard } from '../types'
import AssistantApiCards from './AssistantApiCards'

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

describe('AssistantApiCards', () => {
  afterEach(cleanup)

  it('shows the Trip Search zero fare in internal preview and opens the selected trip', () => {
    const onSelect = vi.fn()
    render(<MemoryRouter><AssistantApiCards cards={[card]} onSelect={onSelect} /></MemoryRouter>)

    expect(screen.getByText(/0 ₫/).textContent).toContain('0 ₫')
    expect(screen.getByText(/12 ghế còn trống/)).toBeTruthy()
    expect(screen.queryByText(/demo|API báo/)).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Chọn chuyến A' }))
    expect(onSelect).toHaveBeenCalledWith(card)
  })

  it('does not offer a booking action when neither station ids nor a link exist', () => {
    render(<MemoryRouter><AssistantApiCards cards={[{ ...card, boardingStationId: null }]} onSelect={vi.fn()} /></MemoryRouter>)

    expect(screen.queryByText('Chọn chuyến A')).toBeNull()
  })

  it('does not expose an unverified fare outside the explicit development preview', () => {
    render(<MemoryRouter><AssistantApiCards cards={[{ ...card, isDevelopmentPreview: false }]} onSelect={vi.fn()} /></MemoryRouter>)

    expect(screen.getByText(/Giá chưa được xác nhận/)).toBeTruthy()
    expect(screen.queryByText(/0 ₫/)).toBeNull()
  })
})
