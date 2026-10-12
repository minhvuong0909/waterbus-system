# Domain alignment with BR v2.0 FINAL

This change prepares the commercial domain and EF Core model for the Booking/Payment/Refund modules. The business baseline is `Smart_Waterbus_Business_Rules_v2_0_FINAL_2026-09-29.md`.

## Model and rule boundaries

- **BR-COM-01/02/04/10:** `PurchaseOrder -> Booking -> SeatReservation -> Ticket`. Booking directly owns `TripId`, `BoardingCallId`, `DisembarkingCallId`, `QuotedTotal` and nullable `PaidAllocation`. `PurchaseOrder.ValidateForCheckout()` checks purchaser data, one/two Booking count and quoted totals at checkout submission; Draft Orders may be incomplete.
- **BR-ROUTE-02/06/07:** `Booking.SetJourney()` validates actual TripStopCall visit order. A sightseeing loop can board/alight at the same Station through two distinct calls. Composite foreign keys ensure both calls belong to the Booking's Trip.
- **BR-STOP-01/02, BR-BOARD-05:** TripStopCall records planned/actual arrival/departure, optional source ScheduleStop and per-call check-in windows. Trip has SalesCloseAt and actual journey timestamps. `IsDelayed` is a flag rather than another TripStatus.
- **BR-SEAT-03/04/05:** SeatReservation keeps passenger and paid fare snapshots. Its Trip must match its Booking; segment integers are snapshots of the Booking calls' **VisitOrder**, never global Station.OrderIndex. There is no UNIQUE(TripId, SeatId), so non-overlapping segments can reuse a seat. Overlap protection still requires transactional service logic.
- **BR-COM-08, BR-FARE-03:** `Ticket.Issue()` requires a successful Payment and confirmed Booking/reservation, takes price/seat snapshots and refuses a second issuance on the loaded reservation. SQL's unique reservation index provides the final duplicate barrier. Ticket belongs to the same Booking as its reservation. Ticket lifecycle is separate from `BoardingStatus` (`NotCheckedIn`, `CheckedIn`, `NoShow`).
- **BR-PAY-03/04/06, BR-RF-03/07/08/09/10:** FinancialTransaction has typed Payment/Refund and processing statuses, Order ownership, original-payment reference, merchant reference/time and per-Ticket allocations. Composite FKs prevent refund links across Orders. `ValidateRefund()` checks a successful original Payment, same Order/currency, remaining balance, distinct Ticket allocations, Completed/NoShow exclusion and 100% paid fare per included Ticket. Manual success requires evidence and an Admin identifier in SQL.

`ValidateRefund()` receives the refundable balance from the transaction service. That service must load/reserve the balance atomically, counting outstanding and successful refunds. It must determine Cancelled/Terminated eligibility from actual stop progress before constructing allocations; this method is not the complete refund workflow.

## Compatibility and remaining application work

- `Booking.TotalAmount` remains an unmapped compatibility alias for `QuotedTotal`. The existing API DTO can keep its TotalAmount field while the database uses QuotedTotal.
- Existing enum numeric values are retained. `BookingStatus.Pending` aliases PendingPayment; legacy Ticket Pending/CheckedIn/Refunded values and PaymentTransaction remain while the old application slices are migrated. New code should issue Ticket only after payment and use BoardingStatus for check-in.
- Existing Booking customer/token fields are retained for the legacy endpoints. New purchaser snapshots and secure-link ownership belong to PurchaseOrder/AccessGrant.
- Search query no longer relies on a shadow Ticket.TripId; it reads reservations through Trip.Bookings. Full search by actual served segment and FareRule pricing still belongs to the search/checkout implementation.
- The existing CreateBooking and VNPAY handlers still need the previously identified checkout/payment refactor: create Order, select actual calls, price/passenger snapshots, confirm reservations and call Ticket.Issue(). Redis CheckoutSession, gateway attempts/reconciliation, RSA signing and refund processing are separate tasks.
- RT01 demo seeding now creates missing TripStopCalls from ScheduleStops. It leaves check-in windows unset until configured. The demo's 15-minute sales cutoff is sample data, not a fixed BR policy.

## Migration

`20261005041926_CompleteBookingJourneyDomain` renames TotalAmount without discarding its values, removes redundant Ticket.TripId, adds the domain fields/keys/checks and normalizes old Pending/NotBoarded strings.

Legacy Bookings do not identify their real boarding/disembarking calls. Upgrade deliberately refuses a database containing Bookings until an explicit journey backfill is supplied; it never guesses identifiers or deletes history. Downgrade also refuses to discard populated Booking journey/allocation data. This project's currently reviewed Docker database has no Bookings.

From `waterbus-be`, apply to the configured development database:

```powershell
dotnet ef database update --project src/WaterbusSystem.Infrastructure --startup-project src/WaterbusSystem.WebApi -- --environment Development
```

Normal WebApi startup also runs migrations followed by demo seeding.

The new migration has **not** been applied to the user's existing WaterbusDb in this change. Upgrade/downgrade verification used separate temporary databases. Existing populated environments need a separately designed backfill migration; adding identifiers to some external file does not bypass the preflight guard.

## Verification

```powershell
dotnet build WaterbusSystem.sln
dotnet test WaterbusSystem.sln
```

CommercialDomainTests cover loop/reverse/invalid journey ordering, checkout cardinality/totals, payment-gated Ticket issuance, duplicate issuance and refund allocation boundaries.

DomainPersistenceTests require `WATERBUS_TEST_SQLSERVER` set to a local SQL Server connection string. They create uniquely named `WaterbusDomainTests_*` databases and delete only those databases after verification. They exercise the full migration chain, downgrade/reapply, preservation of unmapped legacy Bookings, valid round-trip/seat-reuse persistence and rejection of cross-Trip/cross-Booking/cross-Order references and unsupported manual refund success. They are explicitly skipped when that environment variable is absent.

Fresh verification on Docker SQL Server: **27 new domain/persistence tests passed** (18 domain cases and 9 SQL cases). Build succeeded with zero warnings/errors; diff whitespace checks passed.

The full suite is **not green: 57 passed, 4 failed, 0 skipped, 61 total**. All four failures are in CreateBookingCommandHandlerTests: the legacy handler constructs a Booking without its required Order/principal. The new composite relationships reject that invalid graph during EF SaveChanges, before pricing/overlap assertions. Baseline was 31 passed / 3 failed / 34 total: three checkout tests already failed due to zero pricing/missing reservation identifiers/overlap; the fourth, previously passing lock-retry test now exposes the same invalid write. The checkout handler needs an application-level refactor before it can write this model; the new tests do not demonstrate end-to-end checkout readiness. Existing Payment/SeatAvailability fixtures were updated to include their Order/Booking principals and their 16 tests pass.
