import { useCallback, useEffect, useState } from 'react'

export interface FetchState<T> {
  data: T | undefined
  /** True until the first result (or error) arrives; stays false while a refetch runs. */
  loading: boolean
  /** True whenever a request is in flight, including refetches that keep the old `data`. */
  fetching: boolean
  error: Error | undefined
  /** Runs the loader again, e.g. from a "Try again" button. */
  retry: () => void
}

/**
 * Runs an async loader on mount, on every `retry`, and whenever a value in `deps` changes.
 * After a `deps` change the previous `data` stays available until the new result arrives.
 */
export function useFetch<T>(
  loader: () => Promise<T>,
  deps: readonly unknown[] = [],
): FetchState<T> {
  // `request` numbers the loads; a result only counts once it carries the current number.
  const [request, setRequest] = useState(0)
  const [result, setResult] = useState<{ data?: T; error?: Error; request: number }>({
    request: -1,
  })

  // Start a new load during render when `deps` change, instead of setting state in an effect.
  const [lastDeps, setLastDeps] = useState(deps)
  if (lastDeps.length !== deps.length || lastDeps.some((dep, i) => !Object.is(dep, deps[i]))) {
    setLastDeps(deps)
    setRequest((n) => n + 1)
  }

  useEffect(() => {
    let cancelled = false
    loader()
      .then((data) => !cancelled && setResult({ data, request }))
      .catch((error: Error) => !cancelled && setResult({ error, request }))
    return () => {
      cancelled = true
    }
    // The loader is re-created every render by callers; only a new `request` re-runs it.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [request])

  const retry = useCallback(() => {
    setResult({ request: -1 })
    setRequest((n) => n + 1)
  }, [])

  const fetching = result.request !== request
  return {
    data: result.data,
    loading: fetching && result.data === undefined,
    fetching,
    error: fetching ? undefined : result.error,
    retry,
  }
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
