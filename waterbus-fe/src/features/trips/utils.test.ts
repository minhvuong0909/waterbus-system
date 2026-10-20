import { describe, expect, it } from 'vitest'
import { hasFare } from './utils'

describe('hasFare', () => {
  it('treats 0 as "not known yet", because the search endpoint sends no fare', () => {
    expect(hasFare(0)).toBe(false)
  })

  it('accepts a real fare', () => {
    expect(hasFare(15000)).toBe(true)
  })

  it('does not accept a negative amount', () => {
    expect(hasFare(-1)).toBe(false)
  })
})
