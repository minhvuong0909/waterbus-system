import { useState } from 'react'
import { Breadcrumb, Button, ErrorState, Icon, PageLoader } from '../../components/ui'
import AccountOrderCard from '../../features/tickets/components/AccountOrderCard'
import { useAccountOrders } from '../../features/tickets/hooks/useTickets'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'

/** Every order of the signed-in Passenger (BR-ID-04), including unpaid and cancelled ones. */
export default function OrderHistoryPage() {
  useDocumentTitle('Order history')
  const [pageNumber, setPageNumber] = useState(1)
  const { page, loading, fetching, error, retry } = useAccountOrders(pageNumber)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !page) return <PageLoader />

  const goTo = (next: number) => {
    setPageNumber(next)
    window.scrollTo({ top: 0 })
  }

  return (
    <div className="mx-auto w-full max-w-4xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Order history' },
        ]}
      />
      <h1 className="mt-space-sm font-display text-4xl font-bold tracking-tight text-deep-river">
        {t('Order history')}
      </h1>

      {page.items.length === 0 ? (
        <div className="mx-auto mt-space-xl flex max-w-lg flex-col items-center rounded-2xl border border-outline-variant/40 bg-white p-space-xl text-center shadow-sm">
          <span className="flex h-16 w-16 items-center justify-center rounded-full bg-secondary-container text-on-secondary-container">
            <Icon name="receipt_long" className="text-3xl" />
          </span>
          <h2 className="mt-space-md font-display text-headline-md text-deep-river">
            {t('No orders in your account yet')}
          </h2>
          <p className="mt-space-xs text-body-md text-on-surface-variant">
            {t('Bookings you make while signed in appear here. To add one you bought as a guest, open its booking link and save it to your account.')}
          </p>
          <Button to={ROUTES.searchResults} className="mt-space-lg w-full sm:w-auto">
            {t('Find a trip')}
          </Button>
        </div>
      ) : (
        <>
          <div
            className={`mt-space-lg space-y-space-md transition-opacity ${fetching ? 'opacity-60' : ''}`}
            aria-busy={fetching}
          >
            {page.items.map((order) => (
              <AccountOrderCard key={order.orderId} order={order} />
            ))}
          </div>

          {page.totalPages > 1 && (
            <nav
              aria-label={t('Pages')}
              className="mt-space-lg flex items-center justify-between gap-space-md"
            >
              <Button
                variant="neutral"
                size="sm"
                icon="chevron_left"
                onClick={() => goTo(page.pageNumber - 1)}
                disabled={fetching || page.pageNumber <= 1}
              >
                {t('Newer')}
              </Button>
              <span className="text-sm text-on-surface-variant">
                {t('Page {page} of {total}', { page: page.pageNumber, total: page.totalPages })}
              </span>
              <Button
                variant="neutral"
                size="sm"
                onClick={() => goTo(page.pageNumber + 1)}
                disabled={fetching || page.pageNumber >= page.totalPages}
              >
                {t('Older')}
                <Icon name="chevron_right" className="text-lg" />
              </Button>
            </nav>
          )}
        </>
      )}
    </div>
  )
}
