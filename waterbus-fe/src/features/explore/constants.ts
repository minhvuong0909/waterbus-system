import { ROUTES } from '../../routes/routes'

/** Width and side padding of every section of the Explore page. */
export const SECTION = 'mx-auto max-w-7xl px-margin'

/** Where both calls to action of the page lead: the departures, filtered to sightseeing trips. */
export const SIGHTSEEING_RESULTS = `${ROUTES.searchResults}?type=sightseeing`

export const PRIMARY_CTA =
  'group inline-flex items-center gap-2 rounded-xl bg-teal-flow px-6 py-3 font-display text-sm font-semibold text-on-primary shadow-lg transition-colors hover:bg-secondary'
