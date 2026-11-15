import { useEffect, useRef, useState } from 'react'
import { usePrefersReducedMotion } from '../../../hooks'
import { t } from '../../../i18n'
import { SECTION } from '../constants'
import type { Landmark } from '../types'
import SightseeingCta from './SightseeingCta'

/** How long each landmark stays on screen. The progress bar of the active tab runs for exactly this long. */
const SLIDE_MS = 3000
const WORD_DELAY_MS = 45

/** Title whose words rise into place one after another, each from behind its own mask. */
function RunningTitle({ text, className }: { text: string; className: string }) {
  return (
    <h1 className={className}>
      {text.split(' ').map((word, index) => (
        <span key={index}>
          <span className="inline-block overflow-hidden pb-[0.12em] align-bottom">
            <span
              className="inline-block animate-word-rise motion-reduce:animate-none"
              style={{ animationDelay: `${80 + index * WORD_DELAY_MS}ms` }}
            >
              {word}
            </span>
          </span>{' '}
        </span>
      ))}
    </h1>
  )
}

interface ExploreHeroProps {
  landmarks: Landmark[]
  /** Landmark to open on, e.g. the one picked on the home page. The first one when missing or unknown. */
  startId?: string
}

export default function ExploreHero({ landmarks, startId }: ExploreHeroProps) {
  const heroRef = useRef<HTMLElement>(null)
  const reducedMotion = usePrefersReducedMotion()
  const slides = landmarks.filter(
    (landmark): landmark is Landmark & { image: string } => Boolean(landmark.image),
  )
  // The photo that was on screen stays in view while it fades out, so it keeps its zoom instead of snapping back.
  const [view, setView] = useState(() => ({
    active: Math.max(0, slides.findIndex((slide) => slide.id === startId)),
    previous: -1,
  }))
  const [isHovered, setIsHovered] = useState(false)
  const [isFocused, setIsFocused] = useState(false)
  const [isVisible, setIsVisible] = useState(true)
  const [isDocumentVisible, setIsDocumentVisible] = useState(() => !document.hidden)
  const activeSlide = slides[view.active]
  const autoPlay = !reducedMotion && slides.length > 1
  const paused = isHovered || isFocused || !isVisible || !isDocumentVisible

  const show = (index: number) => setView((current) => ({ active: index, previous: current.active }))
  const showNext = () =>
    setView((current) => ({ active: (current.active + 1) % slides.length, previous: current.active }))

  useEffect(() => {
    const hero = heroRef.current
    if (!hero || typeof IntersectionObserver === 'undefined') return

    const observer = new IntersectionObserver(([entry]) => setIsVisible(entry.isIntersecting), {
      threshold: 0.1,
    })
    observer.observe(hero)
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    const updateVisibility = () => setIsDocumentVisible(!document.hidden)
    document.addEventListener('visibilitychange', updateVisibility)
    return () => document.removeEventListener('visibilitychange', updateVisibility)
  }, [])

  return (
    <section
      ref={heroRef}
      // Chỉ phím Tab mới dừng slide; bấm chuột vào một thẻ không làm thanh tiến trình đứng lại.
      onFocusCapture={(event) => setIsFocused(event.target.matches(':focus-visible'))}
      onBlurCapture={(event) => {
        const nextTarget = event.relatedTarget
        if (!(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
          setIsFocused(false)
        }
      }}
      className="relative isolate flex min-h-[min(760px,calc(100svh-5rem))] overflow-hidden bg-deep-river text-on-primary"
    >
      <div aria-hidden="true" className="absolute inset-0 bg-deep-river">
        {slides.map((slide, index) => (
          <img
            key={slide.id}
            src={slide.image}
            alt=""
            aria-hidden="true"
            loading="eager"
            className={`absolute inset-0 h-full w-full object-cover object-center transition-opacity duration-700 ease-in-out motion-reduce:animate-none motion-reduce:transition-none ${
              index === view.active ? 'opacity-100' : 'opacity-0'
            } ${index === view.active || index === view.previous ? 'animate-ken-burns' : ''}`}
          />
        ))}
      </div>
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-r from-deep-river/90 via-deep-river/60 to-deep-river/10"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-t from-deep-river/80 via-transparent to-deep-river/20"
      />

      <div className={`${SECTION} relative flex w-full flex-col justify-center pb-36 pt-16 md:pb-40 md:pt-24`}>
        {/* Rê chuột lên phần chữ thì dừng để khách đọc; rê xuống thanh thẻ thì không. */}
        <div
          className="max-w-3xl"
          onMouseEnter={() => setIsHovered(true)}
          onMouseLeave={() => setIsHovered(false)}
        >
          <div className="flex items-center gap-3 text-xs font-semibold uppercase tracking-[0.22em] text-sand-light">
            <span className="h-px w-9 bg-sky-aqua" />
            {t('Discover Saigon from the river')}
          </div>
          {activeSlide ? (
            <div key={activeSlide.id} className="mt-6">
              <p className="mb-5 inline-block animate-hero-copy-enter rounded-full border border-white/25 bg-white/10 px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-sand-light backdrop-blur-sm motion-reduce:animate-none">
                {t(activeSlide.category)}
              </p>
              {/* Both blocks keep the room of the longest slide, so the buttons below never jump. */}
              <RunningTitle
                text={activeSlide.title}
                className="min-h-[3.3em] max-w-3xl font-display text-4xl font-bold leading-[1.06] tracking-tight text-white [text-wrap:balance] sm:min-h-[2.2em] sm:text-5xl md:text-6xl"
              />
              <p
                style={{ animationDelay: `${120 + activeSlide.title.split(' ').length * WORD_DELAY_MS}ms` }}
                className="mt-5 line-clamp-3 min-h-[5.25rem] max-w-xl animate-hero-copy-enter text-base leading-7 text-white/85 motion-reduce:animate-none sm:min-h-[6rem] sm:text-lg sm:leading-8"
              >
                {activeSlide.description}
              </p>
            </div>
          ) : (
            <>
              <h1 className="mt-6 max-w-3xl font-display text-4xl font-bold leading-[1.06] tracking-tight text-white [text-wrap:balance] sm:text-5xl md:text-6xl">
                {t('See the City Differently from the River')}
              </h1>
              <p className="mt-6 max-w-xl text-base leading-7 text-white/85 sm:text-lg sm:leading-8">
                {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
              </p>
            </>
          )}
          <div className="mt-6 flex">
            <SightseeingCta />
          </div>
        </div>
      </div>

      {activeSlide && (
        <div className="absolute inset-x-0 bottom-0 z-10 pb-4 sm:pb-6">
          <div className={`${SECTION} flex items-end gap-3 max-sm:pr-20`}>
            <div
              role="group"
              aria-label={t('Explore background photos')}
              className="grid flex-1 grid-flow-col auto-cols-fr gap-2 sm:gap-4"
            >
              {slides.map((slide, index) => {
                const isActive = index === view.active
                return (
                  <button
                    key={slide.id}
                    type="button"
                    aria-label={t('Show background for {title}', { title: slide.title })}
                    aria-pressed={isActive}
                    onClick={() => show(index)}
                    className="group min-w-0 py-2 text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white"
                  >
                    <span className="block h-0.5 overflow-hidden rounded-full bg-white/25">
                      {isActive && (
                        <span
                          data-hero-progress
                          onAnimationEnd={autoPlay ? showNext : undefined}
                          style={{
                            animationDuration: `${SLIDE_MS}ms`,
                            animationPlayState: paused ? 'paused' : 'running',
                          }}
                          className={`block h-full origin-left bg-white ${autoPlay ? 'animate-hero-progress' : ''}`}
                        />
                      )}
                    </span>
                    <span
                      className={`mt-3 hidden items-baseline gap-2 transition-colors duration-300 sm:flex ${
                        isActive ? 'text-white' : 'text-white/55 group-hover:text-white/85'
                      }`}
                    >
                      <span className="font-mono text-xs">{String(index + 1).padStart(2, '0')}</span>
                      <span className="truncate text-sm font-medium">{slide.title}</span>
                    </span>
                  </button>
                )
              })}
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
