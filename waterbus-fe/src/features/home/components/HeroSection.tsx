import { Link } from 'react-router-dom'
import { useEffect, useRef, useState } from 'react'
import heroVideo from '../../../assets/videos/saigon-waterbus.mp4'
import heroPoster from '../../../assets/videos/saigon-waterbus-poster.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'
import { usePrefersReducedMotion } from '../../../hooks'

export default function HeroSection() {
  // With reduced motion the poster stays still instead of a looping video.
  const reducedMotion = usePrefersReducedMotion()
  const [paused, setPaused] = useState(false)
  const videoRef = useRef<HTMLVideoElement>(null)
  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    let visible = false
    const updatePlayback = () => {
      if (visible && !document.hidden && !reducedMotion && !paused) {
        void video.play().catch(() => {
          /* Giữ ảnh bìa nếu trình duyệt chặn tự phát. */
        })
      } else video.pause()
    }
    const observer = new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting
      updatePlayback()
    })
    observer.observe(video)
    document.addEventListener('visibilitychange', updatePlayback)
    return () => {
      observer.disconnect()
      document.removeEventListener('visibilitychange', updatePlayback)
      video.pause()
    }
  }, [reducedMotion, paused])
  return (
    <section className="relative flex min-h-[520px] w-full flex-col justify-center overflow-hidden bg-deep-river sm:min-h-[600px] lg:h-[calc(100svh-5rem)] lg:max-h-[820px] lg:min-h-[600px]">
      <div className="pointer-events-none absolute inset-0 z-0 overflow-hidden">
        <video
          ref={videoRef}
          aria-label={t(
            'Smart Waterbus electric catamaran gliding across the Saigon River with the city skyline',
          )}
          className="h-full w-full object-cover object-[62%_center] saturate-[1.08] lg:object-[68%_center]"
          src={heroVideo}
          poster={heroPoster}
          muted
          loop={!reducedMotion}
          playsInline
          preload="metadata"
        />
        <div className="absolute inset-0 bg-gradient-to-r from-deep-river/85 via-deep-river/55 to-transparent lg:w-[72%]" />
        <div className="absolute inset-0 bg-gradient-to-t from-deep-river/85 via-transparent to-deep-river/25" />
      </div>

      <div className="relative z-10 mx-auto w-full max-w-7xl px-5 py-16 sm:px-8 lg:py-20">
        <div className="max-w-3xl space-y-space-md">
          <div className="relative space-y-1">
            <h1 className="max-w-[780px] font-headline-xl text-[32px] font-extrabold leading-[1.16] tracking-tight text-white drop-shadow-[0_2px_16px_rgba(13,37,56,0.45)] sm:text-5xl lg:text-[60px]">
              {t('A Smarter Journey,')}
              <br />
              <span className="text-sky-aqua">{t('A Brighter Saigon')}</span>
            </h1>
          </div>
          <p className="max-w-xl pt-1 text-base leading-relaxed text-white/90 drop-shadow-[0_1px_8px_rgba(13,37,56,0.5)] sm:text-body-lg">
            {t(
              'Explore the city by water with real-time updates, seamless booking and unforgettable experiences.',
            )}
          </p>
          <div className="flex flex-wrap items-center gap-4 pt-3">
            <Link
              to={ROUTES.search}
              className="group inline-flex items-center justify-center gap-2.5 rounded-xl bg-teal-flow px-7 py-3.5 font-headline-sm text-base font-semibold text-on-primary shadow-[0_12px_24px_-8px_rgba(20,122,126,0.42)] transition-all hover:-translate-y-0.5 hover:bg-secondary hover:shadow-[0_16px_32px_-6px_rgba(20,122,126,0.55)]"
            >
              <span>{t('Explore Routes')}</span>
              <Icon
                name="arrow_forward"
                className="text-[20px] transition-transform group-hover:translate-x-1"
              />
            </Link>
            <Link
              to={ROUTES.liveTracking}
              className="inline-flex min-h-[48px] items-center gap-2 rounded-xl border border-white/30 bg-white/5 px-5 py-3 text-sm font-semibold text-white transition-colors hover:bg-white/15"
            >
              <Icon name="near_me" className="text-[18px]" />
              {t('Live Tracking')}
            </Link>
          </div>
        </div>
      </div>
      {!reducedMotion && (
        <button
          type="button"
          aria-label={t(paused ? 'Play background video' : 'Pause background video')}
          aria-pressed={paused}
          onClick={() => setPaused(!paused)}
          className="absolute bottom-5 left-5 z-10 flex h-11 w-11 items-center justify-center rounded-full border border-white/30 bg-deep-river/70 text-white transition-colors hover:bg-deep-river sm:left-8"
        >
          <Icon name={paused ? 'play_arrow' : 'pause'} className="text-[22px]" />
        </button>
      )}
    </section>
  )
}
