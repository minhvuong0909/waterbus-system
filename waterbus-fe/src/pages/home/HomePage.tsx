import CorridorSection from '../../features/home/components/CorridorSection'
import DeparturesSection from '../../features/home/components/DeparturesSection'
import HeroSection from '../../features/home/components/HeroSection'
import HowItWorksSection from '../../features/home/components/HowItWorksSection'
import SectionNav from '../../features/home/components/SectionNav'
import StoriesSection from '../../features/home/components/StoriesSection'
import { useHomeData } from '../../features/home/hooks/useHomeData'
import { useDocumentTitle } from '../../hooks'
import { Reveal } from '../../components/motion'
import { ErrorState, PageLoader } from '../../components/ui'

export default function HomePage() {
  useDocumentTitle('River Connects Greater Stories')
  const { home, departures, corridor, loading, error, retry } = useHomeData()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !home || !departures || !corridor) {
    return <PageLoader />
  }

  return (
    <Reveal className="flex w-full flex-col">
      <HeroSection />
      <DeparturesSection
        filters={home.routeFilters}
        departures={departures}
        pierName={corridor.piers[0]?.name ?? ''}
      />
      <HowItWorksSection steps={home.steps} />
      <StoriesSection stories={home.stories} />
      <CorridorSection map={corridor} />
      <SectionNav />
    </Reveal>
  )
}
