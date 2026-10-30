import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import TripAside from '../../features/trips/components/TripAside'
import {
  BoardingSteps,
  JourneyTimeline,
  OnboardAmenities,
} from '../../features/trips/components/TripInfoSections'
import TripRouteCard from '../../features/trips/components/TripRouteCard'
import { useTripDetail } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

export default function TripDetailPage() {
  const { detail, loading, error, retry, noTrip } = useTripDetail()
  useDocumentTitle(detail ? t('Trip Detail ({code})', { code: detail.vesselCode }) : 'Trip Detail')

  if (noTrip) return <NoTripSelected />
  if (error) return <ErrorState onRetry={retry} />

  if (loading || !detail) return <PageLoader />

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <section className="pb-space-lg">
        <div className="space-y-space-xs">
          <Breadcrumb
            items={[
              { label: 'Home', to: ROUTES.home },
              { label: 'Search Results', to: ROUTES.searchResults },
              { label: t('Trip Detail ({code})', { code: detail.vesselCode }) },
            ]}
          />
          <div className="flex items-center gap-space-sm pt-space-xs">
            <h1 className="font-display text-headline-lg tracking-tight text-deep-river">
              {t('Review Your Journey')}
            </h1>
          </div>
          <p className="text-body-md text-on-surface-variant">
            {t(
              'Verify your departure details, pier boarding gate, and vessel amenities before choosing your seat.',
            )}
          </p>
        </div>
      </section>

      <TripRouteCard trip={detail} />

      <div className="grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-lg lg:col-span-7">
          <JourneyTimeline trip={detail} />
          <OnboardAmenities trip={detail} />
          <BoardingSteps trip={detail} />
        </div>
        <TripAside trip={detail} />
      </div>

    </div>
  )
}
