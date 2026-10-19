import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import BookingProgress from '../../features/booking/components/BookingProgress'
import CrossingSummary from '../../features/booking/components/CrossingSummary'
import HoldCountdown from '../../features/booking/components/HoldCountdown'
import PassengerFormCard from '../../features/booking/components/PassengerFormCard'
import { useBooking } from '../../features/booking/hooks/useBooking'
import type { BookingStep } from '../../features/booking/types'
import { isBookingComplete, seatCodes } from '../../features/booking/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function PassengerDetailsPage() {
  useDocumentTitle('Passenger Details')
  const {
    loading,
    error,
    retry,
    trip,
    seats,
    seatIds,
    purchaser,
    passengers,
    totals,
    editPurchaser,
    editSeatPassenger,
  } = useBooking()

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !trip || !totals) return <PageLoader />

  const steps: BookingStep[] = [
    { label: 'Trip', detail: 'Trip' },
    { label: 'Seat', detail: t('Seat ({id})', { id: seatCodes(seats) }) },
    { label: 'Passenger Details', detail: 'Passenger Details' },
    { label: 'Checkout', detail: 'Checkout' },
    { label: 'Payment', detail: 'Payment' },
  ]

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'Booking', to: ROUTES.seatSelection },
          { label: 'Passenger Details' },
        ]}
      />
      <div className="mt-space-md">
        <BookingProgress steps={steps} current={2} variant="circles" />
      </div>

      <div className="mt-space-lg flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <h1 className="font-headline-xl text-headline-xl-mobile tracking-tight text-deep-river md:text-headline-lg">
            {t('Passenger Details')}
          </h1>
          <p className="text-body-md text-on-surface-variant">
            {t('Please enter traveler information for this crossing.')}
          </p>
        </div>
        <div className="self-start md:self-auto">
          <HoldCountdown />
        </div>
      </div>

      <div className="mt-space-md grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-7">
          <PassengerFormCard
            purchaser={purchaser}
            seats={seats}
            passengers={passengers}
            onPurchaser={editPurchaser}
            onPassenger={editSeatPassenger}
          />
        </div>
        <div className="lg:col-span-5 lg:self-stretch">
          <CrossingSummary
            trip={trip}
            seats={seats}
            totals={totals}
            canContinue={isBookingComplete(purchaser, seatIds, passengers)}
          />
        </div>
      </div>
    </div>
  )
}
