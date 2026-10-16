import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import { useBooking } from '../../features/booking/hooks/useBooking'
import {
  bookingReference,
  cabinsLabel,
  passengersLabel,
  seatCodes,
  ticketFares,
} from '../../features/booking/utils'
import {
  ConfirmationHero,
  ConfirmationSteps,
  QuickActions,
  WhatsNext,
} from '../../features/payment/components/ConfirmationParts'
import { getBookingQr } from '../../features/booking/services/bookingService'
import { useOrderConfirmation } from '../../features/payment/hooks/useOrderConfirmation'
import { getNextSteps } from '../../features/payment/services/paymentService'
import BoardingPassCard from '../../features/tickets/components/BoardingPassCard'
import { addBooking } from '../../features/tickets/ticketsSlice'
import { bookingSnapshotId } from '../../features/tickets/utils'
import { useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppDispatch, useAppSelector } from '../../store/hooks'
import { t } from '../../i18n'

/** Shown only once the backend reports the order as paid (BR-PAY-01), never from the browser alone. */
export default function BookingSuccessPage() {
  useDocumentTitle('Booking Confirmed')
  const dispatch = useAppDispatch()
  const { query, selectedTrip, passengers } = useAppSelector((s) => s.booking)
  const { loading, error, retry, trip, seats, purchaser, totals } = useBooking()
  const steps = useFetch(getNextSteps)
  const { phase, detail } = useOrderConfirmation()
  const confirmed = phase === 'confirmed'
  const bookingId = detail?.bookings[0]?.bookingId
  const order = useAppSelector((s) => s.booking.order)
  // The boarding QR is issued only for a paid booking (BR-COM-08); the scanner verifies it offline.
  const qr = useFetch(
    () =>
      confirmed && order && bookingId
        ? getBookingQr(order.orderId, bookingId, order.accessToken)
        : Promise.resolve(null),
    [confirmed, bookingId],
  )
  const failure = error ?? steps.error ?? qr.error

  // The tickets of the confirmed order go into the wallet (names by seat, as the traveler typed them).
  useEffect(() => {
    if (!confirmed || !trip || seats.length === 0) return
    dispatch(
      addBooking({
        id: bookingSnapshotId(trip.id, seats),
        tripId: trip.id,
        selectedTrip,
        query,
        seats,
        fares: ticketFares(trip.fareVnd, seats),
        passengerNames: seats.map((seat) => passengers[seat.id]?.fullName ?? ''),
      }),
    )
  }, [confirmed, trip, seats, selectedTrip, query, passengers, dispatch])

  if (failure) {
    return (
      <ErrorState
        onRetry={() => {
          if (error) retry()
          if (steps.error) steps.retry()
          if (qr.error) qr.retry()
        }}
      />
    )
  }

  if (phase === 'none' || phase === 'failed' || phase === 'timeout') {
    return (
      <div className="mx-auto max-w-lg px-margin py-space-3xl text-center">
        <h1 className="font-headline-md text-headline-md text-deep-river">
          {t('Your booking is not confirmed')}
        </h1>
        <p className="mt-space-sm text-body-md text-on-surface-variant">
          {t('We have not received a confirmed payment for this order.')}
        </p>
        <Link
          to={ROUTES.payment}
          className="mt-space-md inline-flex rounded-full bg-teal-flow px-6 py-2.5 font-semibold text-on-primary"
        >
          {t('Back to payment')}
        </Link>
      </div>
    )
  }

  if (loading || !trip || !totals || !steps.data || !confirmed || qr.loading) return <PageLoader />

  const orderedSeats = detail?.bookings.flatMap((b) => b.tickets) ?? []
  const reference = detail?.bookings[0]?.bookingCode ?? bookingReference(seats)
  const pier = trip.originPierName
  const nextSteps = steps.data.map((s) => ({
    ...s,
    title: s.title.replace('{pier}', pier),
    description: s.description.replace('{pier}', pier),
    chipLabel: s.chipLabel.replace('{pier}', pier),
  }))
  // Names as the backend issued them; the typed names stand in for a mock order.
  const names =
    orderedSeats.length > 0
      ? orderedSeats.map((ticket) => ticket.passengerName)
      : seats.map((seat) => passengers[seat.id]?.fullName ?? '')

  return (
    <div>
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-space-sm px-margin py-space-sm">
        <Breadcrumb
          items={[
            { label: 'Home', to: ROUTES.home },
            { label: 'Booking', to: ROUTES.seatSelection },
            { label: 'Confirmation' },
          ]}
        />
        <ConfirmationSteps />
      </div>

      <div className="mx-auto max-w-7xl px-margin pb-space-2xl pt-space-lg">
        <ConfirmationHero reference={reference} email={purchaser.email} />

        <div className="mt-space-2xl grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
          <div className="lg:col-span-7">
            <BoardingPassCard
              pass={{
                tripCode: trip.vesselCode,
                lineName: t('Central Waterway Line'),
                departTime: trip.departTime,
                departPier: trip.originPierName,
                arriveTime: trip.arriveTime,
                arrivePier: trip.destinationPierName,
                durationMins: trip.durationMins,
                seat:
                  seats.length === 1
                    ? t('Seat {id}', { id: seats[0].code })
                    : t('Seats {ids}', { ids: seatCodes(seats) }),
                passenger: `${passengersLabel(seats.length)}: ${names.join(', ')}`,
                status: t('Confirmed'),
                totalVnd: totals.totalVnd,
                reference,
                qrValue: qr.data?.payload,
                seatNote: t('Trip {code} • {kind}', {
                  code: trip.vesselCode,
                  kind: cabinsLabel(seats),
                }),
              }}
            />
          </div>
          <div className="lg:col-span-5">
            <QuickActions />
          </div>
        </div>
      </div>

      <WhatsNext steps={nextSteps} />
    </div>
  )
}
