import { useEffect, useRef, useState } from 'react'
import type { KeyboardEvent, PointerEvent } from 'react'
import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { usePrefersReducedMotion } from '../../../hooks'
import { ROUTES } from '../../../routes/routes'
import { useSearchQuery } from '../../trips/hooks/useTripSearch'
import { pierPhotos } from '../../trips/pierImages'
import ecoPhoto from '../../../assets/images/home/story-eco.jpg'
import heritagePhoto from '../../../assets/images/home/story-heritage.jpg'
import sunsetPhoto from '../../../assets/images/home/story-sunset.jpg'
import vesselPhoto from '../../../assets/images/vessel.jpg'
import waterwayPhoto from '../../../assets/images/waterway.jpg'
import { PIER_INFO } from '../pierInfo'
import PierGallery from './PierGallery'
import type { GallerySlide } from './PierGallery'
import type { Pier } from '../../trips/types'
import { getLocale, t } from '../../../i18n'

/** Quãng kéo ngang tối thiểu (px) để tính là một cú vuốt sang bến khác. */
const SWIPE_PX = 60

/** Ảnh dùng chung cho mọi bến khi bến chưa đủ ảnh riêng; chú thích ghi rõ là ảnh dọc sông. */
const RIVER_PHOTOS = [vesselPhoto, waterwayPhoto, sunsetPhoto, heritagePhoto, ecoPhoto]

function slidesOf(pier: Pier): GallerySlide[] {
  const own = pierPhotos(pier.code).map((src) => ({ src, caption: pier.name }))
  const river = RIVER_PHOTOS.map((src) => ({ src, caption: t('Along the Saigon River') }))
  return [...own, ...river]
}

function distanceLabel(distance: string): string {
  if (distance === 'inside') return t('Inside the pier')
  if (distance === 'opposite') return t('Opposite the pier')
  return t('About {distance}', { distance })
}

/**
 * Hệ thống bến tàu: dãy bến nằm ngang theo thứ tự dọc sông (như một tuyến đường). Bấm một bến,
 * hoặc vuốt/ bấm mũi tên, thì phần mô tả bên dưới trượt ngang sang bến đó.
 */
export default function PiersSection({ piers }: { piers: Pier[] }) {
  const reducedMotion = usePrefersReducedMotion()
  const { query, updateQuery } = useSearchQuery()
  const [active, setActive] = useState(0)
  const tabs = useRef<(HTMLButtonElement | null)[]>([])
  const stripRef = useRef<HTMLDivElement>(null)
  const swipeStart = useRef<number | null>(null)
  const last = piers.length - 1

  // Khi dãy bến phải cuộn ngang (màn hình hẹp), đưa bến đang chọn vào giữa dãy. Chỉ cuộn bên trong dãy
  // (không dùng scrollIntoView vì nó cuộn cả trang) và bỏ qua lần hiện đầu để không kéo trang xuống.
  const mounted = useRef(false)
  useEffect(() => {
    if (!mounted.current) {
      mounted.current = true
      return
    }
    const strip = stripRef.current
    const tab = tabs.current[active]
    if (!strip || !tab || strip.scrollWidth <= strip.clientWidth) return
    strip.scrollTo({
      left: tab.offsetLeft - (strip.clientWidth - tab.clientWidth) / 2,
      behavior: reducedMotion ? 'auto' : 'smooth',
    })
  }, [active, reducedMotion])

  if (piers.length === 0) return null

  const go = (index: number) => setActive(Math.min(last, Math.max(0, index)))

  const onKeyDown = (event: KeyboardEvent) => {
    const target =
      event.key === 'ArrowRight' ? active + 1
      : event.key === 'ArrowLeft' ? active - 1
      : event.key === 'Home' ? 0
      : event.key === 'End' ? last
      : null
    if (target === null) return
    event.preventDefault()
    const index = Math.min(last, Math.max(0, target))
    go(index)
    tabs.current[index]?.focus({ preventScroll: true })
  }

  const onPointerDown = (event: PointerEvent) => {
    swipeStart.current = event.clientX
  }
  const onPointerUp = (event: PointerEvent) => {
    if (swipeStart.current === null) return
    const delta = event.clientX - swipeStart.current
    swipeStart.current = null
    if (Math.abs(delta) >= SWIPE_PX) go(active + (delta < 0 ? 1 : -1))
  }

  // Một chuyến cần hai bến khác nhau: chọn bến đi trùng bến đến thì đảo hai bến cho nhau.
  const bookFrom = (pier: Pier) =>
    updateQuery(
      pier.id === query.destinationId
        ? { originId: pier.id, destinationId: query.originId }
        : { originId: pier.id },
    )

  const lineInset = `${50 / piers.length}%`

  return (
    <section id="piers" className="w-full scroll-mt-20 bg-surface-container-lowest py-10 sm:py-14">
      <div className="mx-auto max-w-7xl space-y-8 px-5 sm:px-8">
        <div data-reveal className="max-w-2xl space-y-space-xs">
          <h2 className="font-headline-lg text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-[36px]">
            {t('Pier network')}
          </h2>
          <p className="text-sm text-on-surface-variant sm:text-base">
            {t('Piers along the Saigon River, from {first} to {last}. Choose a pier to see where it is.', {
              first: piers[0].name,
              last: piers[last].name,
            })}
          </p>
        </div>

        <div
          ref={stripRef}
          data-reveal
          className="relative -mx-5 overflow-x-auto px-5 pb-2 sm:mx-0 sm:px-0 [scrollbar-width:none]"
        >
          <div role="tablist" aria-label={t('Pier list')} className="relative flex min-w-[560px]">
            <span
              aria-hidden="true"
              className="absolute top-[19px] h-0.5 rounded-full bg-outline-variant/60"
              style={{ left: lineInset, right: lineInset }}
            />
            <span
              aria-hidden="true"
              className={`absolute top-[19px] h-0.5 rounded-full bg-teal-flow ${
                reducedMotion ? '' : 'transition-[width] duration-500 ease-out'
              }`}
              style={{
                left: lineInset,
                width: `calc((100% - 2 * ${lineInset}) * ${last > 0 ? active / last : 0})`,
              }}
            />
            {piers.map((pier, i) => {
              const selected = i === active
              return (
                <button
                  key={pier.id}
                  ref={(el) => {
                    tabs.current[i] = el
                  }}
                  type="button"
                  role="tab"
                  id={`pier-tab-${pier.id}`}
                  aria-selected={selected}
                  aria-controls={`pier-panel-${pier.id}`}
                  tabIndex={selected ? 0 : -1}
                  onClick={() => go(i)}
                  onKeyDown={onKeyDown}
                  className="group relative flex min-w-[112px] flex-1 flex-col items-center gap-2 px-2 pb-1 text-center focus:outline-none"
                >
                  <span
                    className={`flex h-10 w-10 items-center justify-center rounded-full border-2 font-numeric-md text-sm font-bold transition-all duration-300 group-focus-visible:ring-4 group-focus-visible:ring-teal-flow/30 ${
                      selected
                        ? 'scale-110 border-teal-flow bg-teal-flow text-on-primary shadow-md'
                        : i < active
                        ? 'border-teal-flow bg-white text-teal-flow'
                        : 'border-outline-variant bg-white text-on-surface-variant group-hover:border-teal-flow/60'
                    }`}
                  >
                    {i + 1}
                  </span>
                  <span
                    className={`text-sm leading-tight transition-colors ${
                      selected ? 'font-bold text-deep-river' : 'font-medium text-on-surface-variant group-hover:text-deep-river'
                    }`}
                  >
                    {pier.name.replace(/^Bến\s+/i, '')}
                  </span>
                </button>
              )
            })}
          </div>
        </div>

        <div
          data-reveal
          className="relative touch-pan-y select-none overflow-hidden rounded-panel border border-outline-variant/30 bg-white shadow-sm"
          onPointerDown={onPointerDown}
          onPointerUp={onPointerUp}
          onPointerCancel={() => {
            swipeStart.current = null
          }}
        >
          <div
            className={`flex ${reducedMotion ? '' : 'transition-transform duration-500 ease-out'}`}
            style={{ transform: `translateX(-${active * 100}%)` }}
          >
            {piers.map((pier, i) => {
              const selected = i === active
              const role =
                i === 0 ? t('First pier') : i === last ? t('Last pier') : t('Intermediate pier')
              const previous = piers[i - 1]
              const next = piers[i + 1]
              return (
                <div
                  key={pier.id}
                  role="tabpanel"
                  id={`pier-panel-${pier.id}`}
                  aria-labelledby={`pier-tab-${pier.id}`}
                  aria-hidden={!selected}
                  className="grid w-full shrink-0 grid-cols-1 items-center gap-6 p-5 sm:p-8 md:grid-cols-2 md:gap-10"
                >
                  <div className="space-y-5">
                    <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
                      <Icon name="anchor" className="text-[15px]" />
                      {t('Pier {n} of {total}', { n: i + 1, total: piers.length })} · {role}
                    </span>
                    <div className="space-y-1.5">
                      <h3 className="font-headline-md text-2xl font-bold text-deep-river sm:text-3xl">
                        {pier.name}
                      </h3>
                      <p className="flex items-center gap-1.5 text-sm font-medium text-teal-flow">
                        <Icon name="location_city" className="text-[17px]" />
                        {pier.subtitle}
                      </p>
                      <p className="flex items-start gap-1.5 text-sm text-on-surface-variant">
                        <Icon name="pin_drop" className="mt-0.5 text-[17px] text-teal-flow" />
                        {pier.address}
                      </p>
                    </div>

                    {PIER_INFO[pier.code] && (
                      <>
                        <p className="text-sm leading-relaxed text-on-surface sm:text-base">
                          {PIER_INFO[pier.code].story[getLocale()]}
                        </p>
                        <div className="space-y-2">
                          <h4 className="text-xs font-bold uppercase tracking-[0.14em] text-on-surface-variant">
                            {t('Nearby')}
                          </h4>
                          <ul className="space-y-1.5 text-sm">
                            {PIER_INFO[pier.code].nearby.map((place) => (
                              <li key={place.name.en} className="flex items-baseline justify-between gap-3 border-b border-surface-container pb-1.5">
                                <span className="font-medium text-deep-river">{place.name[getLocale()]}</span>
                                <span className="shrink-0 text-xs text-on-surface-variant">
                                  {distanceLabel(place.distance)}
                                </span>
                              </li>
                            ))}
                          </ul>
                          <p className="text-[11px] text-on-surface-variant">
                            {t('Distances are approximate.')}
                          </p>
                        </div>
                      </>
                    )}

                    <div className="flex flex-wrap items-center gap-3">
                      <Link
                        to={ROUTES.search}
                        tabIndex={selected ? 0 : -1}
                        onClick={() => bookFrom(pier)}
                        className="inline-flex min-h-[44px] items-center gap-2 rounded-full bg-teal-flow px-6 py-2.5 text-sm font-semibold text-on-primary shadow-sm transition-colors hover:bg-secondary"
                      >
                        {t('Plan a trip from here')}
                        <Icon name="arrow_forward" className="text-[18px]" />
                      </Link>
                      <a
                        href={`https://www.google.com/maps/dir/?api=1&destination=${pier.latitude},${pier.longitude}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        tabIndex={selected ? 0 : -1}
                        className="inline-flex min-h-[44px] items-center gap-2 rounded-full border border-teal-flow px-5 py-2.5 text-sm font-semibold text-teal-flow transition-colors hover:bg-sand-light"
                      >
                        <Icon name="directions" className="text-[18px]" />
                        {t('Get directions')}
                      </a>
                    </div>

                    <div className="flex flex-wrap items-center gap-x-5 gap-y-1 border-t border-surface-container pt-4 text-sm">
                      {previous && (
                        <button
                          type="button"
                          tabIndex={selected ? 0 : -1}
                          onClick={() => go(i - 1)}
                          className="inline-flex items-center gap-1 font-medium text-on-surface-variant hover:text-teal-flow"
                        >
                          <Icon name="arrow_back" className="text-[16px]" />
                          {t('Previous pier')}: {previous.name.replace(/^Bến\s+/i, '')}
                        </button>
                      )}
                      {next && (
                        <button
                          type="button"
                          tabIndex={selected ? 0 : -1}
                          onClick={() => go(i + 1)}
                          className="inline-flex items-center gap-1 font-medium text-on-surface-variant hover:text-teal-flow"
                        >
                          {t('Next pier')}: {next.name.replace(/^Bến\s+/i, '')}
                          <Icon name="arrow_forward" className="text-[16px]" />
                        </button>
                      )}
                    </div>
                  </div>

                  <PierGallery
                    slides={slidesOf(pier)}
                    label={t('{name}, photo gallery', { name: pier.name })}
                    active={selected}
                  />
                </div>
              )
            })}
          </div>
        </div>
      </div>
    </section>
  )
}
