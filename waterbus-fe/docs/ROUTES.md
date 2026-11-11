# Routes

Run `pnpm dev` and open http://localhost:5173.
Passenger routes open directly by URL; guests open their booking with the secure link. `PrivateRoute` guards the
operations area and the captain's portal by role.
Source of truth: `src/routes/routes.ts` (paths) and `src/routes/AppRoutes.tsx` (pages, lazy-loaded).

| Route | Screen |
|---|---|
| `/` | Home |
| `/sign-in` | Sign in |
| `/search/results` | Search results |
| `/trip` | Trip detail |
| `/booking/seats` | Seat selection |
| `/booking/passenger` | Passenger details |
| `/booking/review` | Review booking |
| `/payment` | Payment |
| `/payment/callback` | VNPay return page (checks the result, then shows success or a failure message) |
| `/payment/success` | Booking confirmed |
| `/tickets` | My tickets (orders bought on this device) |
| `/tickets/detail` | Ticket detail |
| `/tickets/manage` | Manage booking |
| `/manage/:token?orderId=` | Manage booking opened from the secure link (no sign-in needed) |
| `/tracking` | Live trip tracking |
| `/explore` | Explore river journeys |
| `/help` | Help & FAQ |

Operations area (sign in required, roles in `src/features/admin/navigation.ts`):

| Route | Screen | Roles |
|---|---|---|
| `/admin` | Overview | Admin |
| `/admin/radar` | Live Operations (Leaflet map of boats) | Admin |
| `/admin/trips` | Trips | Admin |
| `/admin/stations` | Stations | Admin |
| `/admin/boats` | Boats | Admin |
| `/admin/fares` | Fares | Admin |
| `/admin/incidents` | Weather alerts, suspend / cancel / terminate a trip | Admin |
| `/admin/refunds` | Refund a booking, confirm a manual refund, reconcile | Admin |
| `/admin/check-in` | Check-in window of each stop | Admin |
| `/admin/audio-guides` | Sightseeing audio guides | Admin |
| `/captain` | Captain portal (Start, Arrived, SOS, GPS) | Captain, Admin |

Unknown paths redirect to `/`. The backend API is explored with Swagger (Development only), not from the web app.
