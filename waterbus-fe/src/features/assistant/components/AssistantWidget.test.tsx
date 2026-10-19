import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Provider } from 'react-redux'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { store } from '../../../store/store'
import { ROUTES } from '../../../routes/routes'
import { askAssistant } from '../services/assistantService'
import AssistantWidget from './AssistantWidget'

vi.mock('../services/assistantService', () => ({ askAssistant: vi.fn() }))

describe('AssistantWidget live card navigation', () => {
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
  })

  it('uses the card trip and requested stations to open the existing trip flow', async () => {
    vi.mocked(askAssistant).mockResolvedValue({
      text: 'Tìm thấy một chuyến.',
      apiCards: [{
        tripId: '11111111-1111-1111-1111-111111111111',
        boardingStationId: '22222222-2222-2222-2222-222222222222',
        disembarkingStationId: '33333333-3333-3333-3333-333333333333',
        routeName: 'Bạch Đằng - Bình An',
        departureStationName: 'Bạch Đằng',
        arrivalStationName: 'Bình An',
        departureTime: '2026-10-20T01:00:00Z',
        availableSeatsCount: 12,
        basePrice: 0,
        isPriceVerified: false,
        bookingUrl: null,
      }],
    })

    render(
      <Provider store={store}>
        <MemoryRouter initialEntries={[ROUTES.home]}>
          <Routes>
            <Route path={ROUTES.home} element={<AssistantWidget />} />
            <Route path={ROUTES.tripDetail} element={<div>Trip detail opened</div>} />
          </Routes>
        </MemoryRouter>
      </Provider>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Tìm chuyến từ Bạch Đằng đến Bình An ngày 20/10/2026' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    fireEvent.click(await screen.findByRole('button', { name: 'Xem chuyến' }))
    await waitFor(() => expect(screen.getByText('Trip detail opened')).toBeTruthy())
    expect(store.getState().booking.selectedTripId).toBe('11111111-1111-1111-1111-111111111111')
    expect(store.getState().booking.query.originId).toBe('22222222-2222-2222-2222-222222222222')
    expect(store.getState().booking.query.destinationId).toBe('33333333-3333-3333-3333-333333333333')
    expect(store.getState().booking.query.dateId).toBe('2026-10-20')
  })
})
