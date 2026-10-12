import { useCallback, useEffect, useState } from 'react'

export interface FetchState<T> {
  data: T | undefined
  loading: boolean
  error: Error | undefined
  /** Runs the loader again, e.g. from a "Try again" button. */
  retry: () => void
}

/** Runs an async loader on mount (and on every `retry`) and tracks its state. */
export function useFetch<T>(loader: () => Promise<T>): FetchState<T> {
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<Omit<FetchState<T>, 'retry'>>({
    data: undefined,
    loading: true,
    error: undefined,
  })

  useEffect(() => {
    let cancelled = false
    loader()
      .then((data) => !cancelled && setState({ data, loading: false, error: undefined }))
      .catch((error: Error) => !cancelled && setState({ data: undefined, loading: false, error }))
    return () => {
      cancelled = true
    }
    // The loader is re-created every render by callers; only `attempt` should re-run it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attempt])

  const retry = useCallback(() => {
    setState({ data: undefined, loading: true, error: undefined })
    setAttempt((n) => n + 1)
  }, [])

  return { ...state, retry }
}

interface Combined {
  loading: boolean
  error: Error | undefined
  /** Re-runs only the requests that failed. */
  retry: () => void
}

/** Merges several fetches into one loading/error/retry for a page that needs all of them. */
export function combineFetches(...fetches: FetchState<unknown>[]): Combined {
  return {
    loading: fetches.some((f) => f.loading),
    error: fetches.find((f) => f.error)?.error,
    retry: () => fetches.forEach((f) => f.error && f.retry()),
  }
}
