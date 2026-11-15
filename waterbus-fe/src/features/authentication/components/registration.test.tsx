import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import SignUpForm from './SignUpForm'
import VerifyEmailPage from '../../../pages/auth/VerifyEmailPage'
import { confirmEmail, register, resendVerification } from '../services/registrationService'

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
const accepted = () => json({ message: 'ok' })

const body = (call: number) => JSON.parse(fetchMock.mock.calls[call][1].body)

describe('registration service', () => {
  it('sends the form without a role and turns an empty phone into null', async () => {
    fetchMock.mockResolvedValue(accepted())

    await register({ fullName: ' Nguyen A ', email: ' a@gmail.com ', phoneNumber: ' ', password: 'Strong1Pass' })

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/v1\/auth\/register$/)
    expect(body(0)).toEqual({ email: 'a@gmail.com', password: 'Strong1Pass', fullName: 'Nguyen A', phoneNumber: null })
    expect(body(0)).not.toHaveProperty('role')
  })

  it('posts the uid and the token to confirm, and the email to resend', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 })).mockResolvedValueOnce(accepted())

    await confirmEmail('u1', 'tok')
    await resendVerification(' a@gmail.com ')

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/auth\/confirm-email$/)
    expect(body(0)).toEqual({ userId: 'u1', token: 'tok' })
    expect(fetchMock.mock.calls[1][0]).toMatch(/\/auth\/resend-verification$/)
    expect(body(1)).toEqual({ email: 'a@gmail.com' })
  })
})

describe('sign-up form', () => {
  const renderForm = () => render(<MemoryRouter><SignUpForm /></MemoryRouter>)
  const fill = (password = 'Strong1Pass', confirm = 'Strong1Pass') => {
    fireEvent.change(screen.getByLabelText(/Full name|Họ và tên/i), { target: { value: 'Nguyen A' } })
    fireEvent.change(screen.getByLabelText(/^Email address|^Địa chỉ email/i), { target: { value: 'a@gmail.com' } })
    fireEvent.change(screen.getByLabelText(/^Password|^Mật khẩu$/i), { target: { value: password } })
    fireEvent.change(screen.getByLabelText(/Confirm password|Xác nhận mật khẩu/i), { target: { value: confirm } })
  }
  const submit = () => fireEvent.click(screen.getByRole('button', { name: /Create account|Tạo tài khoản/i }))

  it('does not call the backend when the two passwords differ', async () => {
    renderForm()
    fill('Strong1Pass', 'Different1Pass')

    submit()

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('does not call the backend for a password shorter than 8 characters', async () => {
    renderForm()
    fill('Ab1', 'Ab1')

    submit()

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('shows the same "check your email" screen after a successful request', async () => {
    fetchMock.mockResolvedValue(accepted())
    renderForm()
    fill()

    submit()

    expect(await screen.findByText(/Check your email|Kiểm tra email của bạn/i)).toBeTruthy()
  })

  it('shows the first message the backend gives for an invalid field', async () => {
    fetchMock.mockResolvedValue(json({ title: 'x', status: 400, detail: 'x', errors: { Password: ['Mật khẩu phải có chữ hoa.'] } }, 400))
    renderForm()
    fill()

    submit()

    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('Mật khẩu phải có chữ hoa.'))
  })
})

describe('verify email page', () => {
  const renderAt = (url: string) =>
    render(
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/verify-email" element={<VerifyEmailPage />} />
        </Routes>
      </MemoryRouter>,
    )

  it('confirms the link once and shows success', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))
    renderAt('/verify-email?uid=u1&token=tok')

    expect(await screen.findByText(/Email confirmed|Đã xác minh email/i)).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(body(0)).toEqual({ userId: 'u1', token: 'tok' })
  })

  it('offers to send a new link when the link is invalid', async () => {
    fetchMock.mockResolvedValue(json({ title: 'x', status: 400, detail: 'x' }, 400))
    renderAt('/verify-email?uid=u1&token=bad')

    expect(await screen.findByText(/invalid or has expired|không hợp lệ hoặc đã hết hạn/i)).toBeTruthy()
    expect(screen.getByRole('button', { name: /Send verification email|Gửi email xác minh/i })).toBeTruthy()
  })

  it('without uid and token only offers to resend and calls nothing', () => {
    renderAt('/verify-email')

    expect(screen.getByRole('button', { name: /Send verification email|Gửi email xác minh/i })).toBeTruthy()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
