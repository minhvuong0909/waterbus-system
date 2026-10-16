import { useRef } from 'react'
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
  const storiesRef = useRef<HTMLElement>(null)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !data) return <PageLoader />

  return (
    <Reveal>
      <ExploreHero onStories={() => storiesRef.current?.scrollIntoView({ behavior: 'smooth' })} />
      <JourneysSection journeys={data.journeys} />
      <LandmarksSection categories={data.categories} landmarks={data.landmarks} />
      <StoriesSection data={data} sectionRef={storiesRef} />
      <StepAboardBanner />
    </Reveal>
  )
}
