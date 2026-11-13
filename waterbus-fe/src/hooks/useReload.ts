import { useCallback, useState } from 'react'

/** A counter to pass as a `useFetch` dependency; calling `reload` refetches and keeps old data on screen. */
export function useReload(): [number, () => void] {
  const [version, setVersion] = useState(0)
  return [version, useCallback(() => setVersion((v) => v + 1), [])]
}
