-- PostgreSQL readbacks for the Activity 4 schema.
-- Replace the example values with IDs from the current database.

-- 1. UC-01: equipment catalog.
SELECT "Id", "EquipmentIdentifier", "Name", "Description", "IsAvailable"
FROM "Equipment"
ORDER BY "EquipmentIdentifier";

-- 2. UC-02: available equipment only.
SELECT "Id", "EquipmentIdentifier", "Name", "Description"
FROM "Equipment"
WHERE "IsAvailable" = TRUE
ORDER BY "Name";

-- 3. UC-04: active borrowing rows with student and equipment details.
SELECT s."InstitutionalStudentNumber", s."Name" AS "Student",
       e."EquipmentIdentifier", e."Name" AS "Equipment",
       b."DateBorrowed", b."ExpectedReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Students" AS s ON s."Id" = b."StudentId"
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
WHERE b."Status" = 0
ORDER BY b."ExpectedReturnDate", b."Id";

-- 4. UC-07: one student's complete history (set the selected student ID).
SELECT s."InstitutionalStudentNumber", s."Name" AS "Student",
       e."EquipmentIdentifier", e."Name" AS "Equipment",
       b."DateBorrowed", b."ExpectedReturnDate", b."ActualReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Students" AS s ON s."Id" = b."StudentId"
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
WHERE b."StudentId" = 1
ORDER BY b."DateBorrowed" DESC, b."Id";

-- 5. UC-08: one equipment item's complete history (set the selected item ID).
SELECT e."EquipmentIdentifier", e."Name" AS "Equipment",
       s."InstitutionalStudentNumber", s."Name" AS "Student",
       b."DateBorrowed", b."ExpectedReturnDate", b."ActualReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
JOIN "Students" AS s ON s."Id" = b."StudentId"
WHERE b."EquipmentId" = 101
ORDER BY b."DateBorrowed" DESC, b."Id";

-- 6. UC-09: searchable rows for a term. The application composes the status and date predicates.
SELECT b."Id", s."InstitutionalStudentNumber", s."Name" AS "Student",
       e."EquipmentIdentifier", e."Name" AS "Equipment",
       b."DateBorrowed", b."ExpectedReturnDate", b."ActualReturnDate", b."Status"
FROM "Borrowings" AS b
JOIN "Students" AS s ON s."Id" = b."StudentId"
JOIN "Equipment" AS e ON e."Id" = b."EquipmentId"
WHERE lower(s."Name") LIKE '%term%'
   OR lower(s."InstitutionalStudentNumber") LIKE '%term%'
   OR lower(e."Name") LIKE '%term%'
ORDER BY b."DateBorrowed" DESC, b."Id";

-- 7. Active count for each student.
SELECT s."Id", s."InstitutionalStudentNumber", s."Name",
       COUNT(b."Id") FILTER (WHERE b."Status" = 0) AS "ActiveBorrowingCount"
FROM "Students" AS s
LEFT JOIN "Borrowings" AS b ON b."StudentId" = s."Id"
GROUP BY s."Id", s."InstitutionalStudentNumber", s."Name"
ORDER BY s."Name";

-- 8. Confirm migration history and database constraints.
SELECT "MigrationId", "ProductVersion"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";

SELECT conrelid::regclass AS "Table", conname AS "Constraint", contype AS "Type"
FROM pg_constraint
WHERE connamespace = current_schema()::regnamespace
ORDER BY "Table", "Constraint";

-- 9. Show the partial unique active-borrowing index.
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = current_schema() AND tablename = 'Borrowings'
ORDER BY indexname;
