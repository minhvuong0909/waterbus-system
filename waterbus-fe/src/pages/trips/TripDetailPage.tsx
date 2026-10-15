import { Breadcrumb, ErrorState, FeatureStrip, Icon, PageLoader } from '../../components/ui'
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

const CUES = [
  {
    icon: 'qr_code_scanner',
    title: 'QR Ticket Boarding',
    description: 'Instant mobile ticket verification',
  },
  {
    icon: 'event_seat',
    title: 'Seat Selection Next',
    description: 'Select your preferred seat on the next screen',
  },
  {
    icon: 'navigation',
    title: 'Live Trip Tracking',
    description: 'Real-time updates available after booking confirmation',
  },
]

export default function TripDetailPage() {
  const { detail, loading, error, retry } = useTripDetail()
  useDocumentTitle(detail ? t('Trip Detail ({code})', { code: detail.vesselCode }) : 'Trip Detail')

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !detail) return <PageLoader />

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <section className="flex flex-col justify-between gap-space-md pb-space-lg md:flex-row md:items-center">
        <div className="space-y-space-xs">
          <Breadcrumb
            items={[
              { label: 'Home', to: ROUTES.home },
              { label: 'Search Results', to: ROUTES.searchResults },
              { label: t('Trip Detail ({code})', { code: detail.vesselCode }) },
            ]}
          />
          <div className="flex items-center gap-space-sm pt-space-xs">
            <h1 className="font-headline-lg text-headline-lg tracking-tight text-deep-river">
              {t('Review Your Journey')}
            </h1>
            <span className="hidden items-center gap-1.5 rounded-full bg-secondary-container px-space-sm py-0.5 text-body-sm font-semibold text-on-secondary-container sm:inline-flex">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {detail.lineLabel}
            </span>
          </div>
          <p className="text-body-md text-on-surface-variant">
            {t(
              'Verify your departure details, pier boarding gate, and vessel amenities before choosing your seat.',
            )}
          </p>
        </div>
        <div className="flex items-center gap-space-md self-start rounded-xl bg-mist px-space-md py-space-sm shadow-sm md:self-auto">
          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-secondary-container text-teal-flow">
            <Icon name="water" className="text-[20px]" />
          </div>
          <div className="text-left">
            <div className="font-headline-sm text-[15px] leading-tight text-deep-river">
              {t('Calm River Flow')}
            </div>
            <div className="flex items-center gap-1 text-body-sm font-medium text-teal-flow">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" /> {t('On-Time Departure')}
            </div>
          </div>
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

      <FeatureStrip items={CUES} variant="panel" className="mt-space-xl" />
    </div>
  )
}
