import type { TripDetail } from '../types'
import { t } from '../../../i18n'
import SectionCard from './SectionCard'

const DOT: Record<TripDetail['timeline'][number]['tone'], string> = {
  teal: 'bg-teal-flow',
  amber: 'bg-signal-amber',
  dark: 'bg-deep-river',
}

export default function JourneyTimeline({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="schedule" title={t('Journey Timeline')} aside={trip.dateLabel}>
      <ol className="space-y-space-md">
        {trip.timeline.map((step) => (
          <li key={step.time + step.title} className="flex gap-space-sm">
            <span className={`mt-2 h-2.5 w-2.5 shrink-0 rounded-full ${DOT[step.tone]}`} />
            <div>
              <div className="font-display text-headline-sm text-deep-river">
                {step.time} — {step.title}
              </div>
              <p className="text-body-md text-on-surface-variant">{step.description}</p>
            </div>
          </li>
        ))}
      </ol>
    </SectionCard>
  )
}
