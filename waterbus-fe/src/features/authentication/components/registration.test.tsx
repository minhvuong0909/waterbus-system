import { StrictMode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import SignUpForm from './SignUpForm'
import VerifyEmailPage from '../../../pages/auth/VerifyEmailPage'
import { resendVerification } from '../services/registrationService'

const fetchMock = vi.fn()
beforeEach(() => { vi.stubGlobal('fetch', fetchMock) })
afterEach(() => {
  cleanup()
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const body = (call: number) => JSON.parse(fetchMock.mock.calls[call][1].body)

it('signs up without asking for a password and tells the customer to check their email', async () => {
  fetchMock.mockResolvedValue(json({ message: 'ok' }))
  render(<MemoryRouter><SignUpForm /></MemoryRouter>)
  fireEvent.change(screen.getByLabelText(/Full name|Họ và tên/i), { target: { value: 'Nguyen A' } })
  fireEvent.change(screen.getByLabelText(/^Email address|^Địa chỉ email/i), { target: { value: 'a@gmail.com' } })
  expect(screen.queryByLabelText(/^Password$|^Mật khẩu$/i)).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: /Create account|Tạo tài khoản/i }))

  expect(await screen.findByText(/Check your email|Kiểm tra email của bạn/i)).toBeTruthy()
  expect(fetchMock.mock.calls[0][0]).toMatch(/\/auth\/register$/)
  expect(body(0)).toEqual({ email: 'a@gmail.com', fullName: 'Nguyen A', phoneNumber: null })
})

it('sends only the trimmed email when asking for a new link', async () => {
  fetchMock.mockResolvedValue(json({ message: 'ok' }))
  await resendVerification(' a@gmail.com ')

  expect(fetchMock.mock.calls[0][0]).toMatch(/\/auth\/resend-verification$/)
  expect(body(0)).toEqual({ email: 'a@gmail.com' })
})

describe('verify email page', () => {
  function LocationProbe() {
    const location = useLocation()
    return <output data-testid="url">{location.pathname + location.search + location.hash}</output>
  }

  const renderAt = (url: string) =>
    render(
      <StrictMode>
        <MemoryRouter initialEntries={[url]}>
          <LocationProbe />
          <Routes>
            <Route path="/verify-email" element={<VerifyEmailPage />} />
          </Routes>
        </MemoryRouter>
      </StrictMode>,
    )

  const activate = (password = 'Strong1Pass', confirm = password) => {
    fireEvent.change(screen.getByLabelText(/^Password$|^Mật khẩu$/i), { target: { value: password } })
    fireEvent.change(screen.getByLabelText(/Confirm password|Xác nhận mật khẩu/i), { target: { value: confirm } })
    fireEvent.click(screen.getByRole('button', { name: /Activate account|Kích hoạt tài khoản/i }))
  }

  it.each(['#', '?'])('scrubs the %s link and only posts after the recipient enters a password', async (separator) => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    renderAt(`/verify-email${separator}uid=u1&token=tok`)
    await waitFor(() => expect(screen.getByTestId('url').textContent).toBe('/verify-email'))
    expect(fetchMock).not.toHaveBeenCalled()

    activate()

    expect(await screen.findByText(/Email confirmed|Đã xác minh email/i)).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(fetchMock.mock.calls[0][0]).toMatch(/\/auth\/confirm-email$/)
    expect(body(0)).toEqual({ userId: 'u1', token: 'tok', password: 'Strong1Pass' })
  })

  it('keeps the activation form after a password error and allows correction', async () => {
    fetchMock.mockResolvedValueOnce(json({ errors: { password: ['Choose a stronger password.'] } }, 400))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    renderAt('/verify-email#uid=u1&token=tok')

    activate('Common1Pass')
    expect((await screen.findByRole('alert')).textContent).toContain('Choose a stronger password.')
    activate()
    expect(await screen.findByText(/Email confirmed|Đã xác minh email/i)).toBeTruthy()
  })

  it.each(['Weak', 'alllowercase1', 'ALLUPPERCASE1', 'NoDigitsHere'])('refuses the weak password %j without sending', async (weak) => {
    renderAt('/verify-email#uid=u1&token=tok')
    activate(weak)

    expect((await screen.findByRole('alert')).textContent).toMatch(/at least 8 characters|tối thiểu 8 ký tự/)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('rejects a password mismatch before sending anything', async () => {
    renderAt('/verify-email#uid=u1&token=tok')
    activate('Strong1Pass', 'Different1Pass')

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('offers to send a new link when the link is rejected', async () => {
    fetchMock.mockResolvedValue(json({ errors: { token: ['invalid'] } }, 400))
    renderAt('/verify-email#uid=u1&token=bad')

    activate()

    expect(await screen.findByText(/invalid or has expired|không hợp lệ hoặc đã hết hạn/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: /Send verification email|Gửi email xác minh/i })).toBeTruthy()
  })

  it('without uid and token only offers to resend and calls nothing', () => {
    renderAt('/verify-email')

    expect(screen.getByRole('button', { name: /Send verification email|Gửi email xác minh/i })).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
