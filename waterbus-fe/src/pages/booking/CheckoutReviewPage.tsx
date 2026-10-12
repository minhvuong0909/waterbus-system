import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import {
  ETicketContactCard,
  JourneyDetailsCard,
  OrderSummary,
  PassengerInfoCard,
  TermsConfirm,
  VoucherCard,
} from '../../features/booking/components/CheckoutParts'
import { useBooking } from '../../features/booking/hooks/useBooking'
import type { BookingStep } from '../../features/booking/types'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function CheckoutReviewPage() {
  useDocumentTitle('Review Your Booking')
  const {
    loading,
    error,
    retry,
    trip,
    seat,
    seatId,
    passenger,
    voucher,
    totals,
    termsAccepted,
    setVoucher,
    setTerms,
  } = useBooking()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !totals) return <PageLoader />

  const steps: BookingStep[] = [
    { label: 'Trip', detail: trip.lineLabel },
    { label: 'Seat', detail: seat?.window ? 'Window Selected' : 'Aisle Selected' },
    { label: 'Details', detail: passenger.fullName },
    { label: 'Checkout', detail: 'Review Order' },
    { label: 'Payment', detail: 'Instant QR / Card' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <div className="flex flex-wrap items-center justify-between gap-space-sm">
        <Breadcrumb
          items={[
            { label: 'Home', to: ROUTES.home },
            { label: 'Booking', to: ROUTES.seatSelection },
            { label: 'Checkout' },
          ]}
        />
        <span className="inline-flex items-center gap-2 rounded-full bg-secondary-container/60 px-3 py-1 text-xs font-semibold text-on-secondary-container">
          <span className="h-2 w-2 rounded-full bg-teal-flow" />
          {t('River Crossing {vesselCode} • Review Stage', { vesselCode: trip.vesselCode })}
        </span>
      </div>

      <div className="mt-space-md">
        <BookingProgress steps={steps} current={3} variant="bars" />
      </div>

      <div className="mt-space-xl flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <h1 className="font-headline-xl text-headline-xl-mobile tracking-tight text-deep-river md:text-headline-xl">
            {t('Review Your Booking')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {t('Confirm your trip, passenger, seat, and fare before proceeding to payment.')}
          </p>
        </div>
        <span className="inline-flex items-center gap-2 self-start rounded-full bg-sand-light px-space-md py-2 text-xs text-teal-flow md:self-auto">
          <Icon name="verified_user" className="text-[16px]" />
          {t('Your selected seat remains reserved while you complete checkout.')}
        </span>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="space-y-space-md lg:col-span-8">
          <JourneyDetailsCard trip={trip} seat={seat} seatId={seatId} />
          <PassengerInfoCard passenger={passenger} />
          <ETicketContactCard passenger={passenger} />
          <VoucherCard voucher={voucher} onApply={setVoucher} />
          <TermsConfirm checked={termsAccepted} onChange={setTerms} />
        </div>
        <div className="lg:col-span-4">
          <OrderSummary trip={trip} totals={totals} canContinue={termsAccepted} />
        </div>
      </div>
    </div>
  )
}
