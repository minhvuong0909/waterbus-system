import NetworkMap from '../../../components/map/NetworkMap'
import { Icon } from '../../../components/ui'
import { formatVndSuffix } from '../../../utils/format'
import { useSearchQuery } from '../hooks/useTripSearch'
import type { NetworkRoute, PierOption } from '../types'
import { t } from '../../../i18n'

interface NetworkSectionProps {
  routes: NetworkRoute[]
  piers: PierOption[]
}

export default function NetworkSection({ routes, piers }: NetworkSectionProps) {
  const { updateQuery } = useSearchQuery()
  const pier = (id: string) => piers.find((p) => p.id === id)

  const applyRoute = (route: NetworkRoute) => {
    updateQuery({ originId: route.originId, destinationId: route.destinationId })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <div className="relative overflow-hidden rounded-2xl border border-teal-flow/20 bg-white p-6 shadow-sm sm:p-8">
      <div className="flex flex-col justify-between gap-space-md border-b border-surface-container pb-6 lg:flex-row lg:items-center">
        <div>
          <div className="flex items-center gap-space-xs">
            <span className="h-2.5 w-2.5 rounded-full bg-teal-flow" />
            <h2 className="font-headline-sm text-2xl font-bold tracking-tight text-deep-river">
              {t('Explore the River Network')}
            </h2>
          </div>
          <p className="mt-1 text-sm text-on-surface-variant">
            {t(
              'Select a popular waterway connection or trace vessels across our five active piers.',
            )}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-4 rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] px-4 py-2 text-xs font-medium text-on-surface-variant">
          <div className="flex items-center gap-1.5">
            <span className="h-1.5 w-3 rounded-full bg-teal-flow" />
            <span className="font-semibold text-deep-river">{t('Line 1 Main Corridor')}</span>
          </div>
          <span className="text-outline-variant">•</span>
          <div className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full border border-white bg-deep-river" />
            <span>{t('Pier Station Hub')}</span>
          </div>
          <span className="text-outline-variant">•</span>
          <div className="flex items-center gap-1.5">
            <span className="relative flex h-2 w-2">
              <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-sky-aqua opacity-75" />
              <span className="relative inline-flex h-2 w-2 rounded-full bg-teal-flow" />
            </span>
            <span className="font-semibold text-teal-flow">{t('Live Vessel WB-01')}</span>
          </div>
        </div>
      </div>

      <div className="mt-6 grid grid-cols-1 items-stretch gap-6 lg:grid-cols-12">
        <div className="lg:col-span-8">
          <NetworkMap />
        </div>
        <div className="flex flex-col justify-between gap-3 lg:col-span-4">
          {routes.map((route) => (
            <div
              key={route.id}
              className="flex flex-col justify-between rounded-xl border border-[#E2ECEE] bg-[#F4F7F8] p-4 shadow-xs transition-all hover:border-teal-flow/40 hover:bg-white hover:shadow-sm"
            >
              <div>
                <div className="mb-1.5 flex items-center justify-between gap-2">
                  <div className="flex items-center gap-1.5 font-headline-sm text-base font-bold text-deep-river">
                    <span>{pier(route.originId)?.shortName}</span>
                    <Icon name="arrow_forward" className="text-[16px] text-teal-flow" />
                    <span>{pier(route.destinationId)?.shortName}</span>
                  </div>
                  <span className="shrink-0 rounded-md bg-secondary-container px-2 py-0.5 font-headline-sm text-[10px] font-bold tracking-tight text-on-secondary-container">
                    {route.badge}
                  </span>
                </div>
                <p className="text-xs leading-relaxed text-on-surface-variant">{route.summary}</p>
              </div>
              <div className="mt-3 flex items-center justify-between border-t border-outline-variant/40 pt-2.5">
                <div>
                  <span className="block text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                    {t('Standard Fare')}
                  </span>
                  <span className="font-headline-sm text-sm font-bold text-teal-flow">
                    {t('From {price}', { price: formatVndSuffix(route.fromFareVnd) })}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => applyRoute(route)}
                  className="group inline-flex items-center gap-1 rounded-lg border border-teal-flow/20 bg-white px-3 py-1.5 font-headline-sm text-xs font-bold text-teal-flow shadow-2xs transition-all hover:bg-teal-flow hover:text-on-primary"
                >
                  <span>{t('Use this route')}</span>
                  <Icon
                    name="arrow_forward"
                    className="text-[14px] transition-transform group-hover:translate-x-0.5"
                  />
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}
