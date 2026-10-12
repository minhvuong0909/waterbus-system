import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import { useBooking } from '../../features/booking/hooks/useBooking'
import { bookingReference, seatTags } from '../../features/booking/utils'
import {
  ConfirmationHero,
  ConfirmationSteps,
  CorridorCard,
  QuickActions,
  WaterwayBanner,
  WhatsNext,
} from '../../features/payment/components/ConfirmationParts'
import { getNextSteps } from '../../features/payment/services/paymentService'
import BoardingPassCard from '../../features/tickets/components/BoardingPassCard'
import { useDocumentTitle, useFetch } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function BookingSuccessPage() {
  useDocumentTitle('Booking Confirmed')
  const { loading, error, retry, trip, seat, seatId, passenger, totals } = useBooking()
  const steps = useFetch(getNextSteps)
  const failure = error ?? steps.error

  if (failure) {
    return (
      <ErrorState
        onRetry={() => {
          if (error) retry()
          if (steps.error) steps.retry()
        }}
      />
    )
  }

  if (loading || !trip || !totals || !steps.data) return <PageLoader />

  const reference = bookingReference(seatId)
  const pier = trip.originPierName
  const nextSteps = steps.data.map((s) => ({
    ...s,
    title: s.title.replace('{pier}', pier),
    description: s.description.replace('{pier}', pier),
    chipLabel: s.chipLabel.replace('{pier}', pier),
  }))

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
        <ConfirmationHero reference={reference} trip={trip} />

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
                seat: t('Seat {id}', { id: seatId }),
                passenger: t('1 Passenger ({name})', { name: passenger.fullName }),
                status: t('Confirmed'),
                totalVnd: totals.totalVnd,
                reference,
                seatNote: t('Trip {code} • {kind}', {
                  code: trip.vesselCode,
                  kind: seatTags(seat) || t('Aisle'),
                }),
              }}
            />
          </div>
          <div className="space-y-space-md lg:col-span-5">
            <CorridorCard trip={trip} />
            <QuickActions />
          </div>
        </div>
      </div>

      <WhatsNext steps={nextSteps} />

      <div className="mx-auto max-w-7xl px-margin py-space-2xl">
        <WaterwayBanner />
      </div>
    </div>
  )
}
