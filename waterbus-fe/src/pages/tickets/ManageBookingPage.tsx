import { useNavigate, useParams } from 'react-router-dom'
import { Breadcrumb, Button, ErrorState, PageLoader } from '../../components/ui'
import BookingSummaryCard from '../../features/tickets/components/BookingSummaryCard'
import ManageLinkCard from '../../features/tickets/components/ManageLinkCard'
import BookingLinkPrompt from '../../features/tickets/components/BookingLinkPrompt'
import RefundPolicyCard from '../../features/tickets/components/RefundPolicyCard'
import { useManageBooking } from '../../features/tickets/hooks/useTickets'
import { forgetOrder } from '../../features/tickets/orderStorage'
import { hasOrderToManage } from '../../features/tickets/services/ticketService'
import { useDocumentTitle } from '../../hooks'
import { ApiError } from '../../services/apiClient'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function ManageBookingPage() {
  useDocumentTitle('Manage Booking')
  const navigate = useNavigate()
  const { token } = useParams()
  const { manage, loading, error, retry } = useManageBooking(token)

  // Không có link và tab này chưa đặt đơn nào: cho khách dán link đã lưu thay vì báo lỗi.
  if (!hasOrderToManage(token)) {
    return (
      <BookingLinkPrompt
        title={t('Open your booking')}
        message={t(
          'Paste the booking link you saved after payment. Tickets bought in this browser are already in My Tickets.',
        )}
      />
    )
  }

  // 403: the backend rejects a wrong or expired guest token the same way as an unknown booking.
  if (error instanceof ApiError && (error.status === 404 || error.status === 403)) {
    return (
      <BookingLinkPrompt
        title={t('This link is invalid or has expired')}
        message={t(
          'Check that you copied the whole link, or call the hotline with your booking code.',
        )}
        warning
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
          <h1 className="font-display text-4xl font-bold tracking-tight text-deep-river">
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
      </div>

      {manage.manageUrl && (
        <div className="mt-space-md">
          <ManageLinkCard url={manage.manageUrl} />
        </div>
      )}

      <div className="mt-space-md">
        <RefundPolicyCard />
      </div>

      <div className="mt-space-lg flex justify-end">
        <Button
          variant="text"
          size="sm"
          icon="delete"
          onClick={() => {
            forgetOrder(manage.orderId)
            navigate(ROUTES.myTickets)
          }}
        >
          {t('Remove from this device')}
        </Button>
      </div>
    </div>
  )
}
