import { useEffect, useState } from 'react'
import { syncGuestOrders } from '../services/orderAccessService'

export function useGuestOrderSync(token: string | null) {
  const [attempt, setAttempt] = useState(0)
  const [result, setResult] = useState<{ attempt: number; failed: boolean }>()

  useEffect(() => {
    if (!token) return
    let active = true
    const settle = (failed: boolean) => {
      if (active) setResult({ attempt, failed })
    }

    void syncGuestOrders(token).then(
      () => settle(false),
      () => settle(true),
    )
    return () => { active = false }
  }, [attempt, token])

  return {
    pending: !!token && result?.attempt !== attempt,
    failed: result?.attempt === attempt && result.failed,
    completedAttempt: result?.attempt,
    retry: () => setAttempt((previous) => previous + 1),
  }
}
