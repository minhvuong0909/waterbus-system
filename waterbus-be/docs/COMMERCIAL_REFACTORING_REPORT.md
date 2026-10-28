# Commercial backend refactoring verification

Date: 2026-10-28 (Asia/Saigon)
Owner scope: Nhật Anh
Branch: `refactor/commercial-transactions`

The assigned backend refactoring is complete. The existing Checkout implementation was preserved in the approved, separate baseline commit. All later commits contain the remaining owned commercial work. At the start of this finalization, all feature commits through `3b6701b` were local. Synchronization and post-merge validation will be recorded separately in this report.

## Module status and main files

Paths in this report are relative to `waterbus-be/`.

| Module | Status | Application entry points | Infrastructure persistence / adapters |
| --- | --- | --- | --- |
| Booking creation and remaining Checkout reads | Complete | `IBookingService` / `BookingService`, `ICheckoutReadService` / `CheckoutReadService`; existing `ICheckoutService`, `BookingCreationService`, and seat-change services retained | `OrderRepository`, `BookingRepository`, `BookingReferenceRepository` |
| Guest access and expiration | Complete | `ICheckoutAccessService` / `CheckoutAccessService`, `ICheckoutExpirationService` / `CheckoutExpirationService` | `AccessGrantRepository`; cleanup worker resolves the expiration service |
| Payment / VNPay | Complete | `IPaymentService` / `PaymentService`, `PaymentConfirmationService` | `FinancialTransactionRepository`, booking/order repositories; existing `VnPayService` retained |
| Refund requests, allocation, manual processing and reconciliation | Complete | `IRefundRequestService` / `RefundRequestService`, `IManualRefundService` / `ManualRefundService`, `IRefundReconciliationService` / `RefundReconciliationService`; eligibility evaluator retained | Financial transaction and booking repositories; existing VNPay and manual refund adapters retained |
| QR credentials and public key | Complete | `IBookingQrService` / `BookingQrService` | Scoped booking read; existing `RsaBookingQrSigner` retained |
| Redis holds and commercial transactions | Complete: reviewed and integration verified | Existing checkout-session and post-commit-effect services retained; EF exception mapping moved into the unit of work | Existing Redis lock/store and `SqlCommercialTransactionRunner` retained; `EfUnitOfWork` shared by the repositories |
| Owned legacy CQRS cleanup | Complete | Removed all owned booking/payment/refund command/query slices after moving production callers and tests | No unrelated CQRS removal |

Controllers are `BookingsController`, `CheckoutsController`, `PaymentsController`, and `RefundsController`. Request/response contracts live under `Application/DTOs`; validators live under `Application/Validators`. Focused contracts live under `Application/Interfaces/Services`, persistence operations under `Application/Interfaces/Repositories`, and the unit-of-work contract under `Application/Interfaces/Persistence`.

## Architecture and compatibility

The owned HTTP flow is `Controller -> IService -> Application Service -> IRepository -> Infrastructure Repository -> EF Core`. Services coordinate `IUnitOfWork`, `ICommercialTransactionRunner`, domain entities, and integration interfaces. No owned service uses DbContext, DbSet, EF query/persistence APIs, or MediatR. Repositories and the transaction runner share the scoped `ApplicationDbContext`. Architecture tests enforce the dependency boundary and mandatory transaction runners for commercial writers.

HTTP routes, verbs, JSON field names, response meanings, guest header, rate-limit metadata, and refund `AdminOnly` authorization were preserved. In particular:

- Booking responses still expose `totalAmount`; checkout responses retain their existing fields.
- Guest access requires an active, unrevoked, unexpired hashed grant for the specified order. The middleware resolves current AccessGrant identities and preserves its legacy booking-token lookup.
- VNPay IPN retains GET and POST support with signed query-string parameters for both verbs and case-sensitive `RspCode` / `Message` response fields. Form-body fields do not supply or override signed query fields.
- Since `3b6701b`, POST Booking returns HTTP 201 Location for the existing Checkout GET resource. Its response JSON is unchanged; reading Location requires the returned access token in `X-Guest-Token`, never in the URL.
- Browser returns never change payment state. Only verified IPNs or verified gateway reconciliation can confirm payment and issue tickets.
- Existing HMAC and constant-time verification stay inside the existing VNPay adapter. No other payment provider was introduced.
- Refund requests persist before the external gateway call. Gateway calls remain outside SQL transactions. Manual refunds still require evidence, the exact amount, and an identified admin.
- QR remains compact JWS RS256 using RSA SHA-256 / PKCS#1 v1.5. Header and payload fields remain compatible with the staff scanner: `alg`, `typ`, `kid`, `pbid`, `ver`, `tid`, `iat`. Public-key responses never include private material.
- The CLR request/response type locations changed during the migration; existing HTTP JSON shapes were retained. Generated OpenAPI request schema names now reflect the service request types.

A reproduced refund race was fixed: a late gateway outcome previously wrote a tracked refund object after a separate reconciliation had already completed it. The second transaction now reloads the refund and only applies the outcome while it remains Processing. The regression test failed with the old outcome-handling logic and passes with the correction.

## Locking, rollback and recovery review

The existing per-seat Redis mutex is a short critical-section lease (30 seconds), disposed in `finally`. It is distinct from the configurable checkout-session TTL. Booking creation executes inside the SQL commercial transaction, whose transaction-owned exclusive application lock remains held until commit or rollback, including both legs of a round trip. Persisted segment reservations protect inventory after the mutex is released. Seat changes use the same SQL transaction boundary. Segment overlap continues to use TripStopCall VisitOrder.

Checkout cache and realtime updates occur after successful SQL commit. Cache/notification failures do not undo persisted reservations or issue additional tickets. SQL deadlines remain authoritative when Redis is unavailable, and the expiration worker releases expired reservations. Existing Redis ownership, cache TTL, and SQL fallback semantics were preserved.

## Historical pre-merge validation results

Counts below are test cases, not classes. At `01aee3d`, the original 753 tests plus 27 additional cases totaled 780. Commit `3b6701b` added five signed POST-IPN HTTP cases; the latest reported pre-merge result was **785 passed, 0 failed, 0 skipped**, including **18 SQL/Redis cases**, with build **0 warnings, 0 errors**. The October 13 baseline of 215 passing tests is earlier historical evidence. None of these results substitutes for post-merge validation.

| Validation | Passed | Failed | Skipped | Notes |
| --- | ---: | ---: | ---: | --- |
| Original Checkout baseline: `dotnet test WaterbusSystem.sln --no-restore` | 738 | 0 | 15 | SQL/Redis were unavailable initially |
| Booking/guest focused group | 85 | 0 | 7 | Includes the new service and authorization tests |
| Payment/VNPay focused group | 107 | 0 | 7 | Includes real existing VNPay signature tests |
| Refund focused commercial group | 113 | 0 | 7 | Architecture assertion updated to target transaction-owning services |
| Refund service regression tests after restoring corrected implementation | 6 | 0 | 0 | Includes a competing-context reconciliation write |
| QR focused commercial group | 117 | 0 | 7 | Includes real RSA signatures and tamper rejection |
| Cleanup focused group with new SQL tests before integration environment | 117 | 0 | 10 | Added SQL tests were correctly skipped at this stage |
| Real Kestrel HTTP and Swagger smoke tests | 2 | 0 | 0 | Production DI registrations, isolated InMemory persistence, fake external gateway, real RSA signer |
| `tests/infrastructure/Run-IntegrationTests.ps1` | 18 | 0 | 0 | Real SQL Server and three Redis nodes, all healthy |
| Final `dotnet test WaterbusSystem.sln --no-restore` with SQL/Redis environment set | **780** | **0** | **0** | Full solution, including integration and HTTP tests |

Final `dotnet build WaterbusSystem.sln --no-restore`: success, **0 warnings, 0 errors**.

The standalone integration run completed in 1m42s; the final full solution run completed in 1m39s. Docker was initially unavailable but became available during execution. No SQL/Redis tests remain unexecuted in the final run. The prescribed integration runner used its ignored local credentials file and isolated disposable databases; credentials were not included in Git or test output.

Integration coverage includes concurrent same-seat booking without a shared Redis mutex; round-trip rollback with no grant/cache leak; cache visibility after both SQL bookings commit; duplicate concurrent refunds making one gateway call; occupied-seat changes restoring the original reservation and cache; successful seat replacement preserving expiry; concurrent IPNs across independent scopes issuing one ticket; Redis exclusive ownership and TTL; and existing SQL constraints and migration tests.

Live HTTP checks exercised Swagger UI and OpenAPI, invalid/valid booking requests, guest access denial/read, Hold -> Submit -> Payment -> QR, GET/POST duplicate IPNs, refusal to cancel a paid order, refund admin authorization, manual evidence validation, and successful manual confirmation. These HTTP tests intentionally use isolated InMemory persistence; they supplement the separate real SQL/Redis suite.

During development, an architecture test initially failed because it still inspected retired handlers rather than transaction-owning services; its assertions were updated and strengthened. The stale-refund regression was deliberately run against the previous logic to demonstrate failure, then rerun successfully with the fix. No knowingly failing code was committed.

## Legacy removal and ownership

Removed owned slices:

- Booking: CreateBooking, CreateCheckout, CreateCheckoutHold, SubmitCheckout, CancelCheckout, UpdateCheckoutSeats, ExpireCheckoutHolds, GetCheckout, GetBookingQr, GetBookingQrPublicKey.
- Payment: CreateVnPayUrl, ConfirmVnPayIpn, GetVnPayReturn, ReconcilePayment.
- Refund: RequestRefund, ConfirmManualRefund, ReconcileRefund.

Existing tests now call the replacement services, preserving their behavioral assertions. Shared services no longer depend on legacy command/response types. The existing unrelated CQRS infrastructure and features remain in place, including AI Booking, Trip Search, operations, and staff check-in. No AI Booking production code was modified. Shared registration files and the owned guest middleware/cleanup worker were updated as necessary. No other developer's production feature was refactored, and no Domain entity or database migration changed.

No new C# code comments were introduced; existing unrelated comments were preserved.

## Remaining verification and operational limits

- No live merchant VNPay payment or real-money refund was sent. Before deployment, verify the deployed merchant configuration, callback URLs, and signed sandbox initiation/IPN/query/refund flow using the real gateway environment.
- Confirm the deployed QR public key and configured key identifier match the staff scanner's downloaded key. Key handling and signing algorithms were not changed.
- The SQL application lock intentionally serializes commercial writes. Correctness was tested; production throughput and failover/load behavior were not benchmarked.
- Redis/realtime effects remain best effort after SQL commit, as in the baseline. There is no new durable outbox; reconnecting clients must resynchronize through REST.
- The integration runner leaves its local test containers running. It did not target shared or production databases.

To rerun all tests with the isolated integration environment:

```powershell
cd waterbus-be
.\tests\infrastructure\Run-IntegrationTests.ps1 -AllTests
```

Without the SQL/Redis environment variables, `dotnet test WaterbusSystem.sln --no-restore` will skip the 18 environment-dependent cases.

## Ordered local commits and included files

The following inventory is generated from Git. All paths are repository-relative. The first commit contains exactly the approved 30-file Checkout baseline; subsequent commits keep the remaining module work separate.

### `94ecf29` — refactor(checkout): checkpoint existing service and repository baseline

```text
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutHoldRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutHoldRequestValidator.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutRequestValidator.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/SubmitCheckoutRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/SubmitCheckoutRequestValidator.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/UpdateCheckoutSeatsRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Responses/Bookings/BookingResponse.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Responses/Bookings/CheckoutResponse.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingCreationInput.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingCreationService.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutCommitEffects.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutSeatChangeService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Persistence/IUnitOfWork.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IBookingReferenceRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IBookingRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IOrderRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/ICheckoutSeatChangeService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/ICheckoutService.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/EfUnitOfWork.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/BookingReferenceRepository.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/BookingRepository.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/OrderRepository.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/CheckoutsController.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutHoldRequestValidatorTests.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutServiceTests.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutSubmitFlowTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/TestHelpers/CommercialHandlerFactory.cs
```

### `4582c51` — refactor(bookings): migrate booking reads guest access and expiration to services

```text
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateBookingRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Responses/Bookings/CheckoutDetailResponse.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/ExpireCheckoutHolds/ExpireCheckoutHoldsCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Queries/GetCheckout/GetCheckoutQuery.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingService.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutAccessService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutExpirationService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutReadService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IAccessGrantRepository.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IOrderRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IBookingService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/ICheckoutAccessService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/ICheckoutExpirationService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/ICheckoutReadService.cs
A	waterbus-be/src/WaterbusSystem.Application/Validators/Bookings/CreateBookingRequestValidator.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/BackgroundJobs/ExpiredBookingCleanupService.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/AccessGrantRepository.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/OrderRepository.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/BookingsController.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/CheckoutsController.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Middlewares/GuestAccessMiddleware.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/BookingApplicationServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutSubmitFlowTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialCheckoutTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/TestHelpers/CommercialHandlerFactory.cs
```

### `796f033` — refactor(payments): move VNPay use cases behind services and repositories

```text
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Payments/CreateVnPayUrlRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Responses/Payments/PaymentResponses.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/ConfirmVnPayIpn/ConfirmVnPayIpnCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/CreateVnPayUrl/CreateVnPayUrlCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/ReconcilePayment/ReconcilePaymentCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Queries/GetVnPayReturn/GetVnPayReturnQuery.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Services/PaymentConfirmationService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Services/PaymentService.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IBookingRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IFinancialTransactionRepository.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IOrderRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IPaymentService.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/BookingRepository.cs
A	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/FinancialTransactionRepository.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/OrderRepository.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/PaymentsController.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialCheckoutTests.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Payments/PaymentServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/TestHelpers/CommercialHandlerFactory.cs
```

### `8dcbe8b` — refactor(refunds): migrate refund workflows and preserve reconciled outcomes

```text
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Refunds/ConfirmManualRefundRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Refunds/RequestRefundRequest.cs
A	waterbus-be/src/WaterbusSystem.Application/DTOs/Responses/Refunds/RefundResponses.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/ConfirmManualRefund/ConfirmManualRefundCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/ReconcileRefund/ReconcileRefundCommand.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/RequestRefund/RequestRefundCommand.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Services/ManualRefundService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Services/RefundReconciliationService.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Services/RefundRequestService.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IBookingRepository.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IFinancialTransactionRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IManualRefundService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IRefundReconciliationService.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IRefundRequestService.cs
A	waterbus-be/src/WaterbusSystem.Application/Validators/Refunds/ConfirmManualRefundRequestValidator.cs
A	waterbus-be/src/WaterbusSystem.Application/Validators/Refunds/RequestRefundRequestValidator.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/BookingRepository.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/FinancialTransactionRepository.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/RefundsController.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialArchitectureTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialCheckoutTests.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Refunds/RefundServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/TestHelpers/CommercialHandlerFactory.cs
```

### `b01834d` — refactor(qr): expose booking credentials through application service

```text
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Queries/GetBookingQr/GetBookingQrQuery.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Queries/GetBookingQrPublicKey/GetBookingQrPublicKeyQuery.cs
A	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingQrService.cs
M	waterbus-be/src/WaterbusSystem.Application/Interfaces/Repositories/IBookingRepository.cs
A	waterbus-be/src/WaterbusSystem.Application/Interfaces/Services/IBookingQrService.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Repositories/BookingRepository.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/CheckoutsController.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/BookingQrServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutServiceTests.cs
```

### `44fe03b` — refactor(commercial): remove legacy CQRS and verify transaction boundaries

```text
M	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateBookingRequest.cs
M	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/UpdateCheckoutSeatsRequest.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/CancelCheckout/CancelCheckoutCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/CreateBooking/CreateBookingCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/CreateCheckout/CreateCheckoutCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/CreateCheckoutHold/CreateCheckoutHoldCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/ExpireCheckoutHolds/ExpireCheckoutHoldsCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/SubmitCheckout/SubmitCheckoutCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Commands/UpdateCheckoutSeats/UpdateCheckoutSeatsCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Queries/GetCheckout/GetCheckoutQuery.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingCreationService.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/BookingService.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutSeatChangeService.cs
M	waterbus-be/src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutService.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/ConfirmVnPayIpn/ConfirmVnPayIpnCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/CreateVnPayUrl/CreateVnPayUrlCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Commands/ReconcilePayment/ReconcilePaymentCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Payments/Queries/GetVnPayReturn/GetVnPayReturnQuery.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/ConfirmManualRefund/ConfirmManualRefundCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/ReconcileRefund/ReconcileRefundCommand.cs
D	waterbus-be/src/WaterbusSystem.Application/Features/Refunds/Commands/RequestRefund/RequestRefundCommand.cs
R092	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutHoldRequestValidator.cs	waterbus-be/src/WaterbusSystem.Application/Validators/Bookings/CreateCheckoutHoldRequestValidator.cs
R094	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/CreateCheckoutRequestValidator.cs	waterbus-be/src/WaterbusSystem.Application/Validators/Bookings/CreateCheckoutRequestValidator.cs
R088	waterbus-be/src/WaterbusSystem.Application/DTOs/Requests/Bookings/SubmitCheckoutRequestValidator.cs	waterbus-be/src/WaterbusSystem.Application/Validators/Bookings/SubmitCheckoutRequestValidator.cs
A	waterbus-be/src/WaterbusSystem.Application/Validators/Bookings/UpdateCheckoutSeatsRequestValidator.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/BackgroundJobs/ExpiredBookingCleanupService.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/DependencyInjection.cs
M	waterbus-be/src/WaterbusSystem.Infrastructure/Persistence/EfUnitOfWork.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/CheckoutsController.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/PaymentsController.cs
M	waterbus-be/src/WaterbusSystem.WebApi/Controllers/RefundsController.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/BookingQrServiceTests.cs
R088	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CreateBookingCommandHandlerTests.cs	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/BookingServiceBehaviorTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutHoldRequestValidatorTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CheckoutSubmitFlowTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialArchitectureTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialCheckoutTests.cs
A	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialHttpTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialSqlTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CreateBookingSeatMappingTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CreateBookingValidatorTests.cs
R092	waterbus-be/tests/WaterbusSystem.UnitTests/Payments/ConfirmVnPayIpnCommandHandlerTests.cs	waterbus-be/tests/WaterbusSystem.UnitTests/Payments/PaymentConfirmationServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Payments/PaymentServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/Refunds/RefundServiceTests.cs
M	waterbus-be/tests/WaterbusSystem.UnitTests/TestHelpers/CommercialHandlerFactory.cs
```

## Historical repository state at `01aee3d`

Implementation checkpoint: `44fe03b`. After the implementation commit, `git status --short` and `git diff --stat` both produced no output. A final documentation-only commit adds this report. The final delivery checks confirm a clean working tree, an empty unstaged/staged diff, and the same `refactor/commercial-transactions` branch. All commits remained local at that checkpoint.

### `3b6701b` — fix(commercial): correct booking location and verify signed POST IPNs

- `BookingsController` uses `CreatedAtAction` targeting `CheckoutsController.Get` with the order id; HTTP tests follow Location and verify 403 without the guest token and 200 with it.
- Five additional Kestrel HTTP cases use the existing real `VnPayService` HMAC verifier with generated test secrets: valid signed query confirms; tampering returns `97` without state changes; duplicate returns `02` without extra tickets; form-only returns `97`; unsigned form overrides cannot change signed query fields.
- Reviewed changes: `docs/NHAT_ANH_COMMERCIAL_API.md`, `waterbus-be/src/WaterbusSystem.WebApi/Controllers/BookingsController.cs`, `waterbus-be/tests/WaterbusSystem.UnitTests/Bookings/CommercialHttpTests.cs`.
- Reported pre-merge validation: **785 passed, 0 failed, 0 skipped**, including **18 SQL/Redis integration tests**; build **0 warnings, 0 errors**. Actual post-merge execution is documented separately.
