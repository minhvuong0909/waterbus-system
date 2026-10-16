import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'

const NOTES = [
  {
    icon: 'timer',
    title: 'Seat Hold:',
    text: 'Your seat is held for 10 minutes while you complete payment.',
  },
  {
    icon: 'qr_code_scanner',
    title: 'Digital QR Pass:',
    text: 'Your e-ticket is issued as soon as the payment is confirmed.',
  },
]

/** Short booking rules next to the departures; the full details live on the later steps. */
export default function CorridorSidebar() {
  return (
    <aside className="sticky top-28 space-y-space-sm rounded-2xl border border-outline-variant/40 bg-white p-space-md lg:col-span-4">
      <h3 className="font-headline-sm text-base font-bold text-deep-river">
        {t('Before you book')}
      </h3>
      <ul className="space-y-space-sm text-sm text-on-surface-variant">
        {NOTES.map((n) => (
          <li key={n.title} className="flex items-start gap-2">
            <Icon name={n.icon} className="text-[20px] text-teal-flow" />
            <span>
              <strong className="text-deep-river">{t(n.title)}</strong> {t(n.text)}
            </span>
          </li>
        ))}
      </ul>
    </aside>
  )
}
