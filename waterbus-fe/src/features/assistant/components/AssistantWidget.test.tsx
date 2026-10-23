import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { Provider } from 'react-redux'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { store } from '../../../store/store'
import { ROUTES } from '../../../routes/routes'
import { askAssistant } from '../services/assistantService'
import AssistantWidget from './AssistantWidget'
import { setActiveLocale } from '../../../i18n/translate'

vi.mock('../services/assistantService', () => ({ askAssistant: vi.fn() }))

describe('AssistantWidget live card navigation', () => {
  beforeEach(() => setActiveLocale('vi'))
  afterEach(() => {
    cleanup()
    vi.clearAllMocks()
    setActiveLocale('en')
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

    fireEvent.click(await screen.findByRole('button', { name: 'Choose trip A' }))
    expect(screen.getByText(/This is not a booking yet/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /Continue to booking/ }))
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
    await screen.findByRole('button', { name: 'Choose trip A' })
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'chọn chuyến A' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(screen.getByText(/This is not a booking yet/)).toBeTruthy()
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
  it('offers "Try again" after a failed request and sends the same question again', async () => {
    vi.mocked(askAssistant)
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValueOnce({ text: 'Có 2 chuyến chiều nay.' })

    render(<Provider store={store}><MemoryRouter><AssistantWidget /></MemoryRouter></Provider>)
    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Chiều nay có chuyến không?' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    fireEvent.click(await screen.findByRole('button', { name: 'Try again' }))
    await screen.findByText('Có 2 chuyến chiều nay.')
    expect(askAssistant).toHaveBeenCalledTimes(2)
    expect(vi.mocked(askAssistant).mock.calls[1][0]).toBe('Chiều nay có chuyến không?')
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull()
    expect(screen.getAllByText('Chiều nay có chuyến không?')).toHaveLength(1)
  })

  it('starts a new conversation without the previous turns or search context', async () => {
    vi.mocked(askAssistant).mockResolvedValue({ text: 'Đi ngày nào?', searchContext: { departureStationName: 'Bạch Đằng' } })

    render(<Provider store={store}><MemoryRouter><AssistantWidget /></MemoryRouter></Provider>)
    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), { target: { value: 'Từ Bạch Đằng' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByText('Đi ngày nào?')

    fireEvent.click(screen.getByRole('button', { name: 'New conversation' }))
    expect(screen.queryByText('Đi ngày nào?')).toBeNull()
    expect(screen.getByText('You can ask:')).toBeTruthy()

    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), { target: { value: 'Ngày mai' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await waitFor(() => expect(askAssistant).toHaveBeenLastCalledWith('Ngày mai', [], null))
  })

  it('confirms a typed English card choice in English', async () => {
    setActiveLocale('en')
    vi.mocked(askAssistant).mockResolvedValue({
      text: 'Found one trip.',
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
        isDevelopmentPreview: true,
      }],
    })
    render(<Provider store={store}><MemoryRouter><AssistantWidget /></MemoryRouter></Provider>)
    fireEvent.click(screen.getByRole('button', { name: 'Open assistant' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'Show all trips' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByRole('button', { name: 'Choose trip A' })
    fireEvent.change(screen.getByRole('textbox', { name: 'Ask the assistant' }), {
      target: { value: 'select trip A' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(screen.getByText(/This is not a booking yet/)).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Continue to booking' })).toBeTruthy()
  })
})
