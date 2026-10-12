import { ErrorState, FeatureStrip, Icon, PageLoader, StatusPill } from '../../components/ui'
import DiscoverBanner from '../../features/trips/components/DiscoverBanner'
import NetworkSection from '../../features/trips/components/NetworkSection'
import SearchForm from '../../features/trips/components/SearchForm'
import { useSearchForm } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'

const CUES = [
  {
    icon: 'qr_code_scanner',
    title: 'QR E-Ticket Boarding',
    description: 'Tap and go at automated turnstiles across all river piers.',
  },
  {
    icon: 'radar',
    title: 'Real-Time Vessel Tracking',
    description: 'Track WB hydrofoil speed and live catamaran dock ETA.',
  },
  {
    icon: 'accessible',
    title: '100% Step-Free Access',
    description: 'Universal low-incline gangways on all docks and catamarans.',
  },
]

export default function SearchJourneyPage() {
  useDocumentTitle('Search Journey')
  const { piers, routes, dates, loading, error, retry } = useSearchForm()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !piers || !routes || !dates) return <PageLoader />

  return (
    <div className="bg-mist pb-20 pt-6">
      <section className="mx-auto max-w-7xl px-6 pb-6 pt-2 lg:px-12">
        <div className="flex flex-col justify-between gap-space-md md:flex-row md:items-end">
          <div>
            <div className="mb-space-xs inline-flex items-center gap-space-xs rounded-full border border-teal-flow/20 bg-sand-light px-3 py-1 font-headline-sm text-xs font-semibold text-teal-flow">
              <Icon name="water" className="text-[16px]" />
              <span className="text-[11px] font-bold uppercase tracking-wider">
                {t('River Route Planning')}
              </span>
            </div>
            <h1 className="font-headline-sm text-3xl font-extrabold leading-tight tracking-tight text-deep-river sm:text-4xl lg:text-[42px]">
              {t('Plan Your River Journey')}
            </h1>
            <p className="mt-1.5 max-w-2xl text-base leading-relaxed text-on-surface-variant">
              {t(
                'Select your departure pier and destination along the Saigon River transit corridor.',
              )}
            </p>
          </div>
          <StatusPill>{t('All 5 River Terminals Operational')}</StatusPill>
        </div>
      </section>

      <section className="mx-auto mb-10 max-w-7xl px-6 lg:px-12">
        <SearchForm piers={piers} dates={dates} />
      </section>

      <section className="mx-auto mb-12 max-w-7xl px-6 lg:px-12">
        <NetworkSection routes={routes} piers={piers} />
      </section>

      <section className="mx-auto mb-12 max-w-7xl px-6 lg:px-12">
        <DiscoverBanner />
      </section>

      <section className="mx-auto max-w-7xl px-6 lg:px-12">
        <FeatureStrip items={CUES} />
      </section>
    </div>
  )
}
