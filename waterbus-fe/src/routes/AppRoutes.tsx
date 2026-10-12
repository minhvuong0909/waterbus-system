import { lazy } from 'react'
import type { ComponentType } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { ADMIN_NAV, CAPTAIN_ROLES } from '../features/admin/navigation'
import AdminLayout from '../layouts/admin/AdminLayout'
import CaptainLayout from '../layouts/admin/CaptainLayout'
import PrivateRoute from './PrivateRoute'
import PublicRoute from './PublicRoute'
import RouteWithLayout from './RouteWithLayout'
import { ROUTES } from './routes'

// Each page is its own chunk, loaded on first visit.
const HomePage = lazy(() => import('../pages/home/HomePage'))
const SignInPage = lazy(() => import('../pages/auth/SignInPage'))
const SearchJourneyPage = lazy(() => import('../pages/trips/SearchJourneyPage'))
const SearchResultsPage = lazy(() => import('../pages/trips/SearchResultsPage'))
const TripDetailPage = lazy(() => import('../pages/trips/TripDetailPage'))
const SeatSelectionPage = lazy(() => import('../pages/booking/SeatSelectionPage'))
const PassengerDetailsPage = lazy(() => import('../pages/booking/PassengerDetailsPage'))
const CheckoutReviewPage = lazy(() => import('../pages/booking/CheckoutReviewPage'))
const PaymentPage = lazy(() => import('../pages/payment/PaymentPage'))
const PaymentCallbackPage = lazy(() => import('../pages/payment/PaymentCallbackPage'))
const BookingSuccessPage = lazy(() => import('../pages/payment/BookingSuccessPage'))
const MyTicketsPage = lazy(() => import('../pages/tickets/MyTicketsPage'))
const TicketDetailPage = lazy(() => import('../pages/tickets/TicketDetailPage'))
const ManageBookingPage = lazy(() => import('../pages/tickets/ManageBookingPage'))
const LiveTrackingPage = lazy(() => import('../pages/tracking/LiveTrackingPage'))
const ExplorePage = lazy(() => import('../pages/explore/ExplorePage'))
const HelpPage = lazy(() => import('../pages/help/HelpPage'))
const SitemapPage = lazy(() => import('../pages/sitemap/SitemapPage'))

// Operations area. Which roles may open each page is declared once, in ADMIN_NAV.
const ADMIN_PAGES: Record<string, ComponentType> = {
  [ROUTES.admin]: lazy(() => import('../pages/admin/OverviewPage')),
  [ROUTES.adminRadar]: lazy(() => import('../pages/admin/RadarPage')),
  [ROUTES.adminTrips]: lazy(() => import('../pages/admin/TripsPage')),
  [ROUTES.adminSchedules]: lazy(() => import('../pages/admin/SchedulesPage')),
  [ROUTES.adminStations]: lazy(() => import('../pages/admin/StationsPage')),
  [ROUTES.adminBoats]: lazy(() => import('../pages/admin/BoatsPage')),
  [ROUTES.adminFares]: lazy(() => import('../pages/admin/FaresPage')),
  [ROUTES.adminIncidents]: lazy(() => import('../pages/admin/IncidentsPage')),
}
const CaptainPortalPage = lazy(() => import('../pages/captain/CaptainPortalPage'))

const PAGES = [
  { path: ROUTES.home, Page: HomePage },
  { path: ROUTES.signIn, Page: SignInPage },
  { path: ROUTES.search, Page: SearchJourneyPage },
  { path: ROUTES.searchResults, Page: SearchResultsPage },
  { path: ROUTES.tripDetail, Page: TripDetailPage },
  { path: ROUTES.seatSelection, Page: SeatSelectionPage },
  { path: ROUTES.passengerDetails, Page: PassengerDetailsPage },
  { path: ROUTES.checkoutReview, Page: CheckoutReviewPage },
  { path: ROUTES.payment, Page: PaymentPage },
  { path: ROUTES.paymentCallback, Page: PaymentCallbackPage },
  { path: ROUTES.bookingSuccess, Page: BookingSuccessPage },
  { path: ROUTES.myTickets, Page: MyTicketsPage },
  { path: ROUTES.ticketDetail, Page: TicketDetailPage },
  { path: ROUTES.manageBooking, Page: ManageBookingPage },
  { path: ROUTES.manageByLink, Page: ManageBookingPage },
  { path: ROUTES.liveTracking, Page: LiveTrackingPage },
  { path: ROUTES.explore, Page: ExplorePage },
  { path: ROUTES.help, Page: HelpPage },
  { path: ROUTES.sitemap, Page: SitemapPage },
]

// Passenger pages are open to guests. Pages that need a role are wrapped in <PrivateRoute roles>.
export default function AppRoutes() {
  return (
    <Routes>
      {PAGES.map(({ path, Page }) => (
        <Route
          key={path}
          path={path}
          element={
            <PublicRoute>
              <RouteWithLayout>
                <Page />
              </RouteWithLayout>
            </PublicRoute>
          }
        />
      ))}
      {ADMIN_NAV.map(({ to, roles }) => {
        const Page = ADMIN_PAGES[to]
        return (
          <Route
            key={to}
            path={to}
            element={
              <PrivateRoute roles={roles}>
                <RouteWithLayout layout={AdminLayout}>
                  <Page />
                </RouteWithLayout>
              </PrivateRoute>
            }
          />
        )
      })}
      <Route
        path={ROUTES.captain}
        element={
          <PrivateRoute roles={CAPTAIN_ROLES}>
            <RouteWithLayout layout={CaptainLayout}>
              <CaptainPortalPage />
            </RouteWithLayout>
          </PrivateRoute>
        }
      />
      <Route path="*" element={<Navigate to={ROUTES.home} replace />} />
    </Routes>
  )
}
