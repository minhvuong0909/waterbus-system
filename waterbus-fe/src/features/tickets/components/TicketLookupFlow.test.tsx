import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import TicketLookupFlow from './TicketLookupFlow'

const fetchMock = vi.fn()
beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  cleanup()
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

const BOOKING = {
  bookings: [
    {
      bookingCode: 'WB20261114101530ABCDEF',
      routeName: 'Bach Dang - Linh Dong',
      departureTime: '2026-11-15T01:00:00Z',
      arrivalTime: '2026-11-15T02:00:00Z',
      tripStatus: 'Scheduled',
      boardingStation: 'Bach Dang',
      disembarkingStation: 'Linh Dong',
      bookingStatus: 'Confirmed',
      tickets: [
        { ticketCode: 'TK1', passengerName: 'Nguyen Van A', seatCode: 'A1', seatClass: 'Standard', ticketStatus: 'Valid', boardingStatus: 'NotCheckedIn', serviceEntitled: true },
      ],
      qr: { payload: 'signed.qr.payload' },
    },
  ],
}

async function fillDetails() {
  fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'buyer@example.com' } })
  fireEvent.change(screen.getByLabelText(/Booking code|Mã đặt chỗ/i), { target: { value: 'wb20261114101530abcdef' } })
}

describe('ticket lookup flow', () => {
  it('asks for a code, then shows tickets and the QR once the code is accepted', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
      .mockResolvedValueOnce(json(BOOKING))
    render(<TicketLookupFlow />)

    await fillDetails()
    fireEvent.click(screen.getByRole('button', { name: /Send me a code|Gửi mã cho tôi/i }))
    const otpInput = await screen.findByLabelText(/6-digit code|Mã 6 chữ số/i)
    fireEvent.change(otpInput, { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: /View my tickets|Xem vé của tôi/i }))

    expect(await screen.findByText('Nguyen Van A')).toBeTruthy()
    expect(screen.getByRole('img', { name: /WB20261114101530ABCDEF/ })).toBeTruthy()

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/v1\/tickets\/lookup\/otp$/)
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({ email: 'buyer@example.com', bookingCode: 'WB20261114101530ABCDEF' })
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/api\/v1\/tickets\/lookup\/verify$/)
    expect(JSON.parse(fetchMock.mock.calls[1][1].body).otp).toBe('123456')
  })

  it('shows one generic message for a wrong or expired code and keeps the tickets hidden', async () => {
    fetchMock
      .mockResolvedValueOnce(json({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
      .mockResolvedValueOnce(json({ status: 403, title: 'Forbidden', detail: 'x' }, 403))
    render(<TicketLookupFlow />)

    await fillDetails()
    fireEvent.click(screen.getByRole('button', { name: /Send me a code|Gửi mã cho tôi/i }))
    fireEvent.change(await screen.findByLabelText(/6-digit code|Mã 6 chữ số/i), { target: { value: '000000' } })
    fireEvent.click(screen.getByRole('button', { name: /View my tickets|Xem vé của tôi/i }))

    await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy())
    expect(screen.queryByText('Nguyen Van A')).toBeNull()
  })

  it('accepts only digits in the code field and limits it to six', async () => {
    fetchMock.mockResolvedValueOnce(json({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
    render(<TicketLookupFlow />)

    await fillDetails()
    fireEvent.click(screen.getByRole('button', { name: /Send me a code|Gửi mã cho tôi/i }))
    const otpInput = (await screen.findByLabelText(/6-digit code|Mã 6 chữ số/i)) as HTMLInputElement
    fireEvent.change(otpInput, { target: { value: '12ab34567' } })

    expect(otpInput.value).toBe('123456')
  })

  it('says the same thing whether or not the details matched a booking', async () => {
    fetchMock.mockResolvedValue(json({ expiresInSeconds: 300, resendAfterSeconds: 60 }))
    render(<TicketLookupFlow />)

    await fillDetails()
    fireEvent.click(screen.getByRole('button', { name: /Send me a code|Gửi mã cho tôi/i }))

    expect(await screen.findByText(/If the details match|Nếu thông tin khớp/i)).toBeTruthy()
  })
})
