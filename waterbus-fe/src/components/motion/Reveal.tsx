import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'

interface RevealProps {
  children: ReactNode
  className?: string
}

/** Hiện từng khối một lần bằng API trình duyệt, không giữ trình xử lý cuộn liên tục. */
export default function Reveal({ children, className }: RevealProps) {
  const scope = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (
      !scope.current ||
      typeof window.IntersectionObserver !== 'function' ||
      !Element.prototype.animate
    )
      return
    const media = window.matchMedia('(prefers-reduced-motion: reduce)')
    const animations = new Set<Animation>()
    const revealed = new WeakSet<Element>()
    const targets = scope.current.querySelectorAll('[data-reveal]')
    const observer = new IntersectionObserver(
      (entries) => {
        entries
          .filter((entry) => entry.isIntersecting)
          .forEach((entry, index) => {
          if (revealed.has(entry.target)) return
            observer.unobserve(entry.target)
            revealed.add(entry.target)
            if (media.matches || entry.boundingClientRect.top < 0) return
            const animation = entry.target.animate(
              [
                { opacity: 0, transform: 'translateY(18px)' },
                { opacity: 1, transform: 'translateY(0)' },
              ],
              {
                duration: 460,
                delay: Math.min(index, 3) * 45,
                easing: 'cubic-bezier(.2,.7,.2,1)',
                fill: 'backwards',
              },
            )
            animations.add(animation)
            animation.onfinish = animation.oncancel = () => {
              animations.delete(animation)
            }
          })
      },
      { rootMargin: '0px 0px -32px 0px', threshold: 0.08 },
    )
    const configure = () => {
      observer.disconnect()
      animations.forEach((animation) => animation.cancel())
      if (!media.matches)
        targets.forEach((target) => {
          if (!revealed.has(target)) observer.observe(target)
        })
    }
    configure()
    media.addEventListener('change', configure)
    return () => {
      observer.disconnect()
      media.removeEventListener('change', configure)
      animations.forEach((animation) => animation.cancel())
    }
  }, [])

  return (
    <div ref={scope} className={className}>
      {children}
    </div>
  )
}
