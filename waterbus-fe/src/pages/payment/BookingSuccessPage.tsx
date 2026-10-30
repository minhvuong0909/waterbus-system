import { Link, useNavigate } from 'react-router-dom'
import { Breadcrumb, Button, ErrorState, PageLoader } from '../../components/ui'
import { useSearchQuery } from '../../features/trips/hooks/useTripSearch'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { isRoundTrip } from '../../features/booking/legs'
import {
  bookingReference,
  cabinsLabel,
  passengersLabel,
  seatCodes,
} from '../../features/booking/utils'
import ConfirmationHero from '../../features/payment/components/ConfirmationHero'
import ConfirmationSteps from '../../features/payment/components/ConfirmationSteps'
import { getBookingQr } from '../../features/booking/services/bookingService'
import { useOrderConfirmation } from '../../features/payment/hooks/useOrderConfirmation'
import BoardingPassCard from '../../features/tickets/components/BoardingPassCard'
import { useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { useAppSelector } from '../../store/hooks'
import { t } from '../../i18n'
import NoTripSelected from '../../features/booking/components/NoTripSelected'

/** Shown only once the backend reports the order as paid (BR-PAY-01), never from the browser alone. */
export default function BookingSuccessPage() {
  useDocumentTitle('Booking Confirmed')
  const navigate = useNavigate()
  const { updateQuery } = useSearchQuery()
  const { query, passengers, returnLeg: returnState } = useAppSelector((s) => s.booking)
  const roundTrip = isRoundTrip(query)
  const { noTrip, loading, error, retry, trip, seats, purchaser, totals } = useBooking()
  const back = useBooking({ leg: 'return', enabled: roundTrip })
  const { phase, detail } = useOrderConfirmation()
  const confirmed = phase === 'confirmed'
  const bookingId = detail?.bookings[0]?.bookingId
  const returnBookingId = roundTrip ? detail?.bookings[1]?.bookingId : undefined
  const order = useAppSelector((s) => s.booking.order)
  // The boarding QR is issued only for a paid booking (BR-COM-08); the scanner verifies it offline.
  const qr = useFetch(
    () =>
      confirmed && order && bookingId
        ? getBookingQr(order.orderId, bookingId, order.accessToken)
        : Promise.resolve(null),
    [confirmed, bookingId],
  )
  // A round trip has two bookings, so two boarding QR codes (BR-QR-01).
  const returnQr = useFetch(
    () =>
      confirmed && order && returnBookingId
        ? getBookingQr(order.orderId, returnBookingId, order.accessToken)
        : Promise.resolve(null),
    [confirmed, returnBookingId],
  )
  const failure = error ?? back.error ?? qr.error ?? returnQr.error

  if (noTrip) return <NoTripSelected />
  if (failure) {
    return (
      <ErrorState
        onRetry={() => {
          if (error) retry()
          if (back.error) back.retry()
          if (qr.error) qr.retry()
          if (returnQr.error) returnQr.retry()
        }}
      />
    )
  }

  if (phase === 'none' || phase === 'failed' || phase === 'timeout' || phase === 'paidLate') {
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

  const returnReady = !roundTrip || (back.trip && back.totals && !returnQr.loading)
  if (loading || back.loading || !trip || !totals || !confirmed || qr.loading || !returnReady) {
    return <PageLoader />
  }

  const orderedSeats = (roundTrip ? detail?.bookings.slice(0, 1) : detail?.bookings)?.flatMap((b) => b.tickets) ?? []
  const returnTickets = roundTrip ? (detail?.bookings[1]?.tickets ?? []) : []
  const returnReference = roundTrip ? detail?.bookings[1]?.bookingCode : undefined
  const reference = detail?.bookings[0]?.bookingCode ?? bookingReference(seats)
  // Names as the backend issued them; the typed names stand in until the tickets are listed.
  const names =
    orderedSeats.length > 0
      ? orderedSeats.map((ticket) => ticket.passengerName)
      : seats.map((seat) => passengers[seat.id]?.fullName ?? '')
  const returnNames =
    returnTickets.length > 0
      ? returnTickets.map((ticket) => ticket.passengerName)
      : back.seats.map((seat) => returnState.passengers[seat.id]?.fullName ?? '')

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
        <ConfirmationHero
          reference={returnReference ? `${reference}, ${returnReference}` : reference}
          email={purchaser.email}
        />

        <div className="mt-space-2xl grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
          <div className="space-y-space-lg lg:col-span-7">
            {roundTrip && (
              <h2 className="font-headline-sm text-lg font-bold text-deep-river">{t('Outbound trip')}</h2>
            )}
            <BoardingPassCard
              pass={{
                tripCode: trip.vesselCode,
                lineName: trip.lineLabel,
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
            {roundTrip && back.trip && back.totals && (
              <>
                <h2 className="font-headline-sm text-lg font-bold text-deep-river">{t('Return trip')}</h2>
                <BoardingPassCard
                  pass={{
                    tripCode: back.trip.vesselCode,
                    lineName: back.trip.lineLabel,
                    departTime: back.trip.departTime,
                    departPier: back.trip.originPierName,
                    arriveTime: back.trip.arriveTime,
                    arrivePier: back.trip.destinationPierName,
                    durationMins: back.trip.durationMins,
                    seat:
                      back.seats.length === 1
                        ? t('Seat {id}', { id: back.seats[0].code })
                        : t('Seats {ids}', { ids: seatCodes(back.seats) }),
                    passenger: `${passengersLabel(back.seats.length)}: ${returnNames.join(', ')}`,
                    status: t('Confirmed'),
                    totalVnd: back.totals.totalVnd,
                    reference: returnReference ?? reference,
                    qrValue: returnQr.data?.payload,
                    seatNote: t('Trip {code} • {kind}', {
                      code: back.trip.vesselCode,
                      kind: cabinsLabel(back.seats),
                    }),
                  }}
                />
              </>
            )}
          </div>
          <div className="space-y-space-md lg:col-span-5">
            {!roundTrip && (
              <Button
                variant="secondary"
                className="w-full"
                onClick={() => {
                  updateQuery({ originId: query.destinationId, destinationId: query.originId })
                  navigate(ROUTES.searchResults)
                }}
              >
                {t('Book the return trip')}
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}
