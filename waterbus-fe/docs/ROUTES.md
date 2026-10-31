# Routes

Run `pnpm dev` and open http://localhost:5173. The in-app index lives at `/sitemap`.
Passenger routes open directly by URL. `PrivateRoute` guards routes that need a signed-in role (none yet).
Source of truth: `src/routes/routes.ts` (paths) and `src/routes/AppRoutes.tsx` (pages, lazy-loaded).

| Route | Screen |
|---|---|
| `/` | Home |
| `/sign-in` | Sign in / Create account |
| `/search` | Search journey |
| `/search/results` | Search results |
| `/trip` | Trip detail |
| `/booking/seats` | Seat selection |
| `/booking/passenger` | Passenger details |
| `/booking/review` | Review booking |
| `/payment` | Payment |
| `/payment/callback` | VNPay return page (checks the result, then shows success or a failure message) |
| `/payment/success` | Booking confirmed |
| `/tickets` | My tickets |
| `/tickets/detail` | Ticket detail |
| `/tickets/manage` | Manage booking |
| `/manage/:token` | Manage booking opened from a secure link (no sign-in needed) |
| `/tracking` | Live trip tracking |
| `/explore` | Explore river journeys |
| `/sitemap` | All routes |

Operations area (sign in required, roles in `src/features/admin/navigation.ts`):

| Route | Screen | Roles |
|---|---|---|
| `/admin` | Overview | Admin, Dispatcher |
| `/admin/radar` | Live Operations (Leaflet map of boats) | Admin, Dispatcher |
| `/admin/trips` | Trips, change boat | Admin, Dispatcher |
| `/admin/schedules` | Schedules, generate trips | Admin |
| `/admin/stations` | Stations CRUD | Admin |
| `/admin/boats` | Boats CRUD | Admin |
| `/admin/fares` | Fares | Admin |
| `/admin/incidents` | Weather alerts, suspend / cancel / terminate a trip | Admin, Dispatcher |
| `/captain` | Captain portal (Start, Arrived, SOS, narration) | Captain, Admin |

Unknown paths redirect to `/`.
