import { useRef } from 'react'
import {
  CorridorSection,
  ExploreHero,
  JourneysSection,
  LandmarksSection,
  ModesSection,
  StepAboardBanner,
  StoriesSection,
} from '../../features/explore/components/ExploreSections'
import { getExploreData } from '../../features/explore/services/exploreService'
import { useDocumentTitle, useFetch } from '../../hooks'
import { ErrorState, PageLoader } from '../../components/ui'

export default function ExplorePage() {
  useDocumentTitle('Explore River Journeys')
  const { data, loading, error, retry } = useFetch(getExploreData)
  const storiesRef = useRef<HTMLElement>(null)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !data) return <PageLoader />

  return (
    <div>
      <ExploreHero onStories={() => storiesRef.current?.scrollIntoView({ behavior: 'smooth' })} />
      <JourneysSection journeys={data.journeys} />
      <ModesSection modes={data.modes} />
      <LandmarksSection categories={data.categories} landmarks={data.landmarks} />
      <StoriesSection data={data} sectionRef={storiesRef} />
      <CorridorSection data={data} />
      <StepAboardBanner />
    </div>
  )
}
