import mapImage from '../../../assets/images/corridor-map.jpg'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

const NOTES = [
  {
    icon: 'schedule',
    title: 'Punctual Pier Boarding:',
    text: 'Turnstiles open 10 min prior to departure. Boarding gate closes 3 min before cast-off.',
  },
  {
    icon: 'qr_code_scanner',
    title: 'Instant Digital QR Pass:',
    text: 'Digital ticket pass generated immediately upon booking for tap-and-go turnstiles.',
  },
  {
    icon: 'commute',
    title: 'Transit Efficiency:',
    text: '~12 min average crossing time avoiding all bridge congestion between D1 and Thu Thiem.',
  },
]

export default function CorridorSidebar() {
  return (
    <aside className="sticky top-28 space-y-space-md rounded-2xl border border-outline-variant/40 bg-sand-light/70 p-space-md lg:col-span-4">
      <div className="flex items-start justify-between gap-2">
        <div>
          <span className="text-[10px] font-semibold uppercase tracking-wider text-outline">
            {t('Corridor Overview')}
          </span>
          <h3 className="font-headline-sm text-base font-bold leading-tight text-deep-river">
            {t('Corridor Crossing Overview')}
          </h3>
        </div>
        <span className="rounded-full bg-white px-3 py-1 text-[11px] font-semibold text-teal-flow shadow-sm">
          {t('Line 1 Express • Calm River Flow')}
        </span>
      </div>

      <div className="relative h-40 overflow-hidden rounded-xl border border-teal-flow/15 bg-white">
        <img
          alt={t('Bach Dang to Thu Thiem river crossing map')}
          src={mapImage}
          className="h-full w-full object-cover"
        />
        <div className="absolute inset-x-3 bottom-3 flex items-center justify-between rounded-lg bg-white/95 px-3 py-1.5 text-[11px] font-semibold text-deep-river shadow-sm">
          <span>{t('1.2 km navigable river crossing')}</span>
          <span className="text-teal-flow">{t('WB-01 In Transit • 42 km/h')}</span>
        </div>
      </div>

      <ul className="space-y-space-sm rounded-xl bg-white p-space-md text-xs text-on-surface-variant">
        {NOTES.map((n) => (
          <li key={n.title} className="flex items-start gap-2">
            <Icon name={n.icon} className="text-[18px] text-teal-flow" />
            <span>
              <strong className="text-deep-river">{t(n.title)}</strong> {t(n.text)}
            </span>
          </li>
        ))}
      </ul>

      <div className="flex items-center gap-3 rounded-xl bg-deep-river p-space-md text-on-primary">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-white/10 text-sky-aqua">
          <Icon name="support_agent" className="text-[22px]" />
        </div>
        <div className="min-w-0 flex-1">
          <div className="font-headline-sm text-xs font-bold">{t('Waterway Assistant')}</div>
          <p className="text-[11px] text-sand-light/80">
            {t('Need pier transfers or Sala connections?')}
          </p>
        </div>
        <button
          type="button"
          className="inline-flex shrink-0 items-center gap-1 rounded-full bg-teal-flow px-3 py-2 text-[11px] font-bold text-on-primary transition-colors hover:bg-secondary"
        >
          {t('Ask Pier Concierge')}
          <Icon name="send" className="text-[13px]" />
        </button>
      </div>
    </aside>
  )
}
