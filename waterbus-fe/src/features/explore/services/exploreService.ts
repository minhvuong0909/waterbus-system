import { EXPLORE_DATA } from '../../../mocks/explore'
import { localize } from '../../../i18n'
import type { ExploreData } from '../types'

// The Explore content is editorial and lives in the app (the backend has no API for it), so it is
// returned right away, translated to the active language, without a fake network delay.
export function getExploreData(): Promise<ExploreData> {
  return Promise.resolve(localize(EXPLORE_DATA))
}
