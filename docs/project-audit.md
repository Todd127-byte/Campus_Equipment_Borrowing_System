# Laboratory Activity 4 Project Audit

## Scope and source documents

The requested files `ITSD81 Lab Activity 4.pdf` and `ITSD81 Lab Activity 4 Use Case Suite.pdf` were not present in the repository, its parent repository directory, or the checked user document folders. This audit and implementation therefore use the complete requirements pasted with the task. The original PDFs should be reviewed against this work before final submission if they contain additional details.

## Baseline architecture at initial inspection

The solution targets .NET 10 and contains separate Domain, Application, Infrastructure, Avalonia Desktop, Demo, and xUnit projects. Desktop uses CommunityToolkit.Mvvm and dependency injection. Application services depend on repository interfaces, and EF repositories use `IDbContextFactory<EquipmentBorrowingDbContext>` so individual database calls do not share a long-lived context.

Infrastructure currently configures EF Core SQLite. Its two SQLite migrations define `Students`, `Equipment`, and `Borrowings`; startup migrates and seeds the local application-data database. Student and equipment identifiers are currently integer primary keys only. There are no institutional student numbers, equipment identifiers, contact fields, descriptions, or actual return timestamps.

## Baseline feature status

| Feature | Baseline status |
| --- | --- |
| UC-01 equipment catalog | Partial: all equipment list is present; empty/error/retry states are rudimentary. |
| UC-02 available equipment | Partial: service and filtered query exist. |
| UC-03 single checkout | Partial: basic validation exists; writes are separate and can leave inconsistent data. |
| UC-04 active borrowing list | Partial: active summaries exist. |
| UC-05 return equipment | Partial: basic repeat/missing checks exist; writes are separate and omit actual return time. |
| UC-06 multiple checkout | Missing. |
| UC-07 student history | Missing. |
| UC-08 equipment history | Missing. |
| UC-09 search/filter/sort | Missing. |
| UC-12 student registration | Missing. |
| UC-13 student editing | Missing. |
| UC-14 eligibility management | Partial: eligibility is a seeded field, without a management workflow. |
| UC-15 equipment registration | Missing. |
| UC-16 equipment editing | Missing. |

## Baseline defects and architectural gaps

- SQLite is the operational provider and its migrations use SQLite-specific types and SQL.
- The current schema cannot enforce unique normalized institutional numbers or equipment identifiers because those fields do not exist.
- The repository APIs lack create/update operations for students and equipment, transaction boundaries, histories, and search criteria.
- Borrowing and return flows each persist two related state changes in independent saves.
- Expected return dates are checked against today but are not constrained against checkout time in the database.
- Histories cannot show actual return dates because the entity does not store one.
- The desktop interface exposes only equipment and active borrowing pages.
- README and SQL evidence describe Activity 3 and SQLite, not the required PostgreSQL deliverable.

## Git and migration risk

At inspection, the current branch was `master` at `origin/master`. The worktree already contained user changes in `DatabaseInitializer.cs`, `DatabasePaths.cs`, two in-memory repositories, `SqlitePersistenceTests.cs`, and `README.md`, plus an untracked SQLite sample-data migration and designer. Those changes are preserved as input; sample data and migration files must be handled explicitly during the provider transition.

The existing SQLite database file is outside the repository in `%LOCALAPPDATA%`. It will not be migrated automatically or modified by PostgreSQL setup. The SQLite migration sources will be retained as legacy history and excluded from the active PostgreSQL migration assembly. PostgreSQL will receive a separate clean-database migration path. This avoids applying provider-specific SQLite migration SQL to PostgreSQL and avoids touching the existing local SQLite database.

## Planned implementation sequence

1. Extend the domain model for institutional numbers, equipment identifiers, descriptive fields, eligibility, and actual returns.
2. Add application repository operations, use-case services, history/search models, and validation.
3. Implement normalized database constraints and PostgreSQL EF Core mappings; keep the old SQLite migration source archived and inactive.
4. Implement a transactional checkout path that locks the student and selected equipment, validates current database state, and commits the complete set together. Implement atomic return updates as well.
5. Add PostgreSQL connection configuration through settings/environment without committing credentials, and register provider-specific DI.
6. Extend the Avalonia interface to expose inventory, available items, student/equipment management, borrowing, histories, and search workflows.
7. Add/update automated tests; run build and test commands. Verify PostgreSQL migrations and database constraints only when a PostgreSQL service/connection is available.
8. Replace Activity 3-only documentation with Activity 4 run, migration, use-case, traceability, test, and demonstration materials.

## Resulting architecture

The desktop application now uses PostgreSQL through Npgsql and EF Core migrations. The previous SQLite migration sources remain as legacy history and are excluded from the active migration assembly; SQLite is used only by an isolated persistence test. Services depend on repository interfaces, repositories create short-lived contexts through `IDbContextFactory`, and checkout/return changes are transactional. The Avalonia UI exposes Equipment, Students, Checkout & Returns, and Histories & Search tabs.

## Final verification and remaining limits

The full solution build passed with 0 warnings and 0 errors. The updated test suite passed 23 tests with no skips against a disposable PostgreSQL 18 cluster, including migration, constraints, write-failure rollback, and concurrency checks. The desktop startup smoke check loaded the catalog. A full interactive UI walk, a complete application-process restart check, unknown-commit connection-loss injection, and comparison with the unavailable official PDFs remain outstanding; see [the verification report](verification-report.md) and [the A01–A19 evidence ledger](evidence/acceptance-scenarios.md).
