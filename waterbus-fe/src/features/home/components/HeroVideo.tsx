import { useEffect, useRef } from 'react'
import heroVideo from '../../../assets/videos/saigon-waterbus.mp4'
import { usePrefersReducedMotion } from '../../../hooks'
import { t } from '../../../i18n'

/** The clip opens with a title card and a caption; playback starts and loops after them. */
const START_SECONDS = 5.5

/**
 * The river video filling the hero, behind the headline. It only plays while it is on screen and the
 * tab is visible; with reduced motion it stays on its first river frame. Two gradients keep the text
 * on the left and the search bar at the bottom readable whatever the frame shows.
 */
export default function HeroVideo() {
  const reducedMotion = usePrefersReducedMotion()
  const videoRef = useRef<HTMLVideoElement>(null)

  useEffect(() => {
    const video = videoRef.current
    if (!video) return
    let visible = false
    const updatePlayback = () => {
      if (visible && !document.hidden && !reducedMotion) {
        void video.play().catch(() => {
          /* Giữ khung hình đầu nếu trình duyệt chặn tự phát. */
        })
      } else video.pause()
    }
    // `loop` would go back to the title card, so the end of the clip returns to the start point instead.
    const restart = () => {
      video.currentTime = START_SECONDS
      updatePlayback()
    }
    const observer = new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting
      updatePlayback()
    })
    observer.observe(video)
    video.addEventListener('ended', restart)
    document.addEventListener('visibilitychange', updatePlayback)
    return () => {
      observer.disconnect()
      video.removeEventListener('ended', restart)
      document.removeEventListener('visibilitychange', updatePlayback)
      video.pause()
    }
  }, [reducedMotion])

  return (
    <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
      <video
        ref={videoRef}
        aria-label={t('Smart Waterbus electric catamaran gliding across the Saigon River with the city skyline')}
        className="h-full w-full object-cover"
        src={`${heroVideo}#t=${START_SECONDS}`}
        muted
        playsInline
        preload="metadata"
      />
      {/* On a phone the text covers the whole frame, so the shade is even instead of fading to the right. */}
      <div className="absolute inset-0 bg-deep-river/70 lg:bg-transparent lg:bg-gradient-to-r lg:from-deep-river/90 lg:via-deep-river/55 lg:to-deep-river/10" />
      <div className="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-deep-river/80 to-transparent" />
    </div>
  )
}
