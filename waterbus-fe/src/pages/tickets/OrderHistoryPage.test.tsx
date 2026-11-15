import { StrictMode } from 'react'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { getToken } from '../../services/authStorage'
import type { AccountOrder } from '../../features/tickets/types'
import OrderHistoryPage from './OrderHistoryPage'

const fetchMock = vi.fn()
vi.mock('../../services/authStorage', () => ({ getToken: vi.fn() }))
vi.mock('../../store/hooks', () => ({ useAppSelector: () => 'passenger-jwt' }))

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  vi.mocked(getToken).mockReturnValue('passenger-jwt')
})
afterEach(() => {
  cleanup()
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const date = '2026-11-15T08:00:00Z'
const guestOrder: AccountOrder = {
  orderId: 'past-order', orderStatus: 'Confirmed', purchaseMode: 'RoundTrip', quotedTotal: 100000,
  currency: 'VND', createdAt: date, confirmedAt: date,
  bookings: ['Outbound fixture', 'Return fixture'].map((routeName, index) => ({
    bookingId: `leg-${index}`, tripId: `trip-${index}`, bookingCode: `code-${index}`,
    routeName, departureTime: date, bookingStatus: 'Confirmed', paymentStatus: 'Success', ticketCount: 1,
  })),
}

function renderHistory() {
  return render(<StrictMode><MemoryRouter><OrderHistoryPage /></MemoryRouter></StrictMode>)
}

function responses(count: () => number, post: () => unknown, items: () => AccountOrder[]) {
  const json = (value: unknown) =>
    new Response(JSON.stringify(value), { headers: { 'Content-Type': 'application/json' } })
  fetchMock.mockImplementation(async (url: string, { method }: RequestInit) => {
    if (url.endsWith('/me/orders/linkable') && method === 'GET') return json({ count: count() })
    if (url.endsWith('/me/orders/link-by-email') && method === 'POST') return json(post())
    if (url.includes('/me/orders?') && method === 'GET') return json({
      items: items(), pageNumber: 1, totalPages: 1, totalCount: items().length,
    })
    throw new Error(`Unexpected request: ${method} ${url}`)
  })
}

const callsFor = (suffix: string) => fetchMock.mock.calls.filter(([url]: [string]) => url.endsWith(suffix))

it('links remaining batches automatically and refreshes both legs without a button or duplicate linking on reopen', async () => {
  let batches = 0
  responses(
    () => batches < 2 ? 2 : 0,
    () => ({ linked: 1, remaining: 2 - ++batches }),
    () => batches === 2 ? [guestOrder] : [],
  )
  const firstVisit = renderHistory()

  expect(await screen.findByText('Outbound fixture')).toBeTruthy()
  expect(screen.getByText('Return fixture')).toBeTruthy()
  expect(screen.queryByRole('button', { name: /Add to my account|Thêm vào tài khoản/i })).toBeNull()
  expect(callsFor('/linkable')).toHaveLength(1)
  expect(callsFor('/link-by-email')).toHaveLength(2)

  firstVisit.unmount()
  renderHistory()
  expect(await screen.findByText('Outbound fixture')).toBeTruthy()
  expect(callsFor('/linkable')).toHaveLength(2)
  expect(callsFor('/link-by-email')).toHaveLength(2)
  expect(screen.getAllByText('Return fixture')).toHaveLength(1)
})

it('refreshes committed progress and offers a retry if a later batch fails', async () => {
  let batches = 0
  responses(
    () => 1,
    () => {
      batches++
      if (batches === 2) throw new Error('network failure')
      return { linked: 1, remaining: batches === 1 ? 1 : 0 }
    },
    () => batches > 0 ? [guestOrder] : [],
  )
  renderHistory()

  expect(await screen.findByRole('alert')).toBeTruthy()
  expect(await screen.findByText('Outbound fixture')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: /Try again|Thử lại/i }))

  await waitFor(() => expect(callsFor('/link-by-email')).toHaveLength(3))
  expect(screen.queryByRole('alert')).toBeNull()
})

it('stops before requesting a subsequent batch when the session changes', async () => {
  responses(
    () => 2,
    () => {
      vi.mocked(getToken).mockReturnValue('another-session')
      return { linked: 1, remaining: 1 }
    },
    () => [],
  )
  renderHistory()

  expect(await screen.findByRole('alert')).toBeTruthy()
  expect(callsFor('/link-by-email')).toHaveLength(1)
})

it('exposes incomplete synchronization if the server reports remaining orders with zero progress', async () => {
  responses(() => 1, () => ({ linked: 0, remaining: 1 }), () => [])
  renderHistory()

  expect(await screen.findByRole('alert')).toBeTruthy()
  expect(callsFor('/link-by-email')).toHaveLength(1)
})

it('loads history after a zero count so orders linked by another tab are visible', async () => {
  let available = false
  responses(() => {
    available = true
    return 0
  }, () => {
    throw new Error('No linking needed')
  }, () => available ? [guestOrder] : [])
  renderHistory()

  expect(await screen.findByText('Outbound fixture')).toBeTruthy()
  expect(callsFor('/link-by-email')).toHaveLength(0)
})
