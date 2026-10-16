import {
  ExploreHero,
  JourneysSection,
  LandmarksSection,
  StepAboardBanner,
  StoriesSection,
} from '../../features/explore/components/ExploreSections'
import { getExploreData } from '../../features/explore/services/exploreService'
import { useDocumentTitle, useFetch } from '../../hooks'
import { Reveal } from '../../components/motion'
import { ErrorState, PageLoader } from '../../components/ui'

export default function ExplorePage() {
  useDocumentTitle('Explore River Journeys')
  const { data, loading, error, retry } = useFetch(getExploreData)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !data) return <PageLoader />

  return (
    <Reveal>
      <ExploreHero />
      <JourneysSection journeys={data.journeys} />
      <LandmarksSection categories={data.categories} landmarks={data.landmarks} />
      <StoriesSection />
      <StepAboardBanner />
    </Reveal>
  )
}
