# Campus Equipment Borrowing System

Activity 4 extends the existing Avalonia application with PostgreSQL persistence, student and equipment management, atomic multi-item checkout, histories, and combined borrowing search/filter/sort. The PostgreSQL application uses the existing Domain/Application/Infrastructure/Desktop separation.

## Requirements and use cases

The requested source PDFs were not found in the workspace or checked document folders. The implementation and use-case documents in this repository follow the requirements pasted with the task; compare them with the official PDFs before submission if they contain additional details.

- [Use case catalog](docs/use-case-catalog.md)
- [Detailed use case suite](docs/use-case-suite.md)
- [Use case diagram source](docs/use-case-diagram.puml)
- [Business-rule traceability](docs/traceability-matrix.md)
- [Verification report and evidence index](docs/verification-report.md)
- [A01-A19 acceptance evidence](docs/evidence/acceptance-scenarios.md)
- [Demonstration guide](docs/demonstration-guide.md)
- [Project audit and implementation plan](docs/project-audit.md)

Required use cases: UC-01 through UC-09 except deferred UC-10/UC-11, plus UC-12 through UC-16.

## Technology

- .NET 10 and C#
- Avalonia UI 12 and CommunityToolkit.Mvvm
- Entity Framework Core 10
- PostgreSQL through Npgsql Entity Framework Core provider 10.0.3
- xUnit

## Prerequisites

- .NET 10 SDK
- PostgreSQL server and a database/user with permission to create tables, indexes, and constraints
- (Optional) Docker Desktop for the PostgreSQL integration tests

## Configure PostgreSQL

Create a database such as `equipment_borrowing` and a user with access to it. Configure the connection string using one of these local options:

1. Set the `EQUIPMENT_BORROWING_CONNECTION_STRING` environment variable.
2. Set `ConnectionStrings__EquipmentBorrowing` as an environment variable.
3. Create `EquipmentBorrowing.Desktop/appsettings.Local.json` from this example. The file is ignored by Git:

```json
{
  "ConnectionStrings": {
    "EquipmentBorrowing": "Host=localhost;Port=5432;Database=equipment_borrowing;Username=<your-user>;Password=<your-password>"
  }
}
```

Do not commit passwords or secrets. The committed `appsettings.json` intentionally has an empty connection string. The environment variable takes precedence over JSON configuration.

In PowerShell, for the current terminal session:

```powershell
$env:EQUIPMENT_BORROWING_CONNECTION_STRING = "Host=localhost;Port=5432;Database=equipment_borrowing;Username=<your-user>;Password=<your-password>"
```

On the first application start, EF Core applies the PostgreSQL migrations. Sample records are inserted only when the Students, Equipment, and Borrowings tables are all empty. Existing records are not reset. The former SQLite file under `%LOCALAPPDATA%/EquipmentBorrowing/Data` is left untouched and is not automatically copied to PostgreSQL.

## Build and run

From the repository root:

```powershell
dotnet restore EquipmentBorrowing.slnx
dotnet build EquipmentBorrowing.slnx
dotnet run --project EquipmentBorrowing.Desktop
```

The desktop interface has tabs for Equipment, Students, Checkout & Returns, and Histories & Search. The `EquipmentBorrowing.Demo` console project remains an in-memory Activity 2 demonstration; the Avalonia Desktop project is the PostgreSQL application.

## EF Core migrations

The active PostgreSQL migration and model snapshot are in `EquipmentBorrowing.Infrastructure/Persistence/Migrations/PostgreSql`. The previous SQLite migration source is retained in `Persistence/Migrations` as legacy Activity 3 history and excluded from the active migration assembly. No SQLite-to-PostgreSQL data migration is performed.

Set `EQUIPMENT_BORROWING_CONNECTION_STRING`, restore the local tool, then inspect/apply migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations list --project EquipmentBorrowing.Infrastructure --startup-project EquipmentBorrowing.Infrastructure
dotnet tool run dotnet-ef database update --project EquipmentBorrowing.Infrastructure --startup-project EquipmentBorrowing.Infrastructure
```

Create a later migration after changing the model:

```powershell
dotnet tool run dotnet-ef migrations add MigrationName --project EquipmentBorrowing.Infrastructure --startup-project EquipmentBorrowing.Infrastructure --output-dir Persistence/Migrations/PostgreSql
```

The EF design-time factory requires the environment connection string. Migration scaffolding does not need to connect to PostgreSQL; `database update` does.

## Architecture

```text
Avalonia Views and MVVM ViewModels
               ↓
          Application services
               ↓
       Repository abstractions
               ↓
 EF Core repositories and transactions
               ↓
           PostgreSQL
```

Views bind to ViewModels. Application workflows depend on repository interfaces. Infrastructure creates a short-lived `DbContext` per operation through `IDbContextFactory`. The checkout repository re-reads and locks the student and selected equipment in one PostgreSQL transaction, validates the current active count and availability, and saves all borrowing rows and equipment states together. A filtered unique index prevents multiple active borrowings for the same equipment item. The return workflow also updates borrowing status, actual return date, and equipment availability in one transaction.

Dependency injection registers `IDbContextFactory<EquipmentBorrowingDbContext>`; each repository operation creates and disposes its own context so concurrent UI operations do not share a context. Views only bind to ViewModels, and ViewModels call application services without configuring or migrating the database.

The schema includes normalized unique student/equipment identifiers, required-field and date checks, status/return-time consistency, foreign keys with restricted deletes, search indexes, and the active-equipment unique index. The maximum of three active items is enforced in the application transaction while holding the student row lock.

Borrowing dates are local calendar dates. The application compares expected return dates against local `DateTime.Today` and records checkout/return dates at UTC midnight in PostgreSQL `timestamp with time zone` columns. Values are treated as date-only and must not be converted between time zones.

## Tests

Run unit and isolated persistence tests:

```powershell
dotnet test EquipmentBorrowing.slnx
```

The SQLite provider in the test project is used only for fast isolated EF persistence checks. It is not registered in the desktop application. PostgreSQL integration tests are discovered but skipped when `EQUIPMENT_BORROWING_TEST_CONNECTION_STRING` is unset. When set, the tests create a unique temporary schema, apply migrations, verify constraints and persistence, inject a transactional write failure, and drop that schema afterward. Use a disposable PostgreSQL database/user with permission to create and drop schemas:

```powershell
$env:EQUIPMENT_BORROWING_TEST_CONNECTION_STRING = "Host=localhost;Port=5432;Database=equipment_borrowing_test;Username=<test-user>;Password=<test-password>"
dotnet test EquipmentBorrowing.slnx
```

The application does not enable automatic database retries for checkout or return. If commit confirmation is lost, Infrastructure reconnects using a fresh context to inspect the operation ID. If the state still cannot be determined, the interface reports an unknown outcome and refreshes the lists without repeating the operation.

## Known verification limits

The project audit and verification report record which requirements were implemented and which checks were actually run. The final PostgreSQL integration suite passed against a disposable local server. The official Activity 4 PDFs were not available for comparison; the UI startup was observed, but a full interactive walkthrough and screenshots were not captured. The unknown-commit network-loss path remains untested. UC-10 and UC-11 are deferred as specified in the pasted Activity 4 requirements.

## Troubleshooting

- **Connection refused:** Confirm PostgreSQL is running, the host/port are correct, and the configured database exists. The application reads `EQUIPMENT_BORROWING_CONNECTION_STRING` first, then standard connection-string environment configuration and local JSON settings.
- **Migration or permission error:** Use a database role that can create tables, indexes, and constraints in the target database. Startup applies migrations automatically; running the documented `dotnet-ef database update` command manually is optional.
- **Integration tests are skipped:** Set `EQUIPMENT_BORROWING_TEST_CONNECTION_STRING`. The test role must be able to create and drop schemas in a disposable database.
- **No sample rows appear:** The initializer seeds only when Students, Equipment, and Borrowings are all empty. It never overwrites existing rows.
- **A query fails while PostgreSQL is unavailable:** The interface reports a database error separately from an empty result. Restore the connection and use Refresh / Retry.
