import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../../components/ui'
import { usePrefersReducedMotion } from '../../../hooks'
import { t } from '../../../i18n'
import { SECTION } from '../constants'
import type { Landmark } from '../types'
import SightseeingCta from './SightseeingCta'

const BACKGROUND_ROTATION_INTERVAL = 7000

export default function ExploreHero({ landmarks }: { landmarks: Landmark[] }) {
  const heroRef = useRef<HTMLElement>(null)
  const reducedMotion = usePrefersReducedMotion()
  const slides = landmarks.filter(
    (landmark): landmark is Landmark & { image: string } => Boolean(landmark.image),
  )
  const [activeIndex, setActiveIndex] = useState(0)
  const [isPlaying, setIsPlaying] = useState(!reducedMotion)
  const [isHovered, setIsHovered] = useState(false)
  const [isFocused, setIsFocused] = useState(false)
  const [isVisible, setIsVisible] = useState(true)
  const [isDocumentVisible, setIsDocumentVisible] = useState(() => !document.hidden)
  const activeSlide = slides[activeIndex]

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

  useEffect(() => {
    if (
      !isPlaying ||
      reducedMotion ||
      isHovered ||
      isFocused ||
      !isVisible ||
      !isDocumentVisible ||
      slides.length < 2
    ) {
      return
    }

    const timer = window.setInterval(
      () => setActiveIndex((index) => (index + 1) % slides.length),
      BACKGROUND_ROTATION_INTERVAL,
    )
    return () => window.clearInterval(timer)
  }, [isDocumentVisible, isFocused, isHovered, isPlaying, isVisible, reducedMotion, slides.length])

  return (
    <section
      ref={heroRef}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      onFocusCapture={() => setIsFocused(true)}
      onBlurCapture={(event) => {
        const nextTarget = event.relatedTarget
        if (!(nextTarget instanceof Node) || !event.currentTarget.contains(nextTarget)) {
          setIsFocused(false)
        }
      }}
      className="relative isolate flex min-h-[min(740px,calc(100svh-5rem))] overflow-hidden bg-deep-river text-on-primary"
    >
      <div aria-hidden="true" className="absolute inset-0 bg-deep-river">
        {slides.map((slide, index) => (
          <img
            key={slide.id}
            src={slide.image}
            alt=""
            aria-hidden="true"
            loading="eager"
            className={`absolute inset-0 h-full w-full object-cover object-center transition-opacity duration-1000 ease-in-out motion-reduce:transition-none ${
              index === activeIndex ? 'opacity-100' : 'opacity-0'
            }`}
          />
        ))}
      </div>
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-r from-deep-river/95 via-deep-river/80 to-deep-river/25"
      />
      <div
        aria-hidden="true"
        className="absolute inset-0 bg-gradient-to-t from-deep-river/60 via-transparent to-deep-river/20"
      />

      <div className={`${SECTION} relative flex w-full flex-col justify-center py-20 pr-20 sm:pr-0 md:py-28`}>
        <div className="explore-hero__content max-w-3xl">
          <div className="flex items-center gap-3 text-xs font-semibold uppercase tracking-[0.22em] text-sand-light">
            <span className="h-px w-9 bg-sky-aqua" />
            {t('Discover Saigon from the river')}
          </div>
          {activeSlide ? (
            <div
              key={activeSlide.id}
              className="mt-6 animate-hero-copy-enter motion-reduce:animate-none"
            >
              <div className="mb-4 flex items-center gap-3 text-xs font-semibold uppercase tracking-[0.16em]">
                <span className="rounded-full border border-white/20 bg-white/10 px-3 py-1.5 text-sand-light backdrop-blur-sm">
                  {t(activeSlide.category)}
                </span>
                <span className="font-mono text-white/60">
                  {String(activeIndex + 1).padStart(2, '0')} / {String(slides.length).padStart(2, '0')}
                </span>
              </div>
              <h1 className="max-w-3xl font-display text-4xl font-bold leading-[1.08] tracking-[-0.04em] text-white sm:text-5xl md:text-6xl lg:text-7xl">
                {activeSlide.title}
              </h1>
              <p className="mt-5 line-clamp-3 max-w-xl text-base leading-7 text-white/85 sm:text-lg sm:leading-8">
                {activeSlide.description}
              </p>
            </div>
          ) : (
            <>
              <h1 className="mt-6 max-w-3xl font-display text-4xl font-bold leading-[1.08] tracking-[-0.04em] text-white sm:text-5xl md:text-6xl lg:text-7xl">
                {t('See the City Differently from the River')}
              </h1>
              <p className="mt-6 max-w-xl text-base leading-7 text-white/85 sm:text-lg sm:leading-8">
                {t('Sightseeing trips on the Saigon River, with stories played over the boat’s speakers.')}
              </p>
            </>
          )}
          <div className="mt-8 flex flex-wrap items-center gap-3">
            <SightseeingCta />
            <a
              href="#river-landmarks"
              className="group inline-flex min-h-12 items-center gap-2 rounded-full border border-white/40 bg-white/10 px-5 text-sm font-semibold text-white backdrop-blur-sm transition-colors hover:border-white/70 hover:bg-white/20"
            >
              {t('Explore landmarks')}
              <Icon name="arrow_downward" className="text-lg transition-transform group-hover:translate-y-0.5 motion-reduce:transform-none" />
            </a>
          </div>
        </div>
      </div>
      {activeSlide && (
        <div
          role="group"
          aria-label={t('Explore background photos')}
          className="absolute right-3 top-1/2 z-10 flex -translate-y-1/2 flex-col items-center rounded-full border border-white/10 bg-deep-river/20 px-1 py-2 backdrop-blur-sm sm:right-8"
        >
          {slides.map((slide, index) => (
            <button
              key={slide.id}
              type="button"
              aria-label={t('Show background for {title}', { title: slide.title })}
              aria-pressed={index === activeIndex}
              onClick={() => setActiveIndex(index)}
              className="grid h-11 w-11 place-items-center rounded-full transition-colors hover:bg-white/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-white motion-reduce:transition-none"
            >
              <span
                className={`rounded-full transition-[background-color,height,width,transform] duration-300 motion-reduce:transition-none ${
                  index === activeIndex
                    ? 'h-5 w-1.5 bg-white shadow-[0_0_10px_rgba(255,255,255,0.65)]'
                    : 'h-1.5 w-1.5 bg-white/40 hover:bg-white/75'
                }`}
              />
            </button>
          ))}
          {!reducedMotion && slides.length > 1 && (
            <>
              <span aria-hidden="true" className="my-1 h-px w-4 bg-white/20" />
              <button
                type="button"
                aria-label={t(isPlaying ? 'Pause background rotation' : 'Resume background rotation')}
                onClick={() => setIsPlaying((playing) => !playing)}
                className="grid h-11 w-11 place-items-center rounded-full text-white/75 transition-colors hover:bg-white/10 hover:text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-white motion-reduce:transition-none"
              >
                <Icon name={isPlaying ? 'pause' : 'play_arrow'} className="text-lg" />
              </button>
            </>
          )}
        </div>
      )}
    </section>
  )
}
