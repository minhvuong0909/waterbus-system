import { Icon } from '../../../components/ui'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

export default function BoardingGuidance({ ticket }: { ticket: Ticket }) {
  return (
    <div className="flex flex-col items-start justify-between gap-space-md rounded-2xl bg-sand-light/70 p-space-md sm:flex-row sm:items-center">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-secondary-container text-teal-flow">
          <Icon name="confirmation_number" className="text-2xl" />
        </span>
        <div>
          <div className="font-display text-base font-bold text-deep-river">
            {t('Boarding Guidance')}
          </div>
          <ul className="flex flex-col gap-x-6 text-xs text-on-surface-variant sm:flex-row">
            <li className="flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {t('Keep your QR ticket ready on your screen before boarding.')}
            </li>
            <li className="flex items-center gap-1.5">
              <span className="h-1.5 w-1.5 rounded-full bg-teal-flow" />
              {t('Present the QR ticket to staff when boarding at {departPier}.', {
                departPier: ticket.departPier,
              })}
            </li>
          </ul>
        </div>
      </div>
      <span className="inline-flex items-center gap-1.5 rounded-lg bg-white px-3 py-2 text-xs text-deep-river shadow-sm">
        <Icon name="help_outline" className="text-sm text-teal-flow" />
        {t('Board at {pier}', { pier: ticket.departPier })}
      </span>
    </div>
  )
}
