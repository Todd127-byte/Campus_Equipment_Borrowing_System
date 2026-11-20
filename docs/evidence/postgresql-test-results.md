# PostgreSQL Test Results

Run date: 2026-11-20 (Asia/Manila)

The suite ran against a disposable local PostgreSQL 18 cluster. The connection string was provided only through `EQUIPMENT_BORROWING_TEST_CONNECTION_STRING`; no database credentials are committed. Each integration test created a random schema, applied the migration, verified state through fresh contexts/connections, and dropped the schema.

Command:

```powershell
$env:EQUIPMENT_BORROWING_TEST_CONNECTION_STRING = "Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres;Password="
dotnet test EquipmentBorrowing.slnx --no-build --no-restore
```

Final output:

```text
Test run for C:\Users\Ted\source\repos\EquipmentBorrowing\EquipmentBorrowing.Tests\bin\Debug\net10.0\EquipmentBorrowing.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    23, Skipped:     0, Total:    23, Duration: 1 s - EquipmentBorrowing.Tests.dll (net10.0)
```

The suite includes 18 workflow tests, one isolated EF/SQLite persistence test, and four PostgreSQL tests covering migration/constraints, rollback, concurrency, management, history, and search. SQLite is not used by the desktop application.

The final full solution build completed with **0 warnings and 0 errors**. See [the verification report](../verification-report.md) for scope and limitations, including the untested unknown-commit network-loss path.
