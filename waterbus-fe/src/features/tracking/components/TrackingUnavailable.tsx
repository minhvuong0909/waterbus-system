import { Link } from 'react-router-dom'
import { Breadcrumb, Icon } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

/**
 * Shown while the backend has no tracking feed for passengers and guests: boat positions are only
 * shared with Admin (radar), so this page explains it and points to what a traveler can do now.
 */
export default function TrackingUnavailable() {
  return (
    <div className="mx-auto w-full max-w-7xl px-margin pt-space-md">
      <Breadcrumb items={[{ label: 'Home', to: ROUTES.home }, { label: 'Live Trip' }]} />
      <div className="mx-auto flex min-h-[50vh] max-w-lg flex-col items-center justify-center gap-space-md py-space-3xl text-center">
        <span className="flex h-16 w-16 items-center justify-center rounded-full bg-teal-flow/10 text-teal-flow">
          <Icon name="directions_boat" className="text-[34px]" />
        </span>
        <h1 className="font-headline-md text-headline-md text-deep-river">
          {t('Live tracking for passengers is coming soon')}
        </h1>
        <p className="text-body-md text-on-surface-variant">
          {t(
            'Boat positions are shared with the operations team only for now. Meanwhile you can check the timetable and book your seat.',
          )}
        </p>
        <div className="flex flex-wrap justify-center gap-space-sm pt-space-xs">
          <Link
            to={ROUTES.search}
            className="inline-flex items-center gap-2 rounded-full bg-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-on-primary shadow-sm transition-colors hover:bg-secondary"
          >
            <Icon name="confirmation_number" className="text-[18px]" />
            {t('Book a Trip')}
          </Link>
          <Link
            to={ROUTES.searchResults}
            className="inline-flex items-center gap-2 rounded-full border border-teal-flow px-6 py-2.5 font-headline-sm text-body-md font-semibold text-teal-flow transition-colors hover:bg-sand-light"
          >
            <Icon name="schedule" className="text-[18px]" />
            {t('Timetable')}
          </Link>
        </div>
      </div>
    </div>
  )
}
