import { useState } from 'react'
import { Breadcrumb, ErrorState, PageLoader } from '../../components/ui'
import {
  AssistantChat,
  BookingSummaryCard,
  ManageOptions,
  OperatingBanner,
  OptionPanel,
} from '../../features/tickets/components/ManageParts'
import type { ManageOption } from '../../features/tickets/components/ManageParts'
import { useManageBooking } from '../../features/tickets/hooks/useTickets'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function ManageBookingPage() {
  useDocumentTitle('Manage Booking')
  const { manage, loading, error, retry } = useManageBooking()
  const [option, setOption] = useState<ManageOption>('change')

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
          <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river">
            {t('Manage Your Booking')}
          </h1>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {t('Review your trip details and choose the support action you need.')}
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
        <OperatingBanner vessel={manage.ticket.tripCode} />
      </div>

      <div className="mt-space-xl">
        <ManageOptions active={option} onSelect={setOption} />
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-7">
          <OptionPanel option={option} manage={manage} />
        </div>
        <div className="lg:col-span-5">
          <AssistantChat />
        </div>
      </div>
    </div>
  )
}
