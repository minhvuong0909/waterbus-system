import { useEffect, useState } from 'react'

/** The current time in ms, refreshed every `ms`, so a component can compare against "now" purely. */
export function useNow(ms: number): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), ms)
    return () => window.clearInterval(timer)
  }, [ms])
  return now
}
