import OtherDeviceLookup from '../../features/tickets/components/OtherDeviceLookup'
import { Breadcrumb, Button, ErrorState, Icon, PageLoader } from '../../components/ui'
import NextDepartureCard from '../../features/tickets/components/NextDepartureCard'
import PastJourneyRow from '../../features/tickets/components/PastJourneyRow'
import UpcomingTripCard from '../../features/tickets/components/UpcomingTripCard'
import { useTicketWallet } from '../../features/tickets/hooks/useTickets'
import { useAuth } from '../../features/authentication/hooks/useAuth'
import { isPassenger } from '../../features/authentication/utils'
import { groupByBooking, isTicketToday } from '../../features/tickets/utils'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function MyTicketsPage() {
  useDocumentTitle('My Tickets')
  const { wallet, loading, error, retry, openTicket } = useTicketWallet()
  // Passenger đã đăng nhập thấy vé của tài khoản trên mọi thiết bị; Guest chỉ thấy vé mua trên máy này.
  const hasAccount = isPassenger(useAuth().user)
  const orderHistoryLink = hasAccount && (
    <Button to={ROUTES.orderHistory} variant="text" size="sm" icon="receipt_long">
      {t('Order history')}
    </Button>
  )

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !wallet) return <PageLoader />

  if (!wallet.next && wallet.past.length === 0)
    return (
      <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
        <Breadcrumb items={[{ label: 'Home', to: ROUTES.home }, { label: 'My Tickets' }]} />
        <div className="mx-auto mt-space-xl flex max-w-lg flex-col items-center rounded-2xl border border-outline-variant/40 bg-white p-space-xl text-center shadow-sm">
          <span className="flex h-16 w-16 items-center justify-center rounded-full bg-secondary-container text-on-secondary-container">
            <Icon name="confirmation_number" className="text-3xl" />
          </span>
          <h1 className="mt-space-md font-display text-headline-md text-deep-river">
            {hasAccount ? t('No tickets yet') : t('No tickets on this device yet')}
          </h1>
          <p className="mt-space-xs text-body-md text-on-surface-variant">
            {hasAccount
              ? t('Tickets of the bookings in your account appear here once payment is confirmed.')
              : t(
                  'Tickets you buy in this browser appear here once payment is confirmed — no account needed.',
                )}
          </p>
          <Button to={ROUTES.searchResults} className="mt-space-lg w-full sm:w-auto">
            {t('Find a trip')}
          </Button>
          {orderHistoryLink && <div className="mt-space-xs">{orderHistoryLink}</div>}
          <div className="mt-space-xl w-full border-t border-outline-variant/40 pt-space-lg">
            <OtherDeviceLookup />
          </div>
        </div>
      </div>
    )

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb items={[{ label: 'Home', to: ROUTES.home }, { label: 'My Tickets' }]} />
      <div className="mt-space-sm flex flex-wrap items-center justify-between gap-space-sm">
        <h1 className="font-display text-4xl font-bold tracking-tight text-deep-river">
          {t('My Tickets')}
        </h1>
        {orderHistoryLink}
      </div>

      {wallet.next && (
        <section className="mt-space-lg">
          <h2 className="flex items-center gap-2 text-2xs font-bold uppercase tracking-wider text-teal-flow">
            <span className="h-2 w-2 rounded-full bg-teal-flow" />
            {isTicketToday(wallet.next) ? t('Next Departure • Today') : t('Next Departure')}
          </h2>
          <div className="mt-space-sm">
            <NextDepartureCard ticket={wallet.next} onOpen={openTicket} />
          </div>
        </section>
      )}

      {wallet.later.length > 0 && (
        <section className="mt-space-2xl">
          <h2 className="font-display text-headline-md text-deep-river">{t('Later trips')}</h2>
          <div className="mt-space-sm grid grid-cols-1 gap-space-md md:grid-cols-2">
            {groupByBooking(wallet.later).map((group) =>
              group.length === 1 ? (
                <UpcomingTripCard key={group[0].id} ticket={group[0]} onOpen={openTicket} />
              ) : (
                <section
                  key={group[0].bookingRef}
                  aria-label={t('Booking {ref}', { ref: group[0].bookingRef })}
                  className="rounded-2xl border border-teal-flow/30 bg-secondary-container/20 p-space-sm md:col-span-2"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2 px-space-xs pb-space-sm text-xs font-semibold text-deep-river">
                    <span>{t('Booking {ref}', { ref: group[0].bookingRef })}</span>
                    <span className="text-on-surface-variant">
                      {t('{count} passengers, checked in individually', { count: group.length })}
                    </span>
                  </div>
                  <div className="grid grid-cols-1 gap-space-md md:grid-cols-2">
                    {group.map((ticket) => (
                      <UpcomingTripCard key={ticket.id} ticket={ticket} onOpen={openTicket} />
                    ))}
                  </div>
                </section>
              ),
            )}
          </div>
        </section>
      )}

      {wallet.past.length > 0 && (
        <section className="mt-space-2xl">
          <h2 className="font-display text-headline-md text-deep-river">{t('Past Journeys')}</h2>
          <div className="mt-space-sm grid grid-cols-1 gap-space-md md:grid-cols-2">
            {wallet.past.map((ticket) => (
              <PastJourneyRow key={ticket.id} ticket={ticket} onOpen={openTicket} />
            ))}
          </div>
        </section>
      )}

      <section className="mt-space-2xl rounded-2xl border border-outline-variant/40 bg-white p-space-md shadow-sm">
        <OtherDeviceLookup />
      </section>
    </div>
  )
}
