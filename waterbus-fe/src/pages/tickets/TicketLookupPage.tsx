import { Breadcrumb } from '../../components/ui'
import TicketLookupFlow from '../../features/tickets/components/TicketLookupFlow'
import { useDocumentTitle } from '../../hooks'
import { t } from '../../i18n'
import { ROUTES } from '../../routes/routes'

/** Guest ticket lookup: booking code + purchaser email, proven with a one-time code sent to that email. */
export default function TicketLookupPage() {
  useDocumentTitle('Find my tickets')
  return (
    <div className="mx-auto w-full max-w-lg px-margin pb-space-3xl pt-space-md">
      <Breadcrumb
        items={[
          { label: 'Home', to: ROUTES.home },
          { label: 'My Tickets', to: ROUTES.myTickets },
          { label: 'Find my tickets' },
        ]}
      />
      <h1 className="mb-space-xs mt-space-lg font-display text-headline-md text-deep-river">{t('Find my tickets')}</h1>
      <p className="mb-space-lg text-body-md text-on-surface-variant">
        {t('Enter the email you booked with and the booking code from the confirmation email. We will send you a one-time code to confirm it is you.')}
      </p>
      <TicketLookupFlow />
    </div>
  )
}
