import { Icon } from '../../../components/ui'
import type { Landmark, PoiCategory } from '../types'

/** Icon of each landmark category, used when there is no photo of the place. */
const CATEGORY_ICON: Record<PoiCategory, string> = {
  Skyline: 'location_city',
  Heritage: 'account_balance',
  Riverside: 'waves',
}

/** The landmark's photo, or a tile with its category icon while no photo of the place is available. */
export default function LandmarkImage({ landmark, className = '' }: { landmark: Landmark; className?: string }) {
  if (landmark.image) {
    return <img alt={landmark.title} src={landmark.image} loading="lazy" decoding="async" className={`object-cover ${className}`} />
  }
  return (
    <div
      aria-hidden="true"
      className={`flex items-center justify-center bg-secondary-container text-teal-flow ${className}`}
    >
      <Icon name={CATEGORY_ICON[landmark.category] ?? 'location_on'} className="text-5xl" />
    </div>
  )
}
