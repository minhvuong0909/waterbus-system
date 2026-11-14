import { Navigate, useLocation, useNavigate, useParams } from 'react-router-dom'
import { Breadcrumb, Button, ErrorState, PageLoader } from '../../components/ui'
import BookingSummaryCard from '../../features/tickets/components/BookingSummaryCard'
import BookingLinkPrompt from '../../features/tickets/components/BookingLinkPrompt'
import OrderAccessCard from '../../features/tickets/components/OrderAccessCard'
import ShareTripCard from '../../features/tickets/components/ShareTripCard'
import RefundPolicyCard from '../../features/tickets/components/RefundPolicyCard'
import { useManageBooking } from '../../features/tickets/hooks/useTickets'
import { selectTicket } from '../../features/tickets/ticketsSlice'
import { forgetOrder } from '../../features/tickets/orderStorage'
import { hasOrderToManage } from '../../features/tickets/services/ticketService'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { useDocumentTitle } from '../../hooks'
import { ApiError } from '../../services/apiClient'
import { ROUTES } from '../../routes/routes'
import { useAppDispatch } from '../../store/hooks'
import { t } from '../../i18n'

export default function ManageBookingPage() {
  useDocumentTitle('Manage Booking')
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const { token } = useParams()
  const { pathname, search } = useLocation()
  const { user } = useAuth()
  const { manage, loading, error, retry } = useManageBooking(token)

  // Đơn của tài khoản (`?orderId=` không kèm token) chỉ mở được sau khi đăng nhập.
  if (!token && !user && new URLSearchParams(search).has('orderId')) {
    return <Navigate to={ROUTES.signIn} state={{ from: pathname + search }} replace />
  }

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

  // A round trip has two bookings; name them so the cards and the share link say which leg they are.
  const legLabels = manage.bookings.length === 2 ? [t('Outbound trip'), t('Return trip')] : []

  return (
    <div className="mx-auto w-full max-w-6xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Manage Booking' },
        ]}
      />

      <div className="mt-space-md">
        <h1 className="font-display text-4xl font-bold tracking-tight text-deep-river">
          {t('Manage Your Booking')}
        </h1>
        <p className="mt-1 text-body-md text-on-surface-variant">
          {t('Your trip details, ticket status and refund information.')}
        </p>
      </div>

      <div className="mt-space-lg space-y-space-md">
        {manage.bookings.map((booking, index) => (
          <BookingSummaryCard
            key={booking.bookingId}
            booking={booking}
            legLabel={legLabels[index]}
            onOpenTicket={(id) => dispatch(selectTicket(id))}
          />
        ))}
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-md lg:grid-cols-2">
        <ShareTripCard
          orderId={manage.orderId}
          accessToken={manage.accessToken}
          bookings={manage.bookings.map((booking, index) => ({
            bookingId: booking.bookingId,
            label: legLabels[index],
          }))}
        />
        <OrderAccessCard orderId={manage.orderId} accessToken={manage.accessToken} />
      </div>

      <div className="mt-space-md">
        <RefundPolicyCard />
      </div>

      {manage.accessToken !== null && (
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
      )}
    </div>
  )
}
