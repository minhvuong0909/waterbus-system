import { Navigate } from 'react-router-dom'
import { ErrorState, PageLoader } from '../../components/ui'
import BookingStepHeader from '../../features/booking/components/BookingStepHeader'
import OrderSummary from '../../features/booking/components/OrderSummary'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { addTotals } from '../../features/booking/utils'
import { isRoundTrip } from '../../features/booking/legs'
import PaymentActions from '../../features/payment/components/PaymentActions'
import PaymentMethodPanel from '../../features/payment/components/PaymentMethodPanel'
import { useDocumentTitle } from '../../hooks'
import { useAppSelector } from '../../store/hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

export default function PaymentPage() {
  useDocumentTitle('Complete Your Payment')
  const hold = useAppSelector((s) => s.booking.hold)
  const roundTrip = isRoundTrip(useAppSelector((s) => s.booking.query))
  const outbound = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { trip, seats, purchaser } = outbound

  if (outbound.noTrip) return <NoTripSelected />
  // Đơn đã huỷ hoặc hết hạn (vd quay lại bằng nút Back): không còn gì để thanh toán.
  if (!hold) {
    return (
      <ErrorState
        icon="event_busy"
        title={t('This booking is no longer active')}
        message={t('The seats were released or the order was cancelled. Choose your seats again to continue.')}
        action={{ label: t('Choose seats again'), to: ROUTES.seatSelection }}
      />
    )
  }
  // Chưa gửi người mua và hành khách: bước xác nhận phải đi trước.
  if (!hold.submitted) return <Navigate to={ROUTES.checkoutReview} replace />
  const error = outbound.error ?? back.error
  if (error) {
    return (
      <ErrorState
        onRetry={() => {
          outbound.retry()
          back.retry()
        }}
      />
    )
  }

  const returnReady = !roundTrip || (back.trip && back.totals)
  if (outbound.loading || back.loading || !trip || !outbound.totals || !returnReady) return <PageLoader />

  // One payment for the whole order: both legs of a round trip (BR-COM-02, BR-PAY-04).
  const totals = addTotals(outbound.totals, roundTrip ? back.totals : undefined)
  const returnLeg = roundTrip && back.trip ? { trip: back.trip, seats: back.seats } : undefined

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-lg">
      <BookingStepHeader
        step="Payment"
        title={t('Complete Your Payment')}
        description={t('Pay through VNPay to receive your e-ticket.')}
      />

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-8">
          <PaymentMethodPanel
            amountVnd={totals.totalVnd}
            reference={hold.bookingCodes.join(', ')}
          />
        </div>
        <div className="lg:col-span-4 lg:self-stretch">
          <OrderSummary
            trip={trip}
            seats={seats}
            totals={totals}
            returnLeg={returnLeg}
            buyerName={purchaser.fullName}
          >
            <PaymentActions />
          </OrderSummary>
        </div>
      </div>
    </div>
  )
}
