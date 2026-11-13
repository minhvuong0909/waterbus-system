import { useEffect, useRef } from 'react'

/** Đồng bộ REST định kỳ khi đang xem trang; không chồng yêu cầu và tải lại khi có mạng. */
export function useAutoRefresh(refresh: () => void, busy: boolean, intervalMs = 60_000) {
  const current = useRef({ refresh, busy })
  useEffect(() => { current.current = { refresh, busy } })
  useEffect(() => {
    const update = () => {
      if (document.visibilityState !== 'hidden' && navigator.onLine && !current.current.busy) current.current.refresh()
    }
    const timer = window.setInterval(update, intervalMs)
    window.addEventListener('online', update)
    document.addEventListener('visibilitychange', update)
    return () => {
      window.clearInterval(timer)
      window.removeEventListener('online', update)
      document.removeEventListener('visibilitychange', update)
    }
  }, [intervalMs])
}
