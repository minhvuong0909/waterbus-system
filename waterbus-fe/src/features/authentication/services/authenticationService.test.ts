import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../services/apiClient'
import { signIn } from './authenticationService'
import { ROUTES } from '../../../routes/routes'

const fetchMock = vi.fn()
beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
})
afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

const payload = { email: 'admin@waterbus.vn', password: 'Secret123', remember: true }

describe('staff sign-in', () => {
  it('posts to /auth/login and returns the session of the account the backend accepted', async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({ userId: 'u1', email: 'admin@waterbus.vn', fullName: 'Admin', roles: ['Admin'], token: 'jwt' }),
      ),
    )

    const session = await signIn(payload)

    expect(fetchMock.mock.calls[0][0]).toMatch(/\/api\/v1\/auth\/login$/)
    expect(session).toEqual({
      token: 'jwt',
      user: { id: 'u1', fullName: 'Admin', email: 'admin@waterbus.vn', roles: ['Admin'] },
    })
  })

  it('lets the backend decide which roles may sign in (no role is rejected in the browser)', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ userId: 'u2', email: 'x@waterbus.vn', fullName: 'X', roles: ['Passenger'], token: 'jwt' })),
    )

    await expect(signIn(payload)).resolves.toMatchObject({ token: 'jwt' })
  })

  it('rejects with ApiError 401 when the backend refuses the account', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ title: 'Unauthorized', status: 401, detail: 'Email hoặc mật khẩu không chính xác.' }), {
        status: 401,
        headers: { 'Content-Type': 'application/problem+json' },
      }),
    )

    await expect(signIn(payload)).rejects.toMatchObject({ status: 401 })
    await expect(signIn(payload)).rejects.toBeInstanceOf(ApiError)
  })

  it('serves the sign-in page at a private path, not the old public /sign-in', () => {
    expect(ROUTES.signIn).toBe('/staff-login')
  })
})
