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
    const play = (
      target: Element,
      keyframes: Keyframe[],
      options: KeyframeAnimationOptions,
    ) => {
      const animation = target.animate(keyframes, { ...options, fill: 'both' })
      animations.add(animation)
      animation.onfinish = () => {
        animation.cancel()
        animations.delete(animation)
      }
      animation.oncancel = () => animations.delete(animation)
    }
    const observer = new IntersectionObserver(
      (entries) => {
        entries
          .filter((entry) => entry.isIntersecting)
          .forEach((entry, index) => {
            if (revealed.has(entry.target)) return
            observer.unobserve(entry.target)
            revealed.add(entry.target)
            if (media.matches) return

            const isLandmarkReveal = entry.target.getAttribute('data-reveal') === 'landmark'
            const mediaTarget = entry.target.querySelector('[data-reveal-part="media"]')
            const copyTarget = entry.target.querySelector('[data-reveal-part="copy"]')
            if (isLandmarkReveal && mediaTarget && copyTarget) {
              const startsFromRight =
                entry.target.getAttribute('data-reveal-direction') === 'right'
              play(
                mediaTarget,
                [
                  {
                    opacity: 0.45,
                    clipPath: startsFromRight ? 'inset(0 0 0 100%)' : 'inset(0 100% 0 0)',
                    transform: 'scale(1.06)',
                  },
                  { opacity: 1, clipPath: 'inset(0 0 0 0)', transform: 'scale(1)' },
                ],
                { duration: 980, easing: 'cubic-bezier(.16,1,.3,1)' },
              )
              play(
                copyTarget,
                [
                  { opacity: 0, transform: 'translateY(48px)' },
                  { opacity: 1, transform: 'translateY(0)' },
                ],
                { duration: 760, delay: 170, easing: 'cubic-bezier(.16,1,.3,1)' },
              )
              return
            }

            const distance = isLandmarkReveal ? 36 : 32
            play(
              entry.target,
              [
                { opacity: 0, transform: `translateY(${distance}px)` },
                { opacity: 1, transform: 'translateY(0)' },
              ],
              {
                duration: isLandmarkReveal ? 760 : 620,
                delay: Math.min(index, 3) * 65,
                easing: 'cubic-bezier(.16,1,.3,1)',
              },
            )
          })
      },
      { rootMargin: '0px 0px -48px 0px', threshold: 0.12 },
    )
    const configure = () => {
      observer.disconnect()
      animations.forEach((animation) => animation.cancel())
      animations.clear()
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
