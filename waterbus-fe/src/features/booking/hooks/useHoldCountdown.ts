import { useEffect, useState } from 'react'
import { useAppSelector } from '../../../store/hooks'

/** Time left on the seat hold of the current booking. `hold` is null until a booking exists. */
export function useHoldCountdown() {
  const hold = useAppSelector((s) => s.booking.hold)
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!hold) return
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [hold])

  const remainingSeconds = hold ? Math.max(0, Math.ceil((hold.expiresAt - now) / 1000)) : 0
  return { hold, remainingSeconds, expired: hold !== null && remainingSeconds === 0 }
}
