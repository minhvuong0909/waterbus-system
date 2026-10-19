import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { usePrefersReducedMotion } from '../../../hooks'
import { ROUTES } from '../../../routes/routes'
import { formatVnd } from '../../../utils/format'
import type { Story } from '../types'
import { t } from '../../../i18n'

/** Chiều cao thanh header cố định (h-20), phần đứng yên của khung ảnh dính ngay dưới nó. */
const HEADER_PX = 80
/** Mỗi tour chiếm 80% chiều cao màn hình khi cuộn. */
const SEGMENT_VH = 80

/**
 * Tổng quan các hành trình tham quan theo kiểu "cuộn qua từng khung ảnh": một khung toàn màn hình
 * đứng yên, cuộn trang thì ảnh, tiêu đề và giá đổi sang tour kế tiếp; danh sách bên phải cho thấy
 * đang ở tour nào và bấm để nhảy tới. Chỉ dùng từ màn hình rộng và khi không bật giảm chuyển động.
 */
export default function TourShowcase({ stories }: { stories: Story[] }) {
  const reducedMotion = usePrefersReducedMotion()
  const track = useRef<HTMLDivElement>(null)
  const sentinels = useRef<(HTMLDivElement | null)[]>([])
  const [active, setActive] = useState(0)
  const last = stories.length - 1

  // Tour đang xem là đoạn cuộn đang nằm ở giữa màn hình (không gắn trình xử lý cuộn liên tục).
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) setActive(Number((entry.target as HTMLElement).dataset.index))
        })
      },
      { rootMargin: '-45% 0px -45% 0px' },
    )
    sentinels.current.forEach((el) => el && observer.observe(el))
    return () => observer.disconnect()
  }, [stories.length])

  const goTo = (index: number) => {
    const el = track.current
    if (!el) return
    const top = el.getBoundingClientRect().top + window.scrollY
    const segment = (window.innerHeight * SEGMENT_VH) / 100
    window.scrollTo({
      top: top - HEADER_PX + index * segment + (index === last ? 2 : 4),
      behavior: reducedMotion ? 'auto' : 'smooth',
    })
  }

  return (
    <div
      ref={track}
      className="relative"
      style={{ height: `calc(100svh - ${HEADER_PX}px + ${last * SEGMENT_VH}svh + 8px)` }}
    >
      {stories.map((story, i) => (
        <div
          key={story.id}
          ref={(el) => {
            sentinels.current[i] = el
          }}
          data-index={i}
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0"
          style={i === last ? { top: `${i * SEGMENT_VH}svh`, bottom: 0 } : { top: `${i * SEGMENT_VH}svh`, height: `${SEGMENT_VH}svh` }}
        />
      ))}

      <div
        className="sticky top-20 w-full overflow-hidden bg-deep-river"
        style={{ height: `calc(100svh - ${HEADER_PX}px)` }}
      >
        {stories.map((story, i) => (
          <div
            key={story.id}
            aria-hidden={i !== active}
            className={`absolute inset-0 transition-opacity duration-700 ease-out ${
              i === active ? 'opacity-100' : 'opacity-0'
            }`}
          >
            <div
              role="img"
              aria-label={story.title}
              className={`h-full w-full bg-cover bg-center transition-transform duration-[1800ms] ease-out ${
                i === active ? 'scale-100' : 'scale-110'
              }`}
              style={{ backgroundImage: `url(${story.image})` }}
            />
          </div>
        ))}
        <div className="absolute inset-0 bg-[linear-gradient(90deg,rgba(13,37,56,0.88)_0%,rgba(13,37,56,0.5)_42%,rgba(13,37,56,0.08)_100%)]" />
        <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-deep-river/60 to-transparent" />

        <div className="relative z-10 mx-auto flex h-full max-w-7xl items-stretch justify-between gap-10 pl-8 pr-20">
          <div className="flex max-w-xl flex-col justify-between py-10 text-white">
            <h2 className="font-headline-sm text-xs font-semibold uppercase tracking-[0.2em] text-white/80">
              {t('A Different Perspective on Saigon’s Skyline & Heritage')}
            </h2>

            <div className="relative min-h-[360px]">
              {stories.map((story, i) => (
                <div
                  key={story.id}
                  aria-hidden={i !== active}
                  className={`absolute inset-x-0 bottom-0 space-y-4 transition-all ease-out ${
                    i === active
                      ? 'translate-y-0 opacity-100 delay-300 duration-700'
                      : 'pointer-events-none translate-y-6 opacity-0 duration-300'
                  }`}
                >
                  <span className="inline-flex items-center gap-1.5 rounded-full border border-white/30 bg-deep-river/45 px-3.5 py-1.5 text-xs font-semibold uppercase tracking-[0.14em] backdrop-blur-sm">
                    <Icon name={story.badgeIcon} className={`text-[16px] ${story.badgeIconClass}`} />
                    {story.badge}
                  </span>
                  <h3 className="font-headline-xl text-4xl font-extrabold leading-[1.1] tracking-[-0.02em] drop-shadow-[0_3px_16px_rgba(13,37,56,0.5)] xl:text-[46px]">
                    {story.title}
                  </h3>
                  <p className="max-w-md text-base leading-relaxed text-white/85">{story.description}</p>
                  <div className="flex flex-wrap items-center gap-x-5 gap-y-2 text-sm text-white/90">
                    <span className="inline-flex items-center gap-1.5">
                      <Icon name="timer" className="text-[17px] text-sky-aqua" />
                      {t('{durationMins} mins', { durationMins: story.durationMins })}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Icon name="pin_drop" className="text-[17px] text-sky-aqua" />
                      {story.location}
                    </span>
                    <span className="rounded-full bg-white/90 px-3.5 py-1 font-numeric-md text-sm font-bold text-deep-river">
                      {t('From {price}', { price: formatVnd(story.fromPriceVnd) })}
                    </span>
                  </div>
                  <Link
                    to={ROUTES.explore}
                    tabIndex={i === active ? 0 : -1}
                    className="inline-flex min-h-[48px] items-center gap-2 rounded-full bg-white px-6 py-3 text-sm font-semibold text-deep-river transition-colors hover:bg-sky-aqua"
                  >
                    {t('Explore Tour')}
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </Link>
                </div>
              ))}
            </div>

            <p className="flex items-center gap-2 text-xs text-white/70">
              <span className="font-numeric-md text-sm font-bold text-white">
                {String(active + 1).padStart(2, '0')} / {String(stories.length).padStart(2, '0')}
              </span>
              {active < last && t('Scroll to see the next tour')}
            </p>
          </div>

          <nav aria-label={t('Tour list')} className="flex w-[300px] shrink-0 flex-col justify-center">
            <ol className="space-y-2">
              {stories.map((story, i) => (
                <li key={story.id}>
                  <button
                    type="button"
                    onClick={() => goTo(i)}
                    aria-current={i === active ? 'true' : undefined}
                    className={`flex w-full items-start gap-3 rounded-2xl border px-4 py-3 text-left backdrop-blur-md transition-all duration-300 ${
                      i === active
                        ? 'border-white/50 bg-white/20 text-white'
                        : 'border-white/10 bg-deep-river/35 text-white/65 hover:border-white/30 hover:text-white'
                    }`}
                  >
                    <span className="font-numeric-md text-sm font-bold">
                      {String(i + 1).padStart(2, '0')}
                    </span>
                    <span className="space-y-0.5">
                      <span className="block text-xs font-semibold uppercase tracking-[0.12em] opacity-80">
                        {story.badge}
                      </span>
                      <span className="block text-sm font-semibold leading-snug">{story.title}</span>
                    </span>
                  </button>
                </li>
              ))}
            </ol>
          </nav>
        </div>
      </div>
    </div>
  )
}
