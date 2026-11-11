# SQL Server + Redis integration tests

Run from the repository root with Docker Desktop's Linux engine running:

```powershell
./waterbus-be/tests/infrastructure/Run-IntegrationTests.ps1
```

The script starts an isolated Compose project (`waterbus-integration`), waits for health checks, supplies the two test environment variables, and runs the 18 SQL/Redis cases currently in this branch. The results dated 16/10/2026 below are historical; see [commercial verification](../../docs/COMMERCIAL_REFACTORING_REPORT.md) for final synchronization results. PR #13 has been merged into main, so the default solution path now includes all these tests.

To test a different checkout explicitly:

```powershell
./waterbus-be/tests/infrastructure/Run-IntegrationTests.ps1 -SolutionPath 'D:/path/to/another-checkout/waterbus-be/WaterbusSystem.sln'
```

Add `-AllTests` to run the entire suite with SQL/Redis enabled, or `-StartOnly` to start infrastructure without tests.

Endpoints bound to loopback only:

| Service | Host port |
|---|---:|
| SQL Server 2022 Developer | 11433 |
| Independent Redis 1 | 16379 |
| Independent Redis 2 | 16380 |
| Independent Redis 3 | 16381 |

The SQL password is generated locally and saved in `waterbus-be/.integration-tests.local.json`, already excluded by the backend `.gitignore`. Keep that file while retaining the SQL volume. The script restores any previous test environment variables after execution. Test fixtures create and delete their own `WaterbusDomainTests_*` databases, not the application database.

Containers remain running after tests. Stop them without deleting data:

```powershell
docker compose -f ./waterbus-be/tests/infrastructure/compose.yaml stop
```

Start them again through the script so it supplies the SQL password.

## Verified run — 16/10/2026

PR #13 commit `b77aa92`: **15 passed, 0 failed, 0 skipped** for the integration filter (about 62 seconds). SQL Server and all three Redis containers were healthy. The initial run using `localhost` failed to connect; the script now uses explicit IPv4 `127.0.0.1` to match Docker port bindings on this Windows host. No application source or test expectations were changed.

After moving Docker's WSL data to `D:/setup/DockerData/DockerDesktopWSL`, the full suite was run with `-AllTests` on the same commit: **450 passed, 0 failed, 0 skipped** (about 61 seconds). This covers the PR's existing tests; the additional review reproduction tests are not part of that commit, so this result does not resolve the two review findings by itself.

Docker Desktop initially failed on a stale `userAnalyticsOtlpHttp.sock`. Its runtime-only directory was preserved as `C:/Users/ACER/AppData/Local/Docker/run.stale-waterbus-20261016` and Docker recreated `run`; container images and volumes were retained.
