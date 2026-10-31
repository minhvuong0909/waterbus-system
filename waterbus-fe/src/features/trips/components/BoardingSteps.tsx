import type { TripDetail } from '../types'
import { t } from '../../../i18n'
import SectionCard from './SectionCard'

export default function BoardingSteps({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="qr_code_2" title={t('Boarding & E-Ticket')}>
      <div className="grid grid-cols-1 gap-space-sm sm:grid-cols-3">
        {trip.boardingSteps.map((s, i) => (
          <div key={s.title} className="rounded-xl bg-mist/70 p-space-md">
            <div className="font-display text-headline-sm text-teal-flow">{i + 1}</div>
            <div className="font-display text-headline-sm text-deep-river">{s.title}</div>
            <p className="mt-1 text-body-sm text-on-surface-variant">{s.description}</p>
          </div>
        ))}
      </div>
    </SectionCard>
  )
}
