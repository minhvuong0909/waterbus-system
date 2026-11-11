import ExploreHero from '../../features/explore/components/ExploreHero'
import LandmarksSection from '../../features/explore/components/LandmarksSection'
import SightseeingDeparturesSection from '../../features/explore/components/SightseeingDeparturesSection'
import { getExploreData, getSightseeingDepartures } from '../../features/explore/services/exploreService'
import { useDocumentTitle, useFetch } from '../../hooks'
import { Reveal } from '../../components/motion'
import { ErrorState, PageLoader } from '../../components/ui'

export default function ExplorePage() {
  useDocumentTitle('Explore River Journeys')
  const { data, loading, error, retry } = useFetch(getExploreData)
  // Chuyến thật từ API; lỗi chỉ ẩn phần chuyến, phần địa danh vẫn hiện.
  const departures = useFetch(() => getSightseeingDepartures())

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !data) return <PageLoader />

  return (
    <Reveal>
      <ExploreHero />
      <SightseeingDeparturesSection
        departures={departures.data}
        error={Boolean(departures.error)}
        onRetry={departures.retry}
      />
      <LandmarksSection categories={data.categories} landmarks={data.landmarks} />
    </Reveal>
  )
}
