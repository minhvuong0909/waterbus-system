import { ErrorState } from '../../../components/ui'
import { ROUTES } from '../../../routes/routes'
import { t } from '../../../i18n'

/** A booking page opened without a chosen trip (direct link, new tab): send the traveler to the search. */
export default function NoTripSelected() {
  return (
    <ErrorState
      icon="directions_boat"
      title={t('No trip selected yet')}
      message={t('Choose a trip first, then you can pick your seats and book.')}
      action={{ label: t('Find a trip'), to: ROUTES.searchResults }}
    />
  )
}
