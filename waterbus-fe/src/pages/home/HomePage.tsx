import DeparturesSection from '../../features/home/components/DeparturesSection'
import HeroSection from '../../features/home/components/HeroSection'
import type { HeroFact } from '../../features/home/components/HeroSection'
import PiersSection from '../../features/home/components/PiersSection'
import StoriesSection from '../../features/home/components/StoriesSection'
import { useHomeData } from '../../features/home/hooks/useHomeData'
import { getFeaturedPlaces } from '../../features/home/services/homeService'
import { useDocumentTitle } from '../../hooks'
import { Reveal } from '../../components/motion'
import { ErrorState } from '../../components/ui'
import { t } from '../../i18n'
import { formatVnd } from '../../utils/format'

/** A data section that failed: a compact retry in place of the section, the rest of the page stays. */
function SectionError({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="mx-auto w-full max-w-7xl px-5 py-10 sm:px-8">
      <ErrorState compact onRetry={onRetry} />
    </div>
  )
}

export default function HomePage() {
  useDocumentTitle('River Connects Greater Stories')
  const { departures, corridor, fares } = useHomeData()

  // Figures under the headline, each shown only once the backend has answered it.
  const next = departures.data?.items[0]
  const pierCount = corridor.data?.piers.length ?? 0
  const facts: HeroFact[] = [
    fares.data?.Commuter !== undefined && {
      value: t('From {price}', { price: formatVnd(fares.data.Commuter) }),
      label: t('per ticket'),
    },
    next && next.trip.durationMins > 0 && {
      value: t('{minutes} min', { minutes: next.trip.durationMins }),
      label: next.to ? `${next.from} → ${next.to}` : next.from,
    },
    pierCount > 0 && { value: t('{count} piers', { count: pierCount }), label: t('along the Saigon River') },
  ].filter((fact): fact is HeroFact => Boolean(fact))
  // Both down is one outage (the backend), so it gets one message instead of the same one twice.
  const bothFailed = Boolean(departures.error && corridor.error)

  return (
    <Reveal className="flex w-full flex-col">
      <HeroSection facts={facts} />
      {bothFailed ? (
        <SectionError
          onRetry={() => {
            departures.retry()
            corridor.retry()
          }}
        />
      ) : (
        <>
          {departures.error ? (
            <SectionError onRetry={departures.retry} />
          ) : (
            departures.data && (
              <DeparturesSection departures={departures.data} pierName={corridor.data?.piers[0]?.name ?? ''} />
            )
          )}
          <StoriesSection places={getFeaturedPlaces()} />
          {corridor.error ? (
            <SectionError onRetry={corridor.retry} />
          ) : (
            corridor.data && <PiersSection map={corridor.data} />
          )}
        </>
      )}
    </Reveal>
  )
}
