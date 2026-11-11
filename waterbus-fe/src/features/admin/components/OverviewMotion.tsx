import { useRef } from 'react'
import type { ReactNode } from 'react'
import { gsap } from 'gsap'
import { useGSAP } from '@gsap/react'

gsap.registerPlugin(useGSAP)

/** Hiệu ứng ngắn chỉ ở Tổng quan; dữ liệu đồng bộ không kích hoạt lại animation. */
export default function OverviewMotion({ children }: { children: ReactNode }) {
  const scope = useRef<HTMLDivElement>(null)
  useGSAP(
    () => {
      const media = gsap.matchMedia()
      media.add('(prefers-reduced-motion: no-preference)', () => {
        gsap.fromTo(
          '[data-admin-enter]',
          { y: 10, opacity: 0.7 },
          {
            y: 0,
            opacity: 1,
            duration: 0.32,
            stagger: 0.045,
            ease: 'power2.out',
            clearProps: 'transform,opacity',
          },
        )
      })
      return () => media.revert()
    },
    { scope },
  )
  return <div ref={scope}>{children}</div>
}
