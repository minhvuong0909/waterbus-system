import { describe, expect, it } from 'vitest'
import { previewTimeBudget } from './timeBudget'

const base = { segmentCount: 4, dwellTimeSeconds: 6, nearThreshold: 0.75 }

describe('previewTimeBudget', () => {
  it('accepts the default configuration', () => {
    const budget = previewTimeBudget({ ...base, tripDurationSeconds: 180 })

    expect(budget.isValid).toBe(true)
    expect(budget.violations).toHaveLength(0)
    expect(budget.maxNarrationSeconds).toBeGreaterThan(0)
  })

  it('rejects a trip shorter than the audio it must carry', () => {
    const budget = previewTimeBudget({ ...base, tripDurationSeconds: 30, dwellTimeSeconds: 5 })

    expect(budget.isValid).toBe(false)
    expect(budget.maxNarrationSeconds).toBe(0)
    expect(budget.violations.map((v) => v.property)).toContain('tripDurationSeconds')
  })

  it('rejects a dwell time too short for the arrived announcement', () => {
    const budget = previewTimeBudget({ ...base, tripDurationSeconds: 180, dwellTimeSeconds: 1 })

    expect(budget.isValid).toBe(false)
    expect(budget.violations.map((v) => v.property)).toContain('dwellTimeSeconds')
  })

  it('never lets the narration exceed the departing window', () => {
    const budget = previewTimeBudget({ ...base, tripDurationSeconds: 180 })

    expect(budget.maxNarrationSeconds).toBeLessThanOrEqual(budget.departingWindowSeconds)
    expect(budget.maxNarrationCharacters).toBe(
      Math.floor(budget.maxNarrationSeconds * 14),
    )
  })

  it('gives a longer trip more room for narration until the policy cap applies', () => {
    const short = previewTimeBudget({ ...base, tripDurationSeconds: 120, nearThreshold: 0.5 })
    const long = previewTimeBudget({ ...base, tripDurationSeconds: 240, nearThreshold: 0.5 })

    expect(long.maxNarrationSeconds).toBeGreaterThan(short.maxNarrationSeconds)
    expect(long.maxNarrationSeconds).toBeLessThanOrEqual(10)
  })

  it('keeps the near announcement inside the remaining window', () => {
    const budget = previewTimeBudget({ ...base, tripDurationSeconds: 180 })

    expect(budget.nearWindowSeconds).toBeGreaterThanOrEqual(4)
  })
})
