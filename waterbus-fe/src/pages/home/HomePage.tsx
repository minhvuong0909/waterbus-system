import CorridorSection from '../../features/home/components/CorridorSection'
import DeparturesSection from '../../features/home/components/DeparturesSection'
import HeroSection from '../../features/home/components/HeroSection'
import HowItWorksSection from '../../features/home/components/HowItWorksSection'
import PiersSection from '../../features/home/components/PiersSection'
import SectionNav from '../../features/home/components/SectionNav'
import StoriesSection from '../../features/home/components/StoriesSection'
import { useHomeData } from '../../features/home/hooks/useHomeData'
import { useDocumentTitle } from '../../hooks'
import { Reveal } from '../../components/motion'
import { ErrorState } from '../../components/ui'

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
  const { home, departures, corridor } = useHomeData()

  return (
    <Reveal className="flex w-full flex-col">
      <HeroSection />
      {departures.error ? (
        <SectionError onRetry={departures.retry} />
      ) : (
        departures.data && home.data && (
          <DeparturesSection
            filters={home.data.routeFilters}
            departures={departures.data}
            pierName={corridor.data?.piers[0]?.name ?? ''}
          />
        )
      )}
      {home.data && <HowItWorksSection steps={home.data.steps} />}
      {home.data && <StoriesSection stories={home.data.stories} />}
      {corridor.error ? (
        <SectionError onRetry={corridor.retry} />
      ) : (
        corridor.data && (
          <>
            <PiersSection piers={corridor.data.piers} />
            <CorridorSection map={corridor.data} />
          </>
        )
      )}
      <SectionNav />
    </Reveal>
  )
}
