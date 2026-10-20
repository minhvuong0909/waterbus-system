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
      searchContext: { passengerCount: 2 },
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

    fireEvent.click(await screen.findByRole('button', { name: 'Chọn chuyến A' }))
    expect(screen.getByText(/Đây chưa phải đặt vé thành công/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Tiếp tục đặt vé' }))
    await waitFor(() => expect(screen.getByText('Trip detail opened')).toBeTruthy())
    expect(store.getState().booking.selectedTripId).toBe('11111111-1111-1111-1111-111111111111')
    expect(store.getState().booking.query.originId).toBe('22222222-2222-2222-2222-222222222222')
    expect(store.getState().booking.query.destinationId).toBe('33333333-3333-3333-3333-333333333333')
    expect(store.getState().booking.query.dateId).toBe('2026-10-20')
  })

  it('keeps search context for a follow-up and accepts a typed card choice', async () => {
    vi.mocked(askAssistant).mockResolvedValueOnce({
      text: 'Tìm thấy chuyến.',
      searchContext: { departureStationName: 'Bạch Đằng', passengerCount: 2 },
      apiCards: [{
        tripId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        boardingStationId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        disembarkingStationId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        routeName: 'Bạch Đằng - Bình An',
        departureStationName: 'Bạch Đằng',
        arrivalStationName: 'Bình An',
        departureTime: '2026-10-20T01:00:00Z',
        availableSeatsCount: 12,
        basePrice: 0,
        isPriceVerified: false,
        bookingUrl: null,
      }],
    }).mockResolvedValue({ text: 'Không tìm thấy chuyến.' })

    render(<Provider store={store}><MemoryRouter><AssistantWidget /></MemoryRouter></Provider>)
    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Tìm chuyến Bạch Đằng' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByRole('button', { name: 'Chọn chuyến A' })
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'chọn chuyến A' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(screen.getByText(/Đây chưa phải đặt vé thành công/)).toBeTruthy()
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'sớm hơn' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await waitFor(() => expect(askAssistant).toHaveBeenLastCalledWith('sớm hơn', expect.any(Array),
      { departureStationName: 'Bạch Đằng', passengerCount: 2 }))
  })

  it('sends recent chat turns with the next question', async () => {
    vi.mocked(askAssistant).mockResolvedValue({ text: 'Quý khách muốn đi ngày nào?' })

    render(
      <Provider store={store}>
        <MemoryRouter><AssistantWidget /></MemoryRouter>
      </Provider>,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Từ Bến Bạch Đằng đến Bến Linh Đông' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByText('Quý khách muốn đi ngày nào?')

    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Ngày 2026-10-20' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(askAssistant).toHaveBeenLastCalledWith('Ngày 2026-10-20', [
        { role: 'user', content: 'Từ Bến Bạch Đằng đến Bến Linh Đông' },
        { role: 'assistant', content: 'Quý khách muốn đi ngày nào?' },
    ], null))
  })
})
