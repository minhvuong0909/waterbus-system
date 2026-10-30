import { ErrorState, Icon, PageLoader } from '../../components/ui'
import NetworkSection from '../../features/trips/components/NetworkSection'
import SearchForm from '../../features/trips/components/SearchForm'
import { useSearchForm } from '../../features/trips/hooks/useTripSearch'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { Reveal } from '../../components/motion'

export default function SearchJourneyPage() {
  useDocumentTitle('Search Journey')
  const { piers, routes, dates, loading, error, retry } = useSearchForm()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !piers || !routes || !dates) return <PageLoader />

  return (
    <Reveal className="bg-mist pb-12 pt-6">
      <section className="mx-auto max-w-7xl px-6 pb-6 pt-2 lg:px-12">
        <div className="flex flex-col justify-between gap-space-md md:flex-row md:items-end">
          <div>
            <h1 className="font-display text-3xl font-extrabold leading-tight tracking-tight text-deep-river sm:text-4xl lg:text-5xl">
              {t('Plan Your River Journey')}
            </h1>
          </div>
        </div>
      </section>

      <section data-reveal className="mx-auto mb-6 max-w-7xl px-4 sm:px-6 lg:px-12">
        <SearchForm piers={piers} dates={dates} />
      </section>

      <section data-reveal className="mx-auto mb-8 max-w-7xl px-4 sm:px-6 lg:px-12">
        <details className="group rounded-2xl border border-teal-flow/20 bg-white shadow-sm">
          <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-6 py-4 font-display text-base font-bold text-deep-river">
            {t('Browse the river network')}
            <Icon
              name="expand_more"
              className="text-2xl text-teal-flow transition-transform group-open:rotate-180"
            />
          </summary>
          <NetworkSection routes={routes} piers={piers} />
        </details>
      </section>
    </Reveal>
  )
}
