import { useEffect, useRef } from 'react'
import heroVideo from '../../../assets/videos/saigon-waterbus.mp4'
import heroPoster from '../../../assets/videos/saigon-waterbus-poster.jpg'
import { t } from '../../../i18n'
import { usePrefersReducedMotion } from '../../../hooks'
import HeroSearch from './HeroSearch'

export default function HeroSection() {
  // With reduced motion the poster stays still instead of a looping video.
  const reducedMotion = usePrefersReducedMotion()
  const videoRef = useRef<HTMLVideoElement>(null)
  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    let visible = false
    const updatePlayback = () => {
      if (visible && !document.hidden && !reducedMotion) {
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
  }, [reducedMotion])
  return (
    <div id="top" className="relative bg-mist xl:pb-11">
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
          <div className="absolute inset-0 bg-[radial-gradient(ellipse_75%_65%_at_0%_100%,rgba(13,37,56,0.78),rgba(13,37,56,0.35)_55%,transparent_80%)]" />
          <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-deep-river/45 to-transparent" />
        </div>

        <div className="relative z-10 mx-auto w-full max-w-7xl px-5 pb-10 sm:px-8 xl:pb-20">
          <div className="flex flex-wrap items-end justify-between gap-space-md">
            <div className="relative max-w-[900px] sm:pl-5">
              <span
                aria-hidden="true"
                className="absolute bottom-1 left-0 top-9 hidden w-1 rounded-full bg-gradient-to-b from-sky-aqua to-teal-flow sm:block"
              />
              <p className="mb-3 inline-flex items-center gap-2 rounded-full border border-white/30 bg-deep-river/40 px-3.5 py-1.5 text-[11px] font-semibold uppercase tracking-[0.18em] text-white/95 backdrop-blur-sm">
                <span className="h-1.5 w-1.5 rounded-full bg-sky-aqua" />
                {t('Saigon River Waterbus')}
              </p>
              <h1 className="font-headline-xl text-[34px] font-extrabold leading-[1.08] tracking-[-0.02em] drop-shadow-[0_3px_18px_rgba(13,37,56,0.55)] sm:text-5xl lg:text-[54px] xl:text-[58px]">
                <span className="block text-white">{t('A Smarter Journey,')}</span>
                <span className="block bg-gradient-to-r from-sky-aqua via-[#bfeff7] to-white bg-clip-text text-transparent">
                  {t('A Brighter Saigon')}
                </span>
              </h1>
            </div>
          </div>
        </div>

      </section>

      <div className="relative z-20 mx-auto -mt-6 w-full max-w-6xl px-5 pb-6 sm:px-8 xl:absolute xl:inset-x-0 xl:bottom-11 xl:mt-0 xl:pb-0 xl:translate-y-1/2">
        <HeroSearch />
      </div>
    </div>
  )
}
