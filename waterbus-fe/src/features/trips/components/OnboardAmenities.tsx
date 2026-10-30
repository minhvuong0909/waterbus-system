import { Icon } from '../../../components/ui'
import type { TripDetail } from '../types'
import { t } from '../../../i18n'
import SectionCard from './SectionCard'

export default function OnboardAmenities({ trip }: { trip: TripDetail }) {
  return (
    <SectionCard icon="directions_boat" title={t('Onboard Amenities')}>
      <div className="grid grid-cols-1 gap-space-sm sm:grid-cols-2">
        {trip.amenityDetails.map((a) => (
          <div key={a.title} className="rounded-xl bg-mist/70 p-space-md">
            <div className="flex items-center gap-2 font-display text-headline-sm text-deep-river">
              <Icon name={a.icon} className="text-xl text-teal-flow" />
              {a.title}
            </div>
            <p className="mt-1 text-body-sm text-on-surface-variant">{a.description}</p>
          </div>
        ))}
      </div>
    </SectionCard>
  )
}
