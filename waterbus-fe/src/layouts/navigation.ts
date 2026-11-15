import { ROUTES } from '../routes/routes'

/** Main pages of the public site, shared by the header and the footer. */
export const NAV_ITEMS = [
  { label: 'Timetable', to: ROUTES.searchResults },
  { label: 'Explore', to: ROUTES.explore },
  { label: 'My Tickets', to: ROUTES.myTickets },
  { label: 'Help', to: ROUTES.help },
]
