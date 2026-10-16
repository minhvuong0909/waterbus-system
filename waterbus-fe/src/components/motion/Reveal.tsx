import { useRef } from 'react'
import type { ReactNode } from 'react'
import { gsap } from 'gsap'
import { ScrollTrigger } from 'gsap/ScrollTrigger'
import { useGSAP } from '@gsap/react'

gsap.registerPlugin(useGSAP, ScrollTrigger)

interface RevealProps {
  children: ReactNode
  className?: string
}

/**
 * Fades up every descendant marked `data-reveal` the first time it scrolls into view.
 * Siblings that enter together are staggered. Nothing is animated when the user prefers reduced
 * motion, so the content is simply there. GSAP only runs on the client (inside `useGSAP`) and
 * reverts itself on unmount.
 */
export default function Reveal({ children, className }: RevealProps) {
  const scope = useRef<HTMLDivElement>(null)

  useGSAP(
    () => {
      const mm = gsap.matchMedia()
      mm.add('(prefers-reduced-motion: no-preference)', () => {
        gsap.set('[data-reveal]', { autoAlpha: 0, y: 28 })
        ScrollTrigger.batch('[data-reveal]', {
          start: 'top 88%',
          once: true,
          onEnter: (items) =>
            gsap.to(items, {
              autoAlpha: 1,
              y: 0,
              duration: 0.7,
              ease: 'power3.out',
              stagger: 0.12,
              overwrite: true,
            }),
        })
      })
    },
    { scope },
  )

  return (
    <div ref={scope} className={className}>
      {children}
    </div>
  )
}
