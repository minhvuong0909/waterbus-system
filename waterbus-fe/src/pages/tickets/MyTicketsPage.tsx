import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Breadcrumb, ErrorState, Icon, PageLoader } from '../../components/ui'
import {
  NextDepartureCard,
  PastJourneyRow,
  UpcomingTripCard,
} from '../../features/tickets/components/WalletParts'
import { useTicketWallet } from '../../features/tickets/hooks/useTickets'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

type Tab = 'upcoming' | 'past'

export default function MyTicketsPage() {
  useDocumentTitle('My Tickets')
  const { wallet, loading, error, retry, openTicket } = useTicketWallet()
  const [tab, setTab] = useState<Tab>('upcoming')

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !wallet) return <PageLoader />

  const upcomingCount = 1 + wallet.later.length
  const tabClass = (active: boolean) =>
    `flex items-center gap-2 rounded-full px-5 py-2 text-sm transition-all ${
      active
        ? 'bg-deep-river font-semibold text-on-primary'
        : 'text-on-surface-variant hover:text-deep-river'
    }`
  const badge = (active: boolean) =>
    `rounded-full px-1.5 text-[11px] ${
      active ? 'bg-teal-flow text-white' : 'bg-surface-container text-on-surface-variant'
    }`

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb items={[{ label: 'Home', to: ROUTES.home }, { label: 'My Tickets' }]} />

      <div className="mt-space-sm flex flex-col justify-between gap-space-md md:flex-row md:items-start">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river">
              {t('My Tickets')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {t('{upcomingCount} Active Bookings', { upcomingCount })}
            </span>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {t('View your upcoming and previous Smart Waterbus journeys.')}
          </p>
        </div>
        <Link
          to={ROUTES.search}
          className="inline-flex items-center gap-2 self-start rounded-xl border border-outline-variant/50 bg-white px-5 py-2.5 text-sm font-semibold text-deep-river shadow-sm transition-colors hover:bg-surface-container-low"
        >
          <Icon name="add" className="text-[18px]" />
          {t('Book New Trip')}
        </Link>
      </div>

      <div role="tablist" className="mt-space-lg inline-flex rounded-full bg-white p-1 shadow-sm">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'upcoming'}
          onClick={() => setTab('upcoming')}
          className={tabClass(tab === 'upcoming')}
        >
          {t('Upcoming')} <span className={badge(tab === 'upcoming')}>{upcomingCount}</span>
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'past'}
          onClick={() => setTab('past')}
          className={tabClass(tab === 'past')}
        >
          {t('Past Journeys')} <span className={badge(tab === 'past')}>{wallet.past.length}</span>
        </button>
      </div>

      {tab === 'upcoming' ? (
        <>
          <div className="mt-space-lg flex items-center justify-between text-[11px] font-bold uppercase tracking-wider">
            <span className="flex items-center gap-2 text-teal-flow">
              <span className="h-2 w-2 rounded-full bg-teal-flow" /> {t('Next Departure • Today')}
            </span>
            <span className="font-medium normal-case tracking-normal text-on-surface-variant">
              {t('Boarding Pier {gate}', { gate: wallet.next.gate })}
            </span>
          </div>
          <div className="mt-space-sm">
            <NextDepartureCard ticket={wallet.next} onOpen={openTicket} />
          </div>

          <div className="mt-space-2xl flex items-end justify-between">
            <div>
              <h2 className="font-headline-lg text-headline-md text-deep-river">
                {t('Later This Week')}
              </h2>
              <p className="text-xs text-on-surface-variant">
                {t('Scheduled upcoming river voyages')}
              </p>
            </div>
            <span className="text-xs font-semibold text-teal-flow">
              {t('{length} Trips Scheduled', { length: wallet.later.length })}
            </span>
          </div>
          <div className="mt-space-sm grid grid-cols-1 gap-space-md md:grid-cols-2">
            {wallet.later.map((ticket) => (
              <UpcomingTripCard key={ticket.id} ticket={ticket} onOpen={openTicket} />
            ))}
          </div>
        </>
      ) : null}

      <div className="mt-space-2xl flex items-end justify-between">
        <div>
          <h2 className="font-headline-lg text-headline-md text-deep-river">
            {t('Recent Past Journeys')}
          </h2>
          <p className="text-xs text-on-surface-variant">{t('Completed river transit records')}</p>
        </div>
        {tab === 'upcoming' && (
          <button
            type="button"
            onClick={() => setTab('past')}
            className="text-xs font-semibold text-teal-flow hover:underline"
          >
            {t('View all past trips ({length}) →', { length: wallet.past.length })}
          </button>
        )}
      </div>
      <div className="mt-space-sm grid grid-cols-1 gap-space-md md:grid-cols-2">
        {wallet.past.map((ticket) => (
          <PastJourneyRow key={ticket.id} ticket={ticket} onOpen={openTicket} />
        ))}
      </div>

      <div className="mt-space-2xl flex flex-col items-start justify-between gap-space-md rounded-2xl border border-teal-flow/20 bg-sand-light/60 p-space-md sm:flex-row sm:items-center">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-teal-flow text-on-primary">
            <Icon name="lightbulb" className="text-[22px]" />
          </span>
          <div>
            <div className="text-sm font-semibold text-deep-river">
              {t('Smart Waterbus Travel Tip')}
            </div>
            <p className="text-xs text-on-surface-variant">
              {t(
                'Your QR ticket is always accessible online right up to boarding. Simply present your screen at the pier turnstile scanners.',
              )}
            </p>
          </div>
        </div>
        <Link
          to={ROUTES.explore}
          className="flex items-center gap-1 whitespace-nowrap text-xs font-semibold text-teal-flow hover:underline"
        >
          {t('Pier Terminal Guide')} <Icon name="arrow_forward" className="text-[16px]" />
        </Link>
      </div>
    </div>
  )
}
