import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
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
const BookingSuccessPage = lazy(() => import('../pages/payment/BookingSuccessPage'))
const MyTicketsPage = lazy(() => import('../pages/tickets/MyTicketsPage'))
const TicketDetailPage = lazy(() => import('../pages/tickets/TicketDetailPage'))
const ManageBookingPage = lazy(() => import('../pages/tickets/ManageBookingPage'))
const LiveTrackingPage = lazy(() => import('../pages/tracking/LiveTrackingPage'))
const ExplorePage = lazy(() => import('../pages/explore/ExplorePage'))
const HelpPage = lazy(() => import('../pages/help/HelpPage'))
const SitemapPage = lazy(() => import('../pages/sitemap/SitemapPage'))

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
  { path: ROUTES.bookingSuccess, Page: BookingSuccessPage },
  { path: ROUTES.myTickets, Page: MyTicketsPage },
  { path: ROUTES.ticketDetail, Page: TicketDetailPage },
  { path: ROUTES.manageBooking, Page: ManageBookingPage },
  { path: ROUTES.liveTracking, Page: LiveTrackingPage },
  { path: ROUTES.explore, Page: ExplorePage },
  { path: ROUTES.help, Page: HelpPage },
  { path: ROUTES.sitemap, Page: SitemapPage },
]

// No guards or login redirects: every page opens directly by URL.
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
      <Route path="*" element={<Navigate to={ROUTES.home} replace />} />
    </Routes>
  )
}
