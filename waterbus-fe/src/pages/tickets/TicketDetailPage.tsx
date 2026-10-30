import { useState } from 'react'
import { Breadcrumb, CopyButton, ErrorState, PageLoader } from '../../components/ui'
import {
  BoardingGuidance,
  QrModal,
  TicketActions,
  TicketBoardingPass,
  TicketCorridorCard,
} from '../../features/tickets/components/TicketDetailParts'
import { useTicketDetail } from '../../features/tickets/hooks/useTickets'
import { useDocumentTitle } from '../../hooks'
import { ROUTES } from '../../routes/routes'
import { t } from '../../i18n'

export default function TicketDetailPage() {
  useDocumentTitle('Ticket Detail')
  const { ticket, loading, error, retry } = useTicketDetail()
  const [qrOpen, setQrOpen] = useState(false)

  if (error) return <ErrorState onRetry={retry} />

  if (loading || !ticket) return <PageLoader />

  return (
    <div className="mx-auto w-full max-w-7xl px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        className="print:hidden"
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Ticket Detail' },
        ]}
      />

      <div className="mt-space-md flex flex-col justify-between gap-space-md md:flex-row md:items-end">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="font-headline-xl text-4xl font-bold tracking-tight text-deep-river">
              {t('Your Ticket')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-secondary-container px-3 py-1 text-xs font-semibold text-on-secondary-container">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {ticket.status}
            </span>
          </div>
          <p className="mt-1 text-body-md text-on-surface-variant">
            {t('Present this QR ticket when boarding your Smart Waterbus trip.')}
          </p>
        </div>
        <div className="flex items-center gap-3 self-start rounded-xl bg-white px-space-md py-2.5 shadow-sm md:self-auto">
          <span className="text-xs text-on-surface-variant">{t('Ref:')}</span>
          <span className="font-mono text-sm font-semibold tracking-wide text-deep-river">
            {ticket.bookingRef}
          </span>
          <CopyButton value={ticket.bookingRef} />
        </div>
      </div>

      <div className="mt-space-lg grid grid-cols-1 items-start gap-space-lg lg:grid-cols-12">
        <div className="lg:col-span-7">
          <TicketBoardingPass ticket={ticket} onEnlarge={() => setQrOpen(true)} />
        </div>
        <div className="space-y-space-md print:hidden lg:col-span-5">
          <TicketCorridorCard ticket={ticket} />
          <TicketActions tripId={ticket.tripId} />
        </div>
      </div>

      <div className="mt-space-lg print:hidden">
        <BoardingGuidance ticket={ticket} />
      </div>

      {qrOpen && <QrModal ticket={ticket} onClose={() => setQrOpen(false)} />}
    </div>
  )
}
