import { beforeEach, describe, expect, it } from 'vitest'
import type { AuthSession } from '../features/authentication/types'
import { clearSession, getToken, loadSession, saveSession } from './authStorage'

const future = Math.floor(Date.now() / 1000) + 3600
const past = Math.floor(Date.now() / 1000) - 3600
const tokenWith = (exp: number) => `h.${btoa(JSON.stringify({ exp }))}.s`
const sessionWith = (token: string) => ({ token } as unknown as AuthSession)

beforeEach(() => {
  window.localStorage.clear()
  window.sessionStorage.clear()
})

describe('authStorage', () => {
  it('keeps a remembered session in localStorage', () => {
    saveSession(sessionWith(tokenWith(future)), true)
    expect(window.localStorage.length).toBe(1)
    expect(window.sessionStorage.length).toBe(0)
    expect(getToken()).toBe(tokenWith(future))
  })

  it('keeps a non-remembered session in sessionStorage only', () => {
    saveSession(sessionWith(tokenWith(future)), false)
    expect(window.sessionStorage.length).toBe(1)
    expect(window.localStorage.length).toBe(0)
  })

  it('drops an expired session on load', () => {
    saveSession(sessionWith(tokenWith(past)), true)
    expect(loadSession()).toBeNull()
    expect(window.localStorage.length).toBe(0)
  })

  it('ignores corrupt stored data', () => {
    window.localStorage.setItem('waterbus.session', '{not json')
    expect(loadSession()).toBeNull()
    expect(getToken()).toBeNull()
  })

  it('clears every store', () => {
    saveSession(sessionWith(tokenWith(future)), true)
    clearSession()
    expect(getToken()).toBeNull()
  })
})
