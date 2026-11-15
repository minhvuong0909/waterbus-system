# Station-bound scanners

## Baseline on origin/main at 1b82a3c

Device registration stored a human-entered code and marked the row active. Devices had no Station or pairing state. Check-in authorized Staff and TripStopCall alone and left the existing CheckInEvent device reference empty. ScannerAssignment combined Staff, scanner and TripStopCall. SQL already serialized Primary assignment by boarding call. Mobile Settings allowed an editable device code and every scan used offline batch sync. The existing signed QR, per-passenger selection, SQLite preload, idempotency, clock bounds and NoShow logic remain in use.

## Operation

1. In **Scanners**, register a device at its physical Station. The server generates a code such as SC-BD-001 using the Station code. Multiple devices can serve the same Station.
2. Generate an activation QR. Staff sign in and scan it from **Settings** on the phone. A current shift is unnecessary for enrollment.
3. In **Staff shifts**, schedule each Staff member at a Station. Shifts have half-open intervals. A Staff member can have one shift at a time, including at the same Station. Adjacent shifts may share a boundary.
4. In **Check-in**, configure the TripStopCall window and explicitly assign one Primary Offline scanner for a finite interval. Other paired scanners at the Station can work online.
5. In **Manifest**, select an available boarding call and preload the passenger list and QR verification key. Use the scanner to verify the signed booking QR and select the passengers present. The app marks offline writes pending until the API acknowledges them.

## Authorization and pairing

The one-time activation QR expires in 10 minutes. The API stores the SHA-256 digest of a random 256-bit secret and consumes it in the same transaction that creates the enrollment. Regenerating the QR invalidates its predecessor. Administrators can cancel an invitation. SQL device locks serialize pairing, revocation and transfer. Issuance and redemption share a per-IP limit of 12 requests per minute per API instance.

The native client keeps the enrollment credential in Expo SecureStore, using the device-only accessibility class on iOS. The repository uses Expo SDK 58 and has no native signing module. Non-exportable key signing would require a custom native build and native-platform testing. This MVP uses a random 256-bit bearer credential with independent Staff authentication. A party that extracts the credential can replay it with an eligible Staff session. The production app requires an HTTPS API origin. Keep activation bodies and credential headers out of logs. [Expo SecureStore](https://docs.expo.dev/versions/v58.0.0/sdk/securestore/) · [Expo development builds](https://docs.expo.dev/develop/development-builds/introduction/)

The API checks the current Identity role, account state and lockout, then resolves the scanner from the credential. A live enrollment and an active shift at its Station authorize access to serviceable boarding calls at that Station. A check-in still requires the ticket's Booking Trip and boarding call to match, along with its validity, boarding status and check-in window. The event records the authenticated device, enrollment and Staff.

Suspension, revocation, re-pairing and transfer close the current enrollment and active or future Primary assignments and cancel the invitation. Transfer selects a new Station and requires fresh pairing. Device codes stay stable across transfer. Staff logout clears the manifest and removes the session. The device pairing remains available to the next Staff account.

## Offline reconciliation

Each server-issued offline grant ties one enrollment, Staff shift and Primary entitlement to a TripStopCall. The half-open grant expires at the earliest of the Primary interval, shift, check-in window, or four hours from issuance. Check-in events in SQLite include the grant and original enrollment. The app submits them only under the owning Staff account. A replacement Primary cannot receive overlapping offline permission until all previously issued grants for the call expire. Shift and Primary writes serialize by Staff account and boarding call, respectively.

Reconciliation checks the scan time against the grant, revocation cutoffs and the existing clock-skew configuration. A still-active enrollment can reconcile eligible events after a shift expires. An ended enrollment can submit its Staff-owned historical work for review. Review conflicts appear in the device's Admin history and defer NoShow while unresolved. Approval requires a scan before the relevant revocation cutoffs and an eligible ticket and trip. The decision records the Admin, time and reason. Corroborate the client-reported scan time with gate records. A disconnected device learns of revocation on reconnection or stops at its cached expiration.

Logout, cache clearing and re-pairing retain pending SQLite events. The app retains original enrollment credentials for reconciliation. Device loss or an OS uninstall can erase local data. Drain pending events before replacing or resetting hardware.

## Database rollout

The additive migration preserves Staff accounts, scanner codes, existing ScannerAssignment rows and CheckInEvent history. Legacy devices with unknown Station appear as **Station required**. Existing Staff-coupled assignment rows remain history and confer no station-bound access.

1. Drain legacy offline queues using the compatible client and API before cutover.
2. Apply the migration and deploy the API, Admin web and scanner app together. Keep ambiguous devices unbound until the Admin confirms their physical Station.
3. Bind and pair legacy devices explicitly, create station shifts and set Primary Offline grants. Local legacy rows without enrollment evidence remain on the originating phone for manual reconciliation.
4. A database downgrade is blocked once station-bound data exists. Restore a pre-cutover backup for rollback and reconcile post-cutover events before resuming operation.

## API changes

| Route | Authentication and contract |
| --- | --- |
| /api/v1/admin/scanner-devices | Admin JWT. Create by Station, filter list, issue or cancel invitation, suspend, revoke, reset, transfer and review events. |
| /api/v1/admin/staff-station-shifts | Admin JWT. List, create and revoke shifts. |
| /api/v1/scanner/pair | Active Staff JWT. Redeem one activation string from the request body. |
| /api/v1/scanner/status | Active Staff JWT and scanner credential. Return Station, enrollment and current shift. |
| /api/v1/checkins/assignments | Admin JWT. Set or revoke a finite Primary Offline interval. |
| /api/v1/checkins/assignments/mine | Active Staff JWT and scanner credential. List available boarding calls. |
| /api/v1/checkins/manifest/{tripStopCallId} | Active Staff JWT and scanner credential. Preload authorized manifest and optional offlineGrant. |
| /api/v1/checkins | Active Staff JWT and scanner credential. Submit an online check-in. |
| /api/v1/checkins/sync | Active Staff JWT and original enrollment credential. Submit Staff-owned events with offlineGrantId. |

Send device credentials in the X-Scanner-Credential header. The ASP.NET Core controller types describe the request and response payloads in Development Swagger.
