import { Link } from 'react-router-dom'
import QrCode from '../../../components/ticket/QrCode'
import { Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { formatVndSuffix } from '../../../utils/format'
import { t } from '../../../i18n'

export interface BoardingPassData {
  tripCode: string
  lineName: string
  departTime: string
  departPier: string
  arriveTime: string
  arrivePier: string
  durationMins: number
  seat: string
  passenger: string
  status: string
  totalVnd: number
  reference: string
  seatNote: string
}

/** Boarding pass: dark header, journey + facts on the left, QR on the right. */
export default function BoardingPassCard({ pass }: { pass: BoardingPassData }) {
  return (
    <div className="overflow-hidden rounded-2xl bg-surface-container-lowest shadow-[0_4px_24px_rgba(13,37,56,0.08)]">
      <div className="flex items-center justify-between bg-deep-river px-space-lg py-space-md text-on-primary">
        <div className="flex items-center gap-3">
          <Icon name="directions_boat" className="text-[22px]" />
          <div>
            <div className="font-headline-sm text-sm font-bold">
              {t('Trip {tripCode}', { tripCode: pass.tripCode })}
            </div>
            <div className="text-[11px] text-sand-light/80">{pass.lineName}</div>
          </div>
        </div>
        <span className="inline-flex items-center gap-1 rounded-full bg-teal-flow px-3 py-1 text-xs font-semibold">
          <Icon name="check_circle" className="text-[14px]" />
          {pass.status}
        </span>
      </div>

      <div className="grid grid-cols-1 gap-space-md p-space-lg md:grid-cols-5">
        <div className="md:col-span-3">
          <div className="flex gap-3">
            <div className="flex flex-col items-center pt-2">
              <span className="h-2.5 w-2.5 rounded-full bg-teal-flow" />
              <span className="my-1 w-0.5 flex-1 bg-teal-flow/30" />
              <span className="h-2.5 w-2.5 rounded-full bg-deep-river" />
            </div>
            <div className="space-y-space-md">
              <div>
                <span className="font-headline-sm text-3xl font-bold text-deep-river">
                  {pass.departTime}
                </span>
                <span className="ml-2 text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                  {t('Departure')}
                </span>
                <div className="font-headline-sm text-lg font-semibold text-deep-river">
                  {pass.departPier}
                </div>
                <span className="mt-1 inline-flex items-center gap-1 rounded-full bg-secondary-container/60 px-2.5 py-0.5 text-[11px] text-teal-flow">
                  <Icon name="schedule" className="text-[13px]" />
                  {t('{durationMins} min river crossing', { durationMins: pass.durationMins })}
                </span>
              </div>
              <div>
                <span className="font-headline-sm text-3xl font-bold text-deep-river">
                  {pass.arriveTime}
                </span>
                <span className="ml-2 text-[10px] font-semibold uppercase tracking-wider text-on-surface-variant">
                  {t('Arrival')}
                </span>
                <div className="font-headline-sm text-lg font-semibold text-deep-river">
                  {pass.arrivePier}
                </div>
              </div>
            </div>
          </div>

          <dl className="mt-space-md grid grid-cols-2 gap-space-sm rounded-xl bg-mist/70 p-space-md text-sm">
            <div>
              <dt className="text-[11px] text-on-surface-variant">{t('Seat')}</dt>
              <dd className="font-semibold text-teal-flow">{pass.seat}</dd>
            </div>
            <div>
              <dt className="text-[11px] text-on-surface-variant">{t('Passenger')}</dt>
              <dd className="truncate font-semibold text-deep-river">{pass.passenger}</dd>
            </div>
            <div>
              <dt className="text-[11px] text-on-surface-variant">{t('Status')}</dt>
              <dd className="font-semibold text-teal-flow">{pass.status}</dd>
            </div>
            <div>
              <dt className="text-[11px] text-on-surface-variant">{t('Total Fare')}</dt>
              <dd className="font-semibold text-deep-river">{formatVndSuffix(pass.totalVnd)}</dd>
            </div>
          </dl>
        </div>

        <div className="flex flex-col items-center justify-center rounded-xl bg-mist/70 p-space-md text-center md:col-span-2">
          <div className="mb-2 text-[10px] font-bold uppercase tracking-wider text-on-surface-variant">
            {t('Boarding QR')}
          </div>
          <div className="rounded-xl bg-white p-2 shadow-sm">
            <QrCode value={pass.reference} size={132} />
          </div>
          <div className="mt-2 font-mono text-[11px] font-semibold text-deep-river">
            {pass.reference}
          </div>
          <div className="text-[11px] text-on-surface-variant">{pass.seatNote}</div>
        </div>
      </div>

      <div className="relative border-t border-dashed border-outline-variant px-space-lg py-space-md">
        <span className="absolute -left-3 -top-3 h-6 w-6 rounded-full bg-surface" />
        <span className="absolute -right-3 -top-3 h-6 w-6 rounded-full bg-surface" />
        <div className="flex flex-col items-start justify-between gap-space-sm sm:flex-row sm:items-center">
          <span className="flex items-center gap-2 text-xs text-on-surface-variant">
            <Icon name="directions_boat" className="text-[16px]" />
            {t('Saigon Central River Crossing')}
          </span>
          <div className="flex gap-space-sm">
            <Link
              to={ROUTES.ticketDetail}
              className="inline-flex items-center gap-2 rounded-lg bg-surface-container px-4 py-2 text-xs font-semibold text-deep-river transition-colors hover:bg-surface-container-high"
            >
              <Icon name="qr_code_2" className="text-[16px]" />
              {t('Access QR Ticket')}
            </Link>
            <Link
              to={ROUTES.myTickets}
              className="inline-flex items-center gap-2 rounded-lg bg-teal-flow px-4 py-2 text-xs font-semibold text-on-primary transition-colors hover:bg-secondary"
            >
              <Icon name="confirmation_number" className="text-[16px]" />
              {t('View My Ticket')}
            </Link>
          </div>
        </div>
      </div>
    </div>
  )
}
