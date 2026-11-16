import { describe, expect, it } from 'vitest'
import { isEmail, isPhone } from './personnel'

describe('personnel form rules', () => {
  it('accepts phone numbers the backend accepts', () => {
    expect(isPhone('0901234567')).toBe(true)
    expect(isPhone('+84 (90) 123-4567')).toBe(true)
  })
  it('rejects phone numbers the backend rejects', () => {
    expect(isPhone('12345')).toBe(false)
    expect(isPhone('09012abc67')).toBe(false)
    expect(isPhone('')).toBe(false)
  })
  it('requires a plausible email', () => {
    expect(isEmail(' an@waterbus.vn ')).toBe(true)
    expect(isEmail('an@waterbus')).toBe(false)
    expect(isEmail('an waterbus.vn')).toBe(false)
  })
})
