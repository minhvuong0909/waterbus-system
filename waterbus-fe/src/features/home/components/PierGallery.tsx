import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent, PointerEvent } from 'react'
import { Icon } from '../../../components/ui'
import { usePrefersReducedMotion } from '../../../hooks'
import { t } from '../../../i18n'

export interface GallerySlide {
  src: string
  /** Chú thích hiện ở góc ảnh, vd tên bến hoặc "Dọc sông Sài Gòn". */
  caption: string
}

/** Thời gian mỗi ảnh khi tự chạy (ms). */
const AUTOPLAY_MS = 4500
/** Quãng kéo ngang tối thiểu (px) để tính là một cú vuốt. */
const SWIPE_PX = 50

/**
 * Thư viện ảnh của một bến: tự chạy qua từng ảnh (dừng khi rê chuột, đang focus, hoặc tab bị ẩn),
 * hoặc xem thủ công bằng mũi tên, chấm, phím trái/phải và vuốt ngang. Không tự chạy khi bật giảm
 * chuyển động hoặc khi bến này không phải bến đang xem.
 */
export default function PierGallery({
  slides,
  label,
  active,
}: {
  slides: GallerySlide[]
  label: string
  /** Bến này có đang được xem không; chỉ bến đang xem mới tự chạy. */
  active: boolean
}) {
  const reducedMotion = usePrefersReducedMotion()
  const [index, setIndex] = useState(0)
  const [paused, setPaused] = useState(false)
  const [hovered, setHovered] = useState(false)
  const [focused, setFocused] = useState(false)
  const [hidden, setHidden] = useState(() => document.hidden)
  const swipeStart = useRef<number | null>(null)
  const count = slides.length

  const go = (next: number) => setIndex(((next % count) + count) % count)

  useEffect(() => {
    const onVisibility = () => setHidden(document.hidden)
    document.addEventListener('visibilitychange', onVisibility)
    return () => document.removeEventListener('visibilitychange', onVisibility)
  }, [])

  const playing = active && !reducedMotion && !paused && !hovered && !focused && !hidden && count > 1
  useEffect(() => {
    if (!playing) return
    const timer = window.setTimeout(() => setIndex((i) => (i + 1) % count), AUTOPLAY_MS)
    return () => window.clearTimeout(timer)
  }, [playing, index, count])

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'ArrowRight') go(index + 1)
    else if (event.key === 'ArrowLeft') go(index - 1)
    else return
    event.preventDefault()
    event.stopPropagation()
  }

  // Vuốt ảnh không được làm đổi bến (khung ngoài cũng nghe cú vuốt), nên chặn lan lên trên.
  const onPointerDown = (event: PointerEvent) => {
    event.stopPropagation()
    swipeStart.current = event.clientX
  }
  const onPointerUp = (event: PointerEvent) => {
    event.stopPropagation()
    if (swipeStart.current === null) return
    const delta = event.clientX - swipeStart.current
    swipeStart.current = null
    if (Math.abs(delta) >= SWIPE_PX) go(index + (delta < 0 ? 1 : -1))
  }

  return (
    <div
      role="group"
      aria-roledescription="carousel"
      aria-label={label}
      tabIndex={0}
      onKeyDown={onKeyDown}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      onPointerDown={onPointerDown}
      onPointerUp={onPointerUp}
      onPointerCancel={() => {
        swipeStart.current = null
      }}
      className="group/gallery relative h-72 touch-pan-y select-none overflow-hidden rounded-2xl bg-deep-river focus-visible:ring-4 focus-visible:ring-teal-flow/40 sm:h-full sm:min-h-[22rem]"
    >
      <div
        className={`flex h-full ${reducedMotion ? '' : 'transition-transform duration-700 ease-out'}`}
        style={{ transform: `translateX(-${index * 100}%)` }}
      >
        {slides.map((slide, i) => (
          <div
            key={slide.src}
            role="group"
            aria-roledescription="slide"
            aria-label={`${i + 1} / ${count}`}
            aria-hidden={i !== index}
            className="relative h-full w-full shrink-0"
          >
            <img
              src={slide.src}
              alt={`${slide.caption}, ${t('photo {current} of {total}', { current: i + 1, total: count })}`}
              draggable={false}
              loading="lazy"
              className="h-full w-full object-cover"
            />
            <span className="absolute left-3 top-3 rounded-full bg-deep-river/65 px-3 py-1 text-xs font-medium text-white backdrop-blur-sm">
              {slide.caption}
            </span>
          </div>
        ))}
      </div>

      {count > 1 && (
        <>
          <button
            type="button"
            aria-label={t('Previous photo')}
            onClick={() => go(index - 1)}
            className="absolute left-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-deep-river opacity-0 shadow transition-opacity hover:bg-white focus-visible:opacity-100 group-hover/gallery:opacity-100"
          >
            <Icon name="chevron_left" className="text-[22px]" />
          </button>
          <button
            type="button"
            aria-label={t('Next photo')}
            onClick={() => go(index + 1)}
            className="absolute right-3 top-1/2 flex h-10 w-10 -translate-y-1/2 items-center justify-center rounded-full bg-white/85 text-deep-river opacity-0 shadow transition-opacity hover:bg-white focus-visible:opacity-100 group-hover/gallery:opacity-100"
          >
            <Icon name="chevron_right" className="text-[22px]" />
          </button>

          <div className="absolute inset-x-0 bottom-3 flex items-center justify-center gap-2">
            <div className="flex items-center gap-1.5 rounded-full bg-deep-river/55 px-3 py-1.5 backdrop-blur-sm">
              {slides.map((slide, i) => (
                <button
                  key={slide.src}
                  type="button"
                  aria-label={t('Photo {n}', { n: i + 1 })}
                  aria-current={i === index ? 'true' : undefined}
                  onClick={() => go(i)}
                  className={`relative h-2 rounded-full transition-all before:absolute before:-inset-x-[3px] before:-inset-y-3 before:content-[''] ${
                    i === index ? 'w-6 bg-white' : 'w-2 bg-white/60 hover:bg-white/85'
                  }`}
                />
              ))}
              {!reducedMotion && (
                <button
                  type="button"
                  aria-label={t(paused ? 'Play slideshow' : 'Pause slideshow')}
                  aria-pressed={paused}
                  onClick={() => setPaused((v) => !v)}
                  className="relative ml-1 flex h-5 w-5 items-center justify-center rounded-full text-white before:absolute before:-inset-2 before:content-[''] hover:bg-white/20"
                >
                  <Icon name={paused ? 'play_arrow' : 'pause'} className="text-[16px]" />
                </button>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
