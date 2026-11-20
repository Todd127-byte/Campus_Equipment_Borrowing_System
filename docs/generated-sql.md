# PostgreSQL Query Shapes

The current application uses EF Core/Npgsql with asynchronous LINQ queries. The examples below describe the SQL semantics of the repository queries; they are illustrative, not captured provider logs. The legacy Activity 3 SQLite command log is preserved separately in `legacy-activity3-generated-sql.md`.

## Available equipment

Source: `EfEquipmentRepository.GetAvailableAsync`.

```sql
SELECT e."Id", e."EquipmentIdentifier", e."Name", e."Description", e."IsAvailable"
FROM "Equipment" AS e
WHERE e."IsAvailable" = TRUE
ORDER BY e."Name";
```

## Active borrowing summary

Source: `EfBorrowingRepository.GetActiveSummariesAsync`.

```sql
SELECT b."Id", s."Id", s."InstitutionalStudentNumber", s."Name",
       e."Id", e."EquipmentIdentifier", e."Name",
       b."DateBorrowed", b."ExpectedReturnDate", b."ActualReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Students" AS s ON s."Id" = b."StudentId"
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
WHERE b."Status" = 0
ORDER BY b."ExpectedReturnDate", b."Id";
```

## Search, status filter, and sort

Source: `EfBorrowingRepository.SearchAsync`. The repository applies each selected filter to the same query before sorting and materializing results.

```sql
SELECT b."Id", s."InstitutionalStudentNumber", s."Name", e."EquipmentIdentifier", e."Name",
       b."DateBorrowed", b."ExpectedReturnDate", b."ActualReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Students" AS s ON s."Id" = b."StudentId"
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
WHERE b."Status" = 0
  AND (lower(s."Name") LIKE @pattern
       OR lower(s."InstitutionalStudentNumber") LIKE @pattern
       OR lower(e."Name") LIKE @pattern)
ORDER BY b."DateBorrowed" DESC, b."Id";
```

For a returned filter, the status predicate is `b."Status" = 1`; All omits it. Sort field and direction come from the UI criteria. These exact SQL strings were not captured from provider logs; the corresponding repository workflows were exercised against PostgreSQL.
