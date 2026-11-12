import { lazy, Suspense, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { useSearchQuery } from '../../booking/hooks/useTripSearch'
import type { CorridorMap, Pier } from '../../trips/types'
import { t } from '../../../i18n'
import PierDetails from './PierDetails'

// The map pulls in Leaflet and its styles, so it loads after the rest of the page.
const PierMap = lazy(() => import('../../trips/components/PierMap'))

const shortName = (pier: Pier) => pier.name.replace(/^Bến\s+/i, '')

/**
 * The piers of the corridor in river order. One pier is open at a time, chosen from the strip or
 * from the map: its details are on the left and the map marks where it is on the right.
 */
export default function PiersSection({ map }: { map: CorridorMap }) {
  const { piers } = map
  const { query, updateQuery } = useSearchQuery()
  const [activeId, setActiveId] = useState(piers[0]?.id)
  const tabs = useRef<(HTMLButtonElement | null)[]>([])

  const activeIndex = Math.max(0, piers.findIndex((pier) => pier.id === activeId))
  const active = piers[activeIndex]
  if (!active) return null
  const last = piers.length - 1

  const onKeyDown = (event: KeyboardEvent) => {
    const target =
      event.key === 'ArrowRight' ? activeIndex + 1
      : event.key === 'ArrowLeft' ? activeIndex - 1
      : event.key === 'Home' ? 0
      : event.key === 'End' ? last
      : null
    if (target === null) return
    event.preventDefault()
    const index = Math.min(last, Math.max(0, target))
    setActiveId(piers[index].id)
    tabs.current[index]?.focus({ preventScroll: true })
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
          <h2 className="font-display text-2xl font-bold tracking-tight text-deep-river sm:text-3xl lg:text-4xl">
            {t('Pier network')}
          </h2>
          <p className="text-sm text-on-surface-variant sm:text-base">
            {t('Piers along the Saigon River, from {first} to {last}. Choose a pier to see where it is.', {
              first: piers[0].name,
              last: piers[last].name,
            })}
          </p>
        </div>

        <div data-reveal className="-mx-5 overflow-x-auto px-5 pb-2 sm:mx-0 sm:px-0 [scrollbar-width:none]">
          <div role="tablist" aria-label={t('Pier list')} className="relative flex min-w-[560px]">
            <span
              aria-hidden="true"
              className="absolute top-[19px] h-0.5 rounded-full bg-outline-variant/60"
              style={{ left: lineInset, right: lineInset }}
            />
            {piers.map((pier, i) => {
              const selected = i === activeIndex
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
                  aria-controls="pier-panel"
                  tabIndex={selected ? 0 : -1}
                  onClick={() => setActiveId(pier.id)}
                  onKeyDown={onKeyDown}
                  className="group relative flex min-w-[112px] flex-1 flex-col items-center gap-2 px-2 pb-1 text-center focus:outline-none"
                >
                  <span
                    className={`flex h-10 w-10 items-center justify-center rounded-full border-2 font-display text-sm font-bold transition-colors group-focus-visible:ring-4 group-focus-visible:ring-teal-flow/25 ${
                      selected
                        ? 'border-teal-flow bg-teal-flow text-on-primary shadow-md'
                        : 'border-outline-variant bg-white text-on-surface-variant group-hover:border-teal-flow/60'
                    }`}
                  >
                    {i + 1}
                  </span>
                  <span
                    className={`text-sm leading-tight transition-colors ${
                      selected
                        ? 'font-bold text-deep-river'
                        : 'font-medium text-on-surface-variant group-hover:text-deep-river'
                    }`}
                  >
                    {shortName(pier)}
                  </span>
                </button>
              )
            })}
          </div>
        </div>

        <div
          data-reveal
          role="tabpanel"
          id="pier-panel"
          aria-labelledby={`pier-tab-${active.id}`}
          className="grid grid-cols-1 items-start gap-6 rounded-panel border border-outline-variant/30 bg-white p-5 shadow-sm sm:p-8 lg:grid-cols-2 lg:gap-10"
        >
          <PierDetails pier={active} onBook={() => bookFrom(active)} />
          <Suspense
            fallback={
              <div className="flex h-[360px] items-center justify-center rounded-2xl bg-surface-container text-sm text-on-surface-variant lg:h-[460px]">
                {t('Loading map…')}
              </div>
            }
          >
            <PierMap map={map} selectedId={active.id} onSelect={setActiveId} className="h-[360px] lg:h-[460px]" />
          </Suspense>
        </div>
      </div>
    </section>
  )
}
