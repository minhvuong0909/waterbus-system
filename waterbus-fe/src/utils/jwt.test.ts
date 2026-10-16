import { describe, expect, it } from 'vitest'
import { getTokenExpiry, isTokenExpired } from './jwt'

const encode = (payload: object) => `h.${btoa(JSON.stringify(payload))}.s`

describe('jwt', () => {
  it('reads the exp claim in milliseconds', () => {
    expect(getTokenExpiry(encode({ exp: 1000 }))).toBe(1_000_000)
  })

  it('treats an unreadable token as valid', () => {
    expect(getTokenExpiry('not-a-token')).toBeNull()
    expect(isTokenExpired('not-a-token')).toBe(false)
  })

  it('detects an expired token and the exact boundary', () => {
    const token = encode({ exp: 1000 })
    expect(isTokenExpired(token, 999_999)).toBe(false)
    expect(isTokenExpired(token, 1_000_000)).toBe(true)
  })
})
