# Commercial audit remediation

Date: 2026-10-29. Owner: Nhật Anh / Commercial Backend. This report records remediation of the earlier read-only architecture audit; that audit is preserved unchanged.

## Scope and baseline

Verified local `main`, `origin/main`, and initial HEAD at `9309a7cf95e01a218be17610419d671029767340` (merged PR #39). No tracked local edits existed. Preserved the pre-existing untracked backend ZIP and architecture audit, ignored files, and local integration credentials. Created `fix/commercial-audit-findings`; no push, merge, or PR.

Controller → IService → Application Service → repository interface → Infrastructure EF remains intact. The SQL transaction runner, `IUnitOfWork`, commercial application lock, allocation checks, and post-commit cache/events remain in use. No Domain, Trips query handler, CheckIns, Mobile, Frontend, shared deployment, schema, or package changes. No C# comments added.

## H01 — implemented

**Root cause:** idempotency was decided from merchant reference plus gateway transaction number before inspecting the verified outcome. A failed notification consumed the same key subsequently used by a successful outcome. Consequently a real receipt could be answered as already confirmed while remaining recorded as Failed.

**Gateway evidence boundary:** VNPay documents successful status `00` and suspected-fraud status `07` requiring merchant review. A synthetic failed `02` followed by `00` is a defensive contradictory-evidence scenario, not a claim that VNPay normally upgrades terminal failures. The `07` case models review-related uncertainty. Notifications and Querydr fixtures use the production HMAC adapter; no signature bypass was introduced. See [VNPay payment documentation](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html) and [merchant query/refund documentation](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html).

**Behavior:**

- Repeated failure for the recorded transaction remains idempotent (`02`). Repeated verified success for the settled transaction remains `02`, without duplicate tickets/allocations.
- A verified success after Failed records Succeeded, gateway reference and completion time, retains prior failure evidence, and prefixes `FailureReason` with `ReconciliationRequired:`. It never restores released holds or issues tickets, even if an upstream state were reopened. Existing late-receipt refund handling can process this unallocated receipt once.
- A conflicting failure or different successful gateway transaction after settlement preserves the existing receipt, reference, completion, tickets, and allocation. It appends deduplicated `VerifiedOutcomeConflict:` evidence and returns `99`. This flags investigation rather than automatically paying a refund or downgrading received money.
- A transaction key already owned by another attempt is flagged for reconciliation.
- Signed merchant Querydr payment reconciliation enters the same confirmation rules after signature, merchant/reference, amount, and payment-type verification. Invalid signatures, wrong merchant/amount/reference/type remain rejected.
- Expired/cancelled/unfulfillable orders record received money for reconciliation, without inventory resurrection. External merchant queries remain outside the SQL transaction; settlement stays inside it.

**Red → green evidence:** two new failure→success cases first failed (`Expected 00, Actual 02`). After correction the focused payment/VNPay selection passed **45/45**. The first isolated infrastructure run passed **20/20**, including two new SQL concurrency cases with independent DbContexts: failure followed by concurrent success after another buyer acquires the seat, and concurrent contradictory callbacks. Both verify eventual financial state, no duplicate ticket, and no resurrection.

## H04 — implemented

**Root cause:** writes loaded the trip without its boat, accepted any assigned seat class, and did not reject unsupported fare currency or negative prices. Seat replacement evaluated some fare errors after marking old holds cancelled.

**Authoritative rules:** retain Scheduled/Boarding, no actual departure, sales window, valid ordered TripStopCalls, seat ownership, and existing VisitOrder overlap rules. Add active/non-deleted boat and seat class checks; seats must remain active/non-deleted. Selected seat classes require one active, effective, non-deleted VND fare with non-negative price. Zero is permitted by current domain snapshots; no new strictly-positive-price policy was invented. Effective fare lookup and soft-delete filtering remain in Infrastructure.

`BookingReferenceRepository` now includes Boat. Creation rejects invalid inventory/pricing before reserving. Replacement validates the new inventory/fares before cancelling the original holds and preserves the original deadline. SQL rollback continues protecting multi-leg creation and later failures; cache/events remain post-commit.

**Payment-time decision:** a new Infrastructure boolean query detects withdrawn boat/class/seat and changed boat assignment, including soft-deleted resources via an order-scoped `IgnoreQueryFilters` query. Payment initiation rejects withdrawn inventory with the existing concurrency exception; first verified settlement records the receipt for reconciliation without issuing tickets. Changes to current fare price/currency/activation do not reprice an accepted VND quote. A repeated settled callback does not revalidate or invalidate already-issued tickets merely because catalog resources were later withdrawn.

**Read-side discrepancy retained for its owner:** Seat Map checks currency but does not reject negative fare prices; copying that behavior would violate domain non-negative snapshots. Search pricing has additional independent audit findings. Trips handlers were not modified. Catalog writers do not necessarily share the commercial application lock: these checks observe current SQL state but do not establish a new cross-team locking contract for concurrent catalog edits.

**Red → green evidence:** the new sellability suite initially had **26 failed, 12 passed**. Its 38 cases cover inactive/deleted boat, class and seat; USD, negative, inactive, expired, future and duplicate fares; unchanged original holds/cache after rejected replacement; VND adjacent segments; withdrawn inventory at payment; stable quotes and issued tickets. The initial replacement unit failures use an InMemory/direct runner and do not claim the old SQL rollback was broken. The combined focused sellability/payment/architecture/booking/checkout selection passed **67/67**. Three additional real SQL cases cover rejected replacement without persisted changes, an invalid return leg rolling back the first leg/access grant, and deleted payment resources changed in independent contexts. The final focused SQL/Redis run passed **23/23**, zero skipped.

## M01 — blocked pending an approved recovery contract

Inspected `RefundRequestService`, `RefundReconciliationService`, `ManualRefundService`, `FinancialTransactionRepository`, the VNPay refund/query adapter, original merchant references and ticket/payment refund allocations. No speculative financial operation was implemented.

A refund is stored Processing with budget/allocations before the gateway request. A lost response leaves it Processing without `GatewayReference`. Request retry returns the same refund without a second network refund. Manual confirmation rejects unresolved automatic Processing refunds. Current reconciliation requires an already-known refund gateway number plus signed, matching original merchant reference, refund type and amount.

The adapter's Querydr result has no original refund request ID suitable for correlating an unknown refund reference to `refund.Id` (the outbound refund `RequestId`). A signed successful original payment is not proof of refund. Matching original payment, amount and type alone is insufficient when two equal partial refunds belong to different legs/tickets. Query request/response identifiers must not be assumed to identify the lost refund request. Existing [VNPay query/refund documentation](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html) and current contracts do not establish the additional correlation/recovery operation required here.

Five new safety cases send an actual timeout, then valid signed Querydr responses for payment `01/00`, refund `02/00`, partial refund `03/00`, rejection `03/09`, and pending `03/05`, without a known reference. All leave Processing, no completion/reference, no manual settlement, one gateway refund call on retry, one allocation and unchanged reserved amount. **5/5 passed**; this is evidence of preserved safeguards, not a fix for recovery.

### Proposed recovery procedure for team approval

1. Keep the refund Processing, reserve its existing budget/allocations, and prevent resend/manual payout while its outcome is uncertain.
2. Merchant operations searches its authorized portal/provider support using merchant account/environment, original payment reference and gateway number/date, outbound refund RequestId (`refund.Id` in GUID N format), amount, request time, and affected allocations. Retain provider evidence; time elapsed alone is not evidence of failure.
3. Obtain evidence linking that exact request to the definitive refund transaction and outcome. If settled, record the identified gateway number and verified settlement evidence; if definitively rejected/not executed, require evidence that no automatic payout remains possible before authorizing manual handling. Ambiguous/pending evidence leaves Processing.
4. Agree evidence format, authorized Admin role, audit retention, duplicate-operation rules, and provider correlation guarantees. Then design a supported recovery API/service that re-locks and re-reads the refund under the existing SQL runner, checks its unchanged status, exact payment/amount/request/allocation budget, records evidence and transitions atomically. After approved rejection recovery, the existing manual flow may be used with its normal controls.
5. Add real SQL race tests for simultaneous recovery/reconcile/manual requests and equal-amount partial refunds, plus verified merchant integration evidence. There is currently no such recovery endpoint: this proposal does not authorize direct database edits or an unsupported second payout.

## H02 — Commercial dependency, coordination required

`RefundEligibilityEvaluator` correctly excludes genuine NoShow, completed tickets/bookings, and segments already served to the disembarking call; unserved/interrupted eligible segments use full paid ticket allocation. Changing all cancelled-trip NoShow to refundable would refund genuine missed boardings. No evaluator, CheckIns, lifecycle, or shared Domain changes were made.

**Required contract with CheckIns/Operations:** `BoardingStatus.NoShow` must assert a real boarding opportunity at the ticket's exact `BoardingCallId`/VisitOrder, whose window actually opened and closed before cancellation/termination/skipping. A scheduled close timestamp by itself does not establish that opportunity. Stop and lifecycle updates must preserve actual service evidence and prevent NoShow finalization for unserved boarding calls. Known unresolved offline events must delay finalization; unsent mobile events require an agreed synchronization/completeness policy rather than treating a grace interval as proof. Incorrect historical NoShow requires coordinated evidence-based correction, not a blanket commercial override. If upstream cannot supply reliable meaning for NoShow, an agreed opportunity/evidence contract is required before Commercial can safely adjust eligibility.

Cross-team regression scenarios to implement together:

| Scenario | Expected Commercial result after authoritative boarding reconciliation |
| --- | --- |
| Cancelled before boarding opportunity; scheduled close passes | Never genuine NoShow; unserved ticket eligible once, at paid allocation |
| Terminated/skipped before the later boarding call | Unserved passenger remains eligible; preserve exact call identity on loops |
| Earlier boarding opportunity completed; passenger absent; later cancellation | Genuine NoShow remains ineligible |
| Passenger boarded; trip terminates before intended disembarking call | Eligible for full paid ticket allocation, subject to allocation/budget checks |
| Intended disembarking call already served before termination | Ineligible |
| Offline check-in unresolved or not yet authoritatively synchronized | Defer NoShow decision; reconcile events before final refund classification |

These are handoff scenarios, not claims of new end-to-end tests or a completed H02 fix.

## M06 — DevOps configuration handoff

Root `docker-compose.yml` already maps QR key ID/private key, merchant TMN/hash secret, payment-page URL, return URL and session-cache Redis connection. `.env.example` entries alone do not inject additional container environment. No shared Compose or infrastructure was edited.

The DevOps owner should add these exact entries under `api.environment`:

```yaml
Checkout__SessionTtlMinutes: ${Checkout__SessionTtlMinutes:-10}
Checkout__PaymentPendingTtlMinutes: ${Checkout__PaymentPendingTtlMinutes:-15}
Checkout__AccessTokenTtlDays: ${Checkout__AccessTokenTtlDays:-30}
VnPay__WebApiUrl: ${VNPAY_WEB_API_URL:?Set the matching merchant query/refund endpoint}
```

Use `VNPAY_WEB_API_URL=https://sandbox.vnpayment.vn/merchant_webapi/api/transaction` for the sandbox environment; obtain the production endpoint and merchant configuration from the provider for a production environment. Pay/query/refund credentials and endpoints must agree. `SessionTtlMinutes` and `PaymentPendingTtlMinutes` are clamped to 1–120; guest access days are clamped to 1–365 at grant creation.

Only when deploying independent quorum nodes, add a populated override mapping for every configured node, for example:

```yaml
RedisLock__Connections__0: ${REDLOCK_CONNECTION_0:?Set independent Redis node 0}
RedisLock__Connections__1: ${REDLOCK_CONNECTION_1:?Set independent Redis node 1}
RedisLock__Connections__2: ${REDLOCK_CONNECTION_2:?Set independent Redis node 2}
```

Those `REDLOCK_CONNECTION_N` variables are proposed deployment source names; .NET consumes `RedisLock__Connections__N`. Do not inject blank array values or count replicas of one master as independent votes. Omitting the array retains the current single `ConnectionStrings__RedisConnection` fallback for local use; checkout session cache continues using that connection independently of quorum configuration.

Keep a stable secret-store RSA private PEM of at least 2048 bits and matching `Qr__KeyId`; do not regenerate it on restart. Their mappings already exist. DevOps should inspect the resolved API environment/binding with non-secret sentinel values, exercise custom TTLs and configured quorum, and verify coherent merchant endpoints. These deployment checks remain a handoff; test-container success does not prove deployment configuration is corrected.

## M13 — backend contract verified; Mobile correction required

`RsaBookingQrSigner` already signs compact JWS `base64url(header).base64url(payload).signature` with RS256 (RSA SHA-256 / PKCS#1 v1.5). Header is `{ "alg": "RS256", "typ": "JWT", "kid": "configured-key-id" }`; payload contains `pbid`, `ver`, `tid`, `iat`, without `kid`. Public key is SPKI PEM and the key endpoint returns its ID/algorithm. Signing format is unchanged.

`waterbus-mobile/myApp/src/services/crypto/qrVerifier.ts` currently reads payload `kid`, then may select the latest active cached key. Mobile must parse/validate the first JWS segment, require RS256, select the exact header kid, and verify the original encoded `header.payload` bytes. Header fields remain untrusted until verification. Agree rejection/refresh behavior for missing or unknown keys; do not silently select another cached key. Retain old named keys for the approved offline validity window. Test two retained keys, old valid QR, unknown kid, disallowed alg, tampered header/payload/signature, and booking/version validity. Current backend exposes only the current key; rotation/distribution remains a team agreement. No scanner edits or physical-device verification occurred.

## Changed files and local commits

Paths below are relative to `waterbus-be/`, except the API guide:

| Files | Purpose |
| --- | --- |
| `src/WaterbusSystem.Application/Features/Payments/Services/PaymentConfirmationService.cs` | H01 outcome handling; H04 first-settlement inventory safeguard |
| `src/WaterbusSystem.Application/Features/Payments/Services/PaymentService.cs` | H04 payment initiation safeguard |
| `src/WaterbusSystem.Application/Features/Bookings/Services/BookingCreationService.cs` | Boat/class/currency/fare validation |
| `src/WaterbusSystem.Application/Features/Bookings/Services/CheckoutSeatChangeService.cs` | Equivalent replacement validation before cancellation |
| `src/WaterbusSystem.Application/Interfaces/Repositories/IBookingRepository.cs` | Inventory availability query contract |
| `src/WaterbusSystem.Infrastructure/Repositories/BookingRepository.cs` | Order-scoped unavailable-resource query |
| `src/WaterbusSystem.Infrastructure/Repositories/BookingReferenceRepository.cs` | Include assigned boat |
| `tests/WaterbusSystem.UnitTests/Payments/VerifiedVnPayFixture.cs` | Real production HMAC callback/Querydr adapter fixture with local HTTP transport |
| `tests/WaterbusSystem.UnitTests/Payments/PaymentOutcomeTests.cs` | 16 H01 signed-outcome cases |
| `tests/WaterbusSystem.UnitTests/Bookings/BookingSellabilityTests.cs` | 38 H04 cases |
| `tests/WaterbusSystem.UnitTests/Bookings/CommercialSqlTests.cs` | Five additional real SQL cases, two H01 and three H04 |
| `tests/WaterbusSystem.UnitTests/Payments/RefundTimeoutSafetyTests.cs` | Five M01 safety cases |
| `../docs/NHAT_ANH_COMMERCIAL_API.md`, `docs/COMMERCIAL_AUDIT_REMEDIATION.md` | Current behavior and scoped handoffs/results |

| Local commit | Description |
| --- | --- |
| `6d62825` | `fix(payment): reconcile contradictory verified outcomes` |
| `cd68045` | `fix(booking): enforce authoritative sellability rules` |
| `7e35782` | `test(refund): preserve uncertain refund safeguards` |
| Final documentation commit containing this report | `docs(commercial): record audit remediation and handoffs` |

## Final verification

Focused runs above were completed before the associated fix commits. All full commands ran from `waterbus-be` against the committed code:

| Command | Result |
| --- | --- |
| `dotnet build WaterbusSystem.sln --no-restore` | Success; 0 warnings, 0 errors |
| `dotnet test WaterbusSystem.sln --no-restore` | 849 passed, 0 failed, 0 skipped; 2m53s |
| `.\tests\infrastructure\Run-IntegrationTests.ps1 -AllTests` | 849 passed, 0 failed, 0 skipped; 2m56s |

The explicit `--no-restore` test command used process-scoped `WATERBUS_TEST_SQLSERVER` and `WATERBUS_TEST_REDIS` pointing at the same isolated resources as the runner. The existing ignored credential file was read without printing or changing it. The runner reused only the `waterbus-integration` project: SQL Server on loopback port 11433 and three independent Redis nodes on 16379/16380/16381. Disposable test databases are managed by the fixtures; unrelated containers/databases/volumes were not modified. The test containers remain running for reuse.

The 849 cases include 23 real SQL/Redis integration cases and the existing HTTP contract tests. There are 64 added cases compared with the verified historical 785-case suite: 16 payment, 38 sellability, five SQL, and five timeout-safety cases. No tests were disabled or weakened. A diff scan confirmed zero added C# comment lines; `git diff --check` passed. Ownership-boundary diffs were empty for Domain, Trips handlers, CheckIns, Mobile, Frontend, and shared deployment files.

## Remaining dependencies and final Git status

M01 recovery, H02 boarding semantics, M06 deployment mappings, and M13 scanner key selection are not declared fixed. Real merchant sandbox credentials/authorization, physical scanner/offline flows, shared application startup/deployment and other architecture-audit findings remain outside this scoped remediation. Signed local merchant fixtures verify cryptographic processing, not a live bank settlement. No new financial API or manual override was introduced.

Final branch: `fix/commercial-audit-findings`. After the final documentation commit, tracked files are clean. Final `git status --short` contains only the two original untracked artifacts, intentionally excluded from commits:

```text
?? waterbus-be.zip
?? waterbus-be/docs/BACKEND_ARCHITECTURE_AUDIT.md
```

All four commits are local. No push, merge, or PR was performed.
