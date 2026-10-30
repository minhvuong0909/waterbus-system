import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../../services/apiClient'
import { t } from '../../../i18n'

/** A counter to pass as a `useFetch` dependency; calling `reload` refetches and keeps old data on screen. */
export function useReload(): [number, () => void] {
  const [version, setVersion] = useState(0)
  return [version, useCallback(() => setVersion((v) => v + 1), [])]
}

/** Text to show for a failed action: the server's message, or a generic one when it is unreachable. */
export function errorText(error: unknown): string {
  if (error instanceof ApiError && error.status !== 0) {
    const fields = Object.values(error.fieldErrors).flat()
    return fields.length > 0 ? fields.join(' ') : error.message
  }
  return t('Something went wrong. Please check your connection and try again.')
}

/** Runs an async action while tracking whether it is busy and why it failed. */
export function useSubmit() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  /** Resolves with the action's result, or undefined when it failed (the message is in `error`). */
  const run = useCallback(async <T,>(action: () => Promise<T>): Promise<T | undefined> => {
    setBusy(true)
    setError(null)
    try {
      return await action()
    } catch (e) {
      setError(errorText(e))
      return undefined
    } finally {
      setBusy(false)
    }
  }, [])

  return { run, busy, error, clearError: () => setError(null) }
}

/** The current time in ms, refreshed every `ms`, so a component can compare against "now" purely. */
export function useNow(ms: number): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), ms)
    return () => window.clearInterval(timer)
  }, [ms])
  return now
}

