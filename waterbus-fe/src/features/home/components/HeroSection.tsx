import { Link } from 'react-router-dom'
import { useEffect, useRef, useState } from 'react'
import heroVideo from '../../../assets/videos/saigon-waterbus.mp4'
import heroPoster from '../../../assets/videos/saigon-waterbus-poster.jpg'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'
import { usePrefersReducedMotion } from '../../../hooks'
import HeroSearch from './HeroSearch'

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
    <div className="relative bg-mist xl:pb-11">
      <section className="relative flex min-h-[480px] w-full flex-col justify-end bg-deep-river sm:min-h-[560px] lg:h-[calc(100svh-5rem-4.5rem)] lg:max-h-[780px] lg:min-h-[540px]">
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
          <div className="absolute inset-0 bg-gradient-to-r from-deep-river/55 via-deep-river/15 to-transparent lg:w-[60%]" />
          <div className="absolute inset-0 bg-gradient-to-t from-deep-river/60 via-transparent to-transparent" />
        </div>

        <div className="relative z-10 mx-auto w-full max-w-7xl px-5 pb-10 sm:px-8 xl:pb-20">
          <div className="flex flex-wrap items-end justify-between gap-space-md">
            <h1 className="max-w-[640px] font-headline-xl text-[30px] font-extrabold leading-[1.15] tracking-tight text-white drop-shadow-[0_2px_16px_rgba(13,37,56,0.55)] sm:text-4xl lg:text-[46px]">
              {t('A Smarter Journey,')}
              <br />
              <span className="text-sky-aqua">{t('A Brighter Saigon')}</span>
            </h1>
            <Link
              to={ROUTES.liveTracking}
              className="inline-flex min-h-[44px] items-center gap-2 rounded-full border border-white/40 bg-deep-river/40 px-4 py-2 text-sm font-semibold text-white backdrop-blur-sm transition-colors hover:bg-deep-river/70"
            >
              <Icon name="near_me" className="text-[18px]" />
              {t('Live Tracking')}
            </Link>
          </div>
        </div>

        {!reducedMotion && (
          <button
            type="button"
            aria-label={t(paused ? 'Play background video' : 'Pause background video')}
            aria-pressed={paused}
            onClick={() => setPaused(!paused)}
            className="absolute right-5 top-5 z-10 flex h-11 w-11 items-center justify-center rounded-full border border-white/30 bg-deep-river/60 text-white transition-colors hover:bg-deep-river sm:right-8"
          >
            <Icon name={paused ? 'play_arrow' : 'pause'} className="text-[22px]" />
          </button>
        )}
      </section>

      <div className="relative z-20 mx-auto -mt-6 w-full max-w-6xl px-5 pb-6 sm:px-8 xl:absolute xl:inset-x-0 xl:bottom-11 xl:mt-0 xl:pb-0 xl:translate-y-1/2">
        <HeroSearch />
      </div>
    </div>
  )
}
