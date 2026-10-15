import CorridorSection from '../../features/home/components/CorridorSection'
import CtaBanner from '../../features/home/components/CtaBanner'
import HeroSection from '../../features/home/components/HeroSection'
import HowItWorksSection from '../../features/home/components/HowItWorksSection'
import StoriesSection from '../../features/home/components/StoriesSection'
import { useHomeData } from '../../features/home/hooks/useHomeData'
import { useDocumentTitle } from '../../hooks'
import { ErrorState, PageLoader } from '../../components/ui'

export default function HomePage() {
  useDocumentTitle('River Connects Greater Stories')
  const { home, departures, corridor, loading, error, retry } = useHomeData()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !home || !departures || !corridor) {
    return <PageLoader />
  }

  return (
    <div className="flex w-full flex-col">
      <HeroSection cues={home.heroCues} />
      <CorridorSection filters={home.routeFilters} map={corridor} departures={departures} />
      <HowItWorksSection steps={home.steps} />
      <StoriesSection stories={home.stories} />
      <CtaBanner />
    </div>
  )
}
