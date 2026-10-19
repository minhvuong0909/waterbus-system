import { useEffect, useRef, useState } from 'react'
import { usePrefersReducedMotion } from '../../../hooks'
import { t } from '../../../i18n'

const SECTIONS = [
  { id: 'top', label: 'Find trips' },
  { id: 'departures', label: 'Upcoming departures' },
  { id: 'how-it-works', label: 'How It Works' },
  { id: 'tours', label: 'Sightseeing' },
  { id: 'piers', label: 'Pier network' },
  { id: 'map', label: 'River corridor map' },
]

/**
 * Thanh tiến độ cuộn mảnh dưới header và (từ màn hình rộng) chấm điều hướng các khối của trang chủ.
 * Khối đang xem lấy từ IntersectionObserver; thanh tiến độ cập nhật theo khung hình, không render lại.
 */
export default function SectionNav() {
  const reducedMotion = usePrefersReducedMotion()
  const [active, setActive] = useState(SECTIONS[0].id)
  const bar = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) setActive(entry.target.id)
        })
      },
      { rootMargin: '-45% 0px -50% 0px' },
    )
    SECTIONS.forEach(({ id }) => {
      const el = document.getElementById(id)
      if (el) observer.observe(el)
    })
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    let frame = 0
    const update = () => {
      frame = 0
      const max = document.documentElement.scrollHeight - window.innerHeight
      const progress = max > 0 ? Math.min(1, Math.max(0, window.scrollY / max)) : 0
      if (bar.current) bar.current.style.transform = `scaleX(${progress})`
    }
    const onScroll = () => {
      if (!frame) frame = requestAnimationFrame(update)
    }
    update()
    window.addEventListener('scroll', onScroll, { passive: true })
    window.addEventListener('resize', onScroll)
    return () => {
      window.removeEventListener('scroll', onScroll)
      window.removeEventListener('resize', onScroll)
      if (frame) cancelAnimationFrame(frame)
    }
  }, [])

  const goTo = (id: string) => {
    const behavior = reducedMotion ? 'auto' : 'smooth'
    if (id === 'top') window.scrollTo({ top: 0, behavior })
    else document.getElementById(id)?.scrollIntoView({ behavior, block: 'start' })
  }

  return (
    <>
      <div aria-hidden="true" className="pointer-events-none fixed inset-x-0 top-16 z-[45] h-[3px] sm:top-20">
        <div
          ref={bar}
          className="h-full origin-left bg-gradient-to-r from-teal-flow to-sky-aqua"
          style={{ transform: 'scaleX(0)' }}
        />
      </div>

      <nav
        aria-label={t('Page sections')}
        className="fixed right-5 top-1/2 z-40 hidden -translate-y-1/2 flex-col gap-1 rounded-full bg-white/80 px-1.5 py-2 shadow-[0_6px_24px_rgba(13,37,56,0.15)] backdrop-blur-md xl:flex"
      >
        {SECTIONS.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            onClick={() => goTo(id)}
            aria-label={t(label)}
            aria-current={active === id ? 'true' : undefined}
            className="group relative flex h-7 w-7 items-center justify-center"
          >
            <span
              className={`rounded-full transition-all duration-300 ${
                active === id ? 'h-3 w-3 bg-teal-flow' : 'h-2 w-2 bg-outline-variant group-hover:bg-teal-flow/60'
              }`}
            />
            <span className="pointer-events-none absolute right-9 whitespace-nowrap rounded-md bg-deep-river px-2.5 py-1 text-xs font-medium text-white opacity-0 shadow transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
              {t(label)}
            </span>
          </button>
        ))}
      </nav>
    </>
  )
}
