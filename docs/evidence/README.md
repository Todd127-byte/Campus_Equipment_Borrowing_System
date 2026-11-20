# Verification Evidence Index

This index points to checks that actually ran. The final console result is recorded in [postgresql-test-results.md](postgresql-test-results.md), and per-scenario setups, expectations, actual results, and gaps are in [acceptance-scenarios.md](acceptance-scenarios.md). No screenshot files were saved; the startup smoke result is noted in the verification report.

| Evidence | Supports | Location | Status |
| --- | --- | --- | --- |
| Full solution build | C# and Avalonia XAML compilation | `dotnet build EquipmentBorrowing.slnx`; final result in `../verification-report.md` | COMPLETE |
| Unit/in-memory workflow checks | Checkout rules, histories, search, return, registration, and edit behavior | `EquipmentBorrowing.Tests/BorrowingWorkflowTests.cs` | COMPLETE for tested scenarios |
| Isolated EF persistence check | Fresh-context persistence and return state | `EquipmentBorrowing.Tests/SqlitePersistenceTests.cs`; SQLite is test-only | COMPLETE for isolated persistence scenario |
| PostgreSQL migration and constraints | Clean schema migration, normalized uniqueness, FKs, dates, and active-item uniqueness | `EquipmentBorrowing.Tests/PostgreSqlIntegrationTests.cs` | COMPLETE |
| PostgreSQL write rollback | Failure after first equipment write attempt leaves no batch writes | `PostgreSql_multi_checkout_rolls_back_all_writes_after_trigger_failure` | COMPLETE |
| PostgreSQL concurrency | Same-equipment checkout and same-student limit races | `PostgreSQL_concurrent_checkouts_serialize_equipment_and_student_limit_checks` | COMPLETE |
| PostgreSQL management/search persistence | Student/equipment register and edit, eligibility, histories, combined search, ordering | `PostgreSQL_management_restriction_history_and_search_workflows_persist` | COMPLETE |
| Desktop startup smoke | PostgreSQL startup migration, sample seed, catalog, available-only list | `../verification-report.md` | COMPLETE with manual walkthrough limit |
| UI screenshots/full walkthrough | Visual interaction evidence across all tabs | Not saved; CUA window was minimized after startup observation | NOT CAPTURED |
| Unknown commit outcome | Simulated connection loss during commit | Recovery logic in Infrastructure; no fault injection | PARTIAL |

## A01-A19 index

| Scenario | Evidence pointer | State |
| --- | --- | --- |
| A01 | Startup catalog; empty/error cases not exercised | Partial |
| A02 | Available list excluded unavailable item; no-match/error not exercised | Partial |
| A03 | Checkout rules and fresh-context persistence; process restart not run | Partial |
| A04 | Active summary/readback excludes returned ID; UI empty/error not exercised | Partial |
| A05 | Return/date/history/repeat rejection; process restart not run | Partial |
| A06 | Multi-checkout validation, trigger rollback, and concurrency | Pass |
| A07 | Two-student scoping, empty/missing IDs; DB error not injected | Partial |
| A08 | Two-equipment scoping, empty/missing IDs; DB error not injected | Partial |
| A09 | All fields, filters, sort directions, stable ties; UI reset/error not exercised | Partial |
| A10 | Deferred UC-10 | Out of scope |
| A11 | Deferred UC-11 | Out of scope |
| A12 | Registration/default/duplicate/persistence; app restart not run | Partial |
| A13 | Update/contact clear and history preservation | Pass |
| A14 | Restriction, existing return, repeated state | Pass |
| A15 | Equipment registration/default/duplicate/persistence; app restart not run | Partial |
| A16 | Description clear, protected availability, history | Pass |
| A17 | Migration applied and database constraints independently checked | Pass |
| A18 | Rollback/concurrency passed; unknown commit loss not injected | Partial |
| A19 | Deliverables present; official PDF comparison unavailable | Partial |
