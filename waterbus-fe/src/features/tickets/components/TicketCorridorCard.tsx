import { Icon } from '../../../components/ui'
import type { Ticket } from '../types'
import { t } from '../../../i18n'

export default function TicketCorridorCard({ ticket }: { ticket: Ticket }) {
  return (
    <div className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <div className="mb-space-md flex items-center justify-between">
        <h2 className="flex items-center gap-2 font-headline-sm text-lg font-bold text-deep-river">
          <Icon name="explore" className="text-[20px] text-teal-flow" />
          {t('River Crossing Corridor')}
        </h2>
        <span className="rounded bg-secondary-container px-2 py-0.5 text-[10px] font-bold text-teal-flow">
          {t('{tripCode} Route', { tripCode: ticket.tripCode })}
        </span>
      </div>
      <div className="relative h-48 overflow-hidden rounded-xl bg-[#E6EFEC]">
        <svg
          className="absolute inset-0 h-full w-full"
          viewBox="0 0 400 190"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path
            d="M0 40 C 120 50 200 100 300 130 C 340 142 370 160 400 190 L 0 190 Z"
            fill="#4FC3D8"
            opacity="0.35"
          />
          <path
            d="M60 50 C 160 70 240 108 340 150"
            fill="none"
            stroke="#147A7E"
            strokeWidth="3"
            strokeDasharray="6 6"
          />
        </svg>
        <span className="absolute left-3 top-3 rounded-lg bg-white/95 px-2.5 py-1 text-[11px] shadow-sm">
          <b className="text-deep-river">{ticket.departPier}</b>
          <span className="block text-on-surface-variant">
            {t('{departDistrict} • Departure', { departDistrict: ticket.departDistrict })}
          </span>
        </span>
        <span className="absolute left-1/2 top-1/2 flex -translate-x-1/2 -translate-y-1/2 items-center gap-1.5 rounded-full bg-deep-river px-3 py-1.5 text-[11px] font-bold text-white shadow-lg">
          <Icon name="directions_boat" className="text-[14px]" />
          {t('Waterbus {tripCode}', { tripCode: ticket.tripCode })}
        </span>
        <span className="absolute bottom-3 right-3 rounded-lg bg-white/95 px-2.5 py-1 text-[11px] shadow-sm">
          <b className="text-deep-river">{ticket.arrivePier}</b>
          <span className="block text-on-surface-variant">
            {t('{arriveDistrict} • Arrival', { arriveDistrict: ticket.arriveDistrict })}
          </span>
        </span>
      </div>
      <div className="mt-space-md grid grid-cols-3 gap-2 text-center">
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Scheduled Crossing')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {t('{durationMins} Minutes', { durationMins: ticket.durationMins })}
          </div>
        </div>
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Corridor Line')}</div>
          <div className="text-sm font-semibold text-deep-river">{t('Direct Line')}</div>
        </div>
        <div className="rounded-lg bg-mist p-2">
          <div className="text-[10px] text-on-surface-variant">{t('Passenger')}</div>
          <div className="text-sm font-semibold text-deep-river">
            {t('1 Pass ({seat})', { seat: ticket.seat })}
          </div>
        </div>
      </div>
    </div>
  )
}
