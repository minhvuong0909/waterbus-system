import { useEffect, useRef, useState } from 'react'
import { usePrefersReducedMotion } from '../../hooks'

/** Số nguyên, có thể có dấu phân cách hàng nghìn (`15.000`, `1,200`). Số thập phân thì không đếm. */
const INTEGER = /\d+(?:[.,]\d{3})*(?![.,]?\d)/

const group = (n: number, separator: string) =>
  separator ? String(n).replace(/\B(?=(\d{3})+(?!\d))/g, separator) : String(n)

interface CountUpProps {
  /** Chuỗi hiển thị cuối cùng, vd "Từ 15.000 VND"; số đầu tiên trong chuỗi đếm lên một lần. */
  value: string
  durationMs?: number
}

/**
 * Đếm số đầu tiên trong `value` từ 0 lên giá trị thật một lần khi lọt vào màn hình. Chỗ cho chữ được giữ đúng cỡ cuối
 * nên bố cục không nhúc nhích; tắt chuyển động hoặc không có số thì hiện nguyên chuỗi.
 */
export default function CountUp({ value, durationMs = 800 }: CountUpProps) {
  const reducedMotion = usePrefersReducedMotion()
  const root = useRef<HTMLSpanElement>(null)
  const match = INTEGER.exec(value)
  const [shown, setShown] = useState<string | null>(null)

  useEffect(() => {
    const el = root.current
    if (!el || !match || reducedMotion || typeof IntersectionObserver !== 'function') return
    const target = Number(match[0].replace(/[.,]/g, ''))
    const separator = /[.,]/.exec(match[0])?.[0] ?? ''
    let frame = 0
    const run = () => {
      const start = performance.now()
      const tick = (now: number) => {
        const progress = Math.min((now - start) / durationMs, 1)
        const eased = 1 - Math.pow(1 - progress, 3)
        const text = value.replace(match[0], group(Math.round(target * eased), separator))
        setShown(progress < 1 ? text : null)
        if (progress < 1) frame = requestAnimationFrame(tick)
      }
      frame = requestAnimationFrame(tick)
    }
    const observer = new IntersectionObserver(([entry]) => {
      if (!entry.isIntersecting) return
      observer.disconnect()
      run()
    })
    observer.observe(el)
    return () => {
      observer.disconnect()
      cancelAnimationFrame(frame)
    }
    // `match` được tính lại từ `value` mỗi lần render nên chỉ theo dõi `value`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, durationMs, reducedMotion])

  return (
    <span ref={root} className="relative inline-block tabular-nums">
      <span className={shown === null ? '' : 'invisible'}>{value}</span>
      {shown !== null && (
        <span aria-hidden className="absolute inset-0 whitespace-nowrap">
          {shown}
        </span>
      )}
    </span>
  )
}
