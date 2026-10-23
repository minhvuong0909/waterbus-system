import { useParams } from 'react-router-dom'
import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import {
  BookingSummaryCard,
  OperatingBanner,
  RefundPolicyCard,
} from '../../features/tickets/components/ManageParts'
import { useManageBooking } from '../../features/tickets/hooks/useTickets'
import { useDocumentTitle } from '../../hooks'
import { ApiError } from '../../services/apiClient'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function ManageBookingPage() {
  useDocumentTitle('Manage Booking')
  const { token } = useParams()
  const { manage, loading, error, retry } = useManageBooking(token)

  // 403: the backend rejects a wrong or expired guest token the same way as an unknown booking.
  if (error instanceof ApiError && (error.status === 404 || error.status === 403)) {
    return (
      <ErrorState
        title={t('This link is invalid or has expired')}
        message={t(
          'Open the latest email or message we sent you, or look up your booking with your booking code.',
        )}
      />
    )
  }

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !manage) return <PageLoader />

  return (
    <div className="mx-auto w-full max-w-6xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Manage Booking' },
        ]}
      />

      <div className="mt-space-md flex flex-col justify-between gap-space-md md:flex-row md:items-start">
        <div>
          <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river">
            {t('Manage Your Booking')}
          </h1>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {t('Your trip details, ticket status and refund information.')}
          </p>
        </div>
        <div className="flex items-center gap-space-sm self-start">
          <span className="rounded-lg border border-outline-variant/50 bg-white px-3 py-1.5 text-xs text-on-surface-variant">
            {t('Booking Code:')}{' '}
            <span className="font-mono font-bold text-deep-river">{manage.bookingCode}</span>
          </span>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1.5 text-xs font-semibold text-on-secondary-container">
            <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
            {manage.ticket.status}
          </span>
        </div>
      </div>

      <div className="mt-space-lg space-y-space-md">
        <BookingSummaryCard ticket={manage.ticket} />
        <OperatingBanner vessel={manage.ticket.tripCode} />
      </div>

      <div className="mt-space-md">
        <RefundPolicyCard />
      </div>
    </div>
  )
}
