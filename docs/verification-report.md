# Verification Report

## Inputs and environment

- The original Activity 4 implementation started on `master` at `origin/master`; this stricter follow-up started on [`c7fa2aa`](https://github.com/Todd127-byte/EquipmentBorrowing/commit/c7fa2aa) on branch `codex/activity4-campus-equipment-borrowing`.
- .NET SDK: 10.0.401; target framework: .NET 10.
- PostgreSQL 18 ran as a disposable local cluster bound to `127.0.0.1:55432`. Each of the four PostgreSQL integration tests created a unique schema, applied the EF migration, read back state, then dropped the schema.
- The two official PDFs were not available in the repository, parent folder, or checked Documents/Downloads/Desktop folders. This work follows the full requirements pasted with the task.
- The desktop app was launched against a disposable PostgreSQL database in the prior verification pass. Startup migration, sample data, catalog, and available-only list loaded. The automation window became minimized after its initial observation, so a tab-by-tab manual walkthrough and screenshots were not completed. The current follow-up changed date semantics and search ordering; it did not change the UI layout.

## Final checks

| Check | Result | Notes |
| --- | --- | --- |
| `dotnet build EquipmentBorrowing.slnx` | Passed: 0 warnings, 0 errors | Full solution including Avalonia Desktop, Demo, Infrastructure, Application, Domain, and Tests. |
| `dotnet test EquipmentBorrowing.slnx --no-build --no-restore` with `EQUIPMENT_BORROWING_TEST_CONNECTION_STRING` set | Passed: 23, failed: 0, skipped: 0 | 18 workflow tests, 1 isolated EF/SQLite persistence test, and 4 PostgreSQL integration tests. SQLite remains test-only. |
| PostgreSQL migration on clean schemas | Passed | Each PostgreSQL integration test independently applied `20261120120247_InitialPostgreSql`. |
| PostgreSQL constraints | Passed | Normalized student/equipment uniqueness, filtered active-item uniqueness, invalid borrowing dates, and foreign-key rejection were verified. |
| PostgreSQL checkout, return, and history | Passed | Fresh contexts confirmed persisted rows, local-calendar return date, availability, active-list filtering, and histories. |
| Atomic multi-item rollback | Passed | A trigger failed on the second equipment update after the first update was attempted; fresh-context assertions showed no borrowing rows and both items available. |
| Concurrent checkout | Passed | Same-equipment requests yielded one success; concurrent requests left a student at exactly three active borrowings. |
| Management, search, and ordering | Passed | PostgreSQL tests verified registration, updates, eligibility, scoped history, combined search, and deterministic ID ties; workflow tests cover both sort fields and directions. |
| Desktop startup smoke check | Passed with scope limit | Startup loaded the catalog and available-only list. Interactive UI states and screenshots remain unverified. |
| Unknown commit/disconnect fault injection | Not run | Recovery logic was reviewed, but connection loss during commit was not simulated. |

Borrowing dates use the workstation's local calendar day, normalized to UTC midnight when stored in PostgreSQL `timestamp with time zone` columns. This convention is documented in the [README](../README.md).

The initial baseline test run had 7 passes and 2 failures because the working tree already contained changed demo fixtures while the old tests expected earlier values. Workflow tests now set their own fixtures.

## Use case status

| Use case | Status | Verification note |
| --- | --- | --- |
| UC-01 View Equipment Catalog | PARTIAL | Populated catalog loaded; empty and database-error UI paths were not exercised. |
| UC-02 Find Available Equipment | PARTIAL | Available-only result was verified; no-match and database-error UI paths were not exercised. |
| UC-03 Borrow Equipment | COMPLETE | Single checkout, rejection rules, transaction state, and persisted data were verified through workflow and PostgreSQL tests. |
| UC-04 View Active Borrowings | COMPLETE | Active summaries and exclusion of returned records were verified through persistence tests. |
| UC-05 Return Equipment | COMPLETE | Return date/status, availability, history, and repeat-return rejection were verified. |
| UC-06 Check Out Multiple Equipment Items | COMPLETE | Batch persistence, invalid-request rejection, rollback, and concurrent checkout were verified. |
| UC-07 View Student Borrowing History | COMPLETE | Student histories stayed scoped; active and returned records were read back. |
| UC-08 View Equipment Borrowing History | COMPLETE | Equipment histories stayed scoped across active and returned records. |
| UC-09 Search, Filter, and Sort Borrowings | PARTIAL | Field search, filters, sort directions, stable ties, and combined PostgreSQL search passed; UI reset/error controls were not manually exercised. |
| UC-12 Register Student | COMPLETE | Defaults, validation, normalized uniqueness, and PostgreSQL persistence were verified. |
| UC-13 Update Student Information | COMPLETE | Blank/duplicate rejection, contact clearing, stable identity, and history preservation were verified. |
| UC-14 Change Student Borrowing Eligibility | COMPLETE | Restricted checkout failed, existing return remained possible, and repeated states were safe. |
| UC-15 Register Equipment | COMPLETE | Required fields, normalized uniqueness, availability default, and PostgreSQL persistence were verified. |
| UC-16 Update Equipment Information | COMPLETE | Description clearing and duplicate/blank rejection preserved availability and history. |

## Acceptance scenarios and business rules

The detailed A01–A19 ledger includes setup, actions, expected behavior, actual results, evidence, and remaining gaps in [acceptance-scenarios.md](evidence/acceptance-scenarios.md). UC-10 and UC-11 remain out of scope. The [traceability matrix](traceability-matrix.md) records every BR-01–BR-19 status: BR-01–BR-17 are COMPLETE; BR-18 and BR-19 are PARTIAL because unknown commit loss and a full application-process restart were not exercised.

## Remaining verification

- Exercise empty and database-error UI states, search reset, and the full tab workflow manually; retain screenshots if submission requires them.
- Run the app through a complete process restart against a non-test database to demonstrate user-facing persistence.
- Fault-inject a connection loss during commit to verify the unknown-outcome path experimentally.
- Compare use-case wording and rubric against the official PDFs when available.

The PostgreSQL cluster and its temporary files are removed after testing.
