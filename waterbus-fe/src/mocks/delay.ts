import { localize } from '../i18n'

// Lets you see and test the error screens without a backend: open any page with `?mockError=1`
// (e.g. http://localhost:5173/tickets?mockError=1). Every mock request made in the first
// N seconds after the page loads fails; clicking "Try again" afterwards succeeds.
const loadedAt = Date.now()

function failureWindowMs(): number {
  const raw = new URLSearchParams(window.location.search).get('mockError')
  if (raw === null) return 0
  const seconds = parseFloat(raw)
  return (Number.isFinite(seconds) && seconds > 1 ? seconds : 2) * 1000
}

const windowMs = failureWindowMs()

/** Resolves with `value` after a short delay, mimicking network latency (or fails on demand). */
export function withDelay<T>(value: T, ms = 150): Promise<T> {
  return new Promise((resolve, reject) => {
    setTimeout(() => {
      if (Date.now() - loadedAt < windowMs) reject(new Error('Mock network error'))
      else resolve(localize(value))
    }, ms)
  })
}
