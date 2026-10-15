import { Icon } from '../../../components/ui'
import type { TripDetail } from '../types'
import { t } from '../../../i18n'

const DOT: Record<TripDetail['timeline'][number]['tone'], string> = {
  teal: 'bg-teal-flow',
  amber: 'bg-signal-amber',
  dark: 'bg-deep-river',
}

function SectionCard({
  icon,
  title,
  aside,
  children,
}: {
  icon: string
  title: string
  aside?: string
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl bg-surface-container-lowest p-space-lg shadow-[0_2px_16px_rgba(13,37,56,0.05)]">
      <div className="mb-space-md flex items-center justify-between">
        <div className="flex items-center gap-space-sm">
          <div className="flex h-9 w-9 items-center justify-center rounded-full bg-secondary-container text-teal-flow">
            <Icon name={icon} className="text-[20px]" />
          </div>
          <h2 className="font-headline-md text-headline-md text-deep-river">{title}</h2>
        </div>
        {aside && <span className="text-body-sm text-on-surface-variant">{aside}</span>}
      </div>
      {children}
    </section>
  )
}

export function JourneyTimeline({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="schedule" title={t('Journey Timeline')} aside={trip.dateLabel}>
      <ol className="space-y-space-md">
        {trip.timeline.map((step) => (
          <li key={step.time + step.title} className="flex gap-space-sm">
            <span className={`mt-2 h-2.5 w-2.5 shrink-0 rounded-full ${DOT[step.tone]}`} />
            <div>
              <div className="font-headline-sm text-headline-sm text-deep-river">
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

export function OnboardAmenities({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="directions_boat" title={t('Onboard Amenities')}>
      <div className="grid grid-cols-1 gap-space-sm sm:grid-cols-2">
        {trip.amenityDetails.map((a) => (
          <div key={a.title} className="rounded-xl bg-mist/70 p-space-md">
            <div className="flex items-center gap-2 font-headline-sm text-headline-sm text-deep-river">
              <Icon name={a.icon} className="text-[20px] text-teal-flow" />
              {a.title}
            </div>
            <p className="mt-1 text-body-sm text-on-surface-variant">{a.description}</p>
          </div>
        ))}
      </div>
    </SectionCard>
  )
}

export function BoardingSteps({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="qr_code_2" title={t('Boarding & E-Ticket')}>
      <div className="grid grid-cols-1 gap-space-sm sm:grid-cols-3">
        {trip.boardingSteps.map((s, i) => (
          <div key={s.title} className="rounded-xl bg-mist/70 p-space-md">
            <div className="font-headline-sm text-headline-sm text-teal-flow">{i + 1}</div>
            <div className="font-headline-sm text-headline-sm text-deep-river">{s.title}</div>
            <p className="mt-1 text-body-sm text-on-surface-variant">{s.description}</p>
          </div>
        ))}
      </div>
    </SectionCard>
  )
}
