import { Button, Icon } from '../../../components/ui'
import { pierPhotos } from '../../../content/pierImages'
import { PIER_INFO } from '../../../content/piers'
import { getLocale, t } from '../../../i18n'
import { ROUTES } from '../../../routes/routes'
import type { Pier } from '../../trips/types'

function distanceLabel(distance: string): string {
  if (distance === 'inside') return t('Inside the pier')
  if (distance === 'opposite') return t('Opposite the pier')
  return t('About {distance}', { distance })
}

/**
 * One pier of the corridor: its photo when there is one, where it is, a short introduction and
 * what is nearby, then the two things a traveler does with it: book from it or find the way there.
 */
export default function PierDetails({ pier, onBook }: { pier: Pier; onBook: () => void }) {
  const info = PIER_INFO[pier.code]
  const photo = pierPhotos(pier.code)[0]

  return (
    <div className="space-y-space-md">
      {photo && <img src={photo} alt={pier.name} className="h-44 w-full rounded-2xl object-cover" />}
      <div className="space-y-1">
        <h3 className="font-display text-2xl font-bold text-deep-river">{pier.name}</h3>
        <p className="flex items-start gap-1.5 text-sm text-on-surface-variant">
          <Icon name="pin_drop" className="mt-0.5 text-lg text-teal-flow" />
          {pier.address}
        </p>
      </div>

      {info && (
        <>
          <p className="text-sm leading-relaxed text-on-surface">{info.story[getLocale()]}</p>
          <div>
            <h4 className="mb-1.5 text-xs font-bold uppercase tracking-[0.14em] text-on-surface-variant">
              {t('Nearby')}
            </h4>
            <ul className="space-y-1.5 text-sm">
              {info.nearby.map((place) => (
                <li
                  key={place.name.en}
                  className="flex items-baseline justify-between gap-3 border-b border-surface-container pb-1.5"
                >
                  <span className="text-deep-river">{place.name[getLocale()]}</span>
                  <span className="shrink-0 text-xs text-on-surface-variant">{distanceLabel(place.distance)}</span>
                </li>
              ))}
            </ul>
          </div>
        </>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button to={ROUTES.searchResults} onClick={onBook}>
          {t('Plan a trip from here')}
          <Icon name="arrow_forward" className="text-lg" />
        </Button>
        <a
          href={`https://www.google.com/maps/dir/?api=1&destination=${pier.latitude},${pier.longitude}`}
          target="_blank"
          rel="noopener noreferrer"
          className="inline-flex min-h-[44px] items-center gap-2 rounded-full border border-teal-flow px-5 py-2.5 text-sm font-semibold text-teal-flow transition-colors hover:bg-sand-light"
        >
          <Icon name="directions" className="text-lg" />
          {t('Get directions')}
        </a>
      </div>
    </div>
  )
}
