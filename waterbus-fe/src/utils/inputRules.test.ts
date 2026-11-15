import { describe, expect, it } from 'vitest'
import { codeTaken, hasLetter, isAccountPhone, isCode, isEmail, isNewPassword, isPersonName, hasSaneYear, parseDecimal, parseSignedDecimal, parseWhole } from './inputRules'

describe('parseWhole', () => {
  it.each(['', ' ', '1e3', '0x10', 'Infinity', '-1', '+5', '1.5', '1,5', 'abc', '١٢', '9'.repeat(30)])(
    'rejects %j',
    (value) => expect(parseWhole(value, 0, 1_000_000)).toBeNull(),
  )
  it('accepts a plain whole number inside the range only', () => {
    expect(parseWhole(' 45 ', 1, 1440)).toBe(45)
    expect(parseWhole('0', 1, 1440)).toBeNull()
    expect(parseWhole('1441', 1, 1440)).toBeNull()
  })
})

describe('parseDecimal', () => {
  it('limits the decimals and refuses thousands separators, exponents and signs', () => {
    expect(parseDecimal('25000', 0, 1e9, 2)).toBe(25000)
    expect(parseDecimal('12.5', 0, 1000, 2)).toBe(12.5)
    expect(parseDecimal('12.505', 0, 1000, 2)).toBeNull()
    expect(parseDecimal('100.000', 0, 1e9, 2)).toBeNull()
    expect(parseDecimal('1e3', 0, 1e9, 2)).toBeNull()
    expect(parseDecimal('-1', 0, 1e9, 2)).toBeNull()
    expect(parseDecimal('1,5', 0, 1e9, 2)).toBeNull()
    expect(parseDecimal('', 0, 1e9, 2)).toBeNull()
  })
})

describe('parseSignedDecimal', () => {
  it('reads coordinates', () => {
    expect(parseSignedDecimal('-10.7769', -90, 90, 7)).toBe(-10.7769)
    expect(parseSignedDecimal('91', -90, 90, 7)).toBeNull()
    expect(parseSignedDecimal('10.12345678', -90, 90, 7)).toBeNull()
    expect(parseSignedDecimal('-', -90, 90, 7)).toBeNull()
  })
})

describe('codes', () => {
  it('accepts letters, digits, dot, underscore and hyphen only', () => {
    expect(isCode('SWB-02')).toBe(true)
    expect(isCode('ST 06')).toBe(false)
    expect(isCode('Bến1')).toBe(false)
    expect(isCode('A/B')).toBe(false)
    expect(isCode('  ')).toBe(false)
  })
  it('finds a taken code regardless of case and spaces', () => {
    expect(codeTaken(' st01 ', [{ code: 'ST01' }])).toBe(true)
    expect(codeTaken('ST02', [{ code: 'ST01' }])).toBe(false)
  })
})

describe('text rules', () => {
  it('needs a letter in a name', () => {
    expect(hasLetter('Nguyễn Văn A')).toBe(true)
    expect(hasLetter('12345')).toBe(false)
    expect(hasLetter('@@ !!')).toBe(false)
  })
  it('keeps dates in a sane year range', () => {
    expect(hasSaneYear('2026-11-20')).toBe(true)
    expect(hasSaneYear('0001-01-01')).toBe(false)
    expect(hasSaneYear('2999-01-01T10:00')).toBe(false)
    expect(hasSaneYear('')).toBe(false)
  })
})

describe('account fields (same rules as the backend)', () => {
  it('checks emails', () => {
    expect(isEmail('an@example.com')).toBe(true)
    expect(isEmail('an@example')).toBe(false)
    expect(isEmail('an @example.com')).toBe(false)
    expect(isEmail('@@.')).toBe(false)
    expect(isEmail(`${'a'.repeat(146)}@x.vn`)).toBe(false)
  })
  it('checks names', () => {
    expect(isPersonName('Nguyễn Thị B')).toBe(true)
    expect(isPersonName('   ')).toBe(false)
    expect(isPersonName('007')).toBe(false)
    expect(isPersonName('A'.repeat(151))).toBe(false)
  })
  it('checks phones like the backend pattern', () => {
    expect(isAccountPhone('+84 908-123-456')).toBe(true)
    expect(isAccountPhone('(028) 3822 5555')).toBe(true)
    expect(isAccountPhone('12345')).toBe(false)
    expect(isAccountPhone('abc12345678')).toBe(false)
    expect(isAccountPhone('1'.repeat(21))).toBe(false)
    expect(isAccountPhone('+ - ( ) ')).toBe(false)
  })
  it('checks new passwords like Identity', () => {
    expect(isNewPassword('Abcdef12')).toBe(true)
    expect(isNewPassword('abcdef12')).toBe(false)
    expect(isNewPassword('ABCDEF12')).toBe(false)
    expect(isNewPassword('Abcdefgh')).toBe(false)
    expect(isNewPassword('Ab1')).toBe(false)
    expect(isNewPassword(`Ab1${'x'.repeat(100)}`)).toBe(false)
  })
})
