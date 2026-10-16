import { lazy, Suspense, useState } from 'react'
import type { ReactNode } from 'react'
import type { CorridorMap, Pier } from '../../features/trips/types'
import { Icon } from '../ui'
import { t } from '../../i18n'
import RiverMap from './RiverMap'

// The street map pulls in Leaflet and its styles, so it is only loaded when someone asks for it.
const PierLeafletMap = lazy(() => import('./PierLeafletMap'))

interface PierMapProps {
  map: CorridorMap
  pierActions?: (pier: Pier) => ReactNode
}

type View = 'diagram' | 'map'

/** The corridor as an illustration, with a switch to the same piers on a real map. */
export default function PierMap({ map, pierActions }: PierMapProps) {
  const [view, setView] = useState<View>('diagram')
  const tab = (active: boolean) =>
    `flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-colors ${
      active ? 'bg-deep-river text-on-primary shadow-sm' : 'text-on-surface-variant hover:text-deep-river'
    }`

  return (
    <div className="space-y-space-sm">
      <div role="tablist" className="inline-flex rounded-full bg-surface-container p-1">
        <button
          type="button"
          role="tab"
          aria-selected={view === 'diagram'}
          onClick={() => setView('diagram')}
          className={tab(view === 'diagram')}
        >
          <Icon name="water" className="text-[16px]" />
          {t('Diagram')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={view === 'map'}
          onClick={() => setView('map')}
          className={tab(view === 'map')}
        >
          <Icon name="map" className="text-[16px]" />
          {t('Map')}
        </button>
      </div>

      {view === 'diagram' ? (
        <RiverMap map={map} pierActions={pierActions} />
      ) : (
        <Suspense
          fallback={
            <div className="flex h-[470px] items-center justify-center rounded-2xl bg-surface-container text-sm text-on-surface-variant">
              {t('Loading map…')}
            </div>
          }
        >
          <PierLeafletMap map={map} pierActions={pierActions} />
        </Suspense>
      )}
    </div>
  )
}
