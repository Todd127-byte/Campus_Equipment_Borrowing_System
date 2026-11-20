# Use Case Suite - Activity 4

## Scope note

This suite expands the use-case details present in the task text. `ITSD81 Lab Activity 4.pdf` and `ITSD81 Lab Activity 4 Use Case Suite.pdf` were not available in the repository or checked document folders, so exact official wording, appendices, and rubric traceability remain to be compared with the source PDFs.

**Primary actor for all cases:** Laboratory Staff / Equipment Custodian.

## Tabular use-case suite

The table gives each in-scope case's preconditions, staff interaction, success outcome, alternatives, rules, and acceptance result. Detailed numbered scenarios follow by use-case ID. Acceptance labels point to the evidence ledger; PARTIAL means the stated unverified subcases remain.

| Use case | Preconditions | Staff interaction and main success | Alternatives and exceptions | Postcondition | Rules | Acceptance |
| --- | --- | --- | --- | --- | --- | --- |
| UC-01 View Equipment Catalog | App is open and PostgreSQL is configured. | Staff opens Equipment or refreshes; all registered item identities and availability load. | Empty catalog has its own state; database errors show retry feedback. | No data changes. | BR-05, BR-06, BR-07, BR-19 | A01 PARTIAL |
| UC-02 Find Available Equipment | App is open. | Staff opens available equipment; only currently available items appear. | No matches and database errors have separate feedback; retry is available. | No data changes. | BR-10, BR-17, BR-19 | A02 PARTIAL |
| UC-03 Borrow Equipment | Student and item exist; student is eligible and below limit; item is available. | Staff selects a student, one item, and future due date; transaction revalidates, records checkout, and marks item unavailable. | Missing/stale, restricted, unavailable, over-limit, or invalid-date request is rejected without writes; unknown commit is not retried. | One active borrowing exists and item is unavailable. | BR-08–BR-11, BR-15, BR-16, BR-18, BR-19 | A03 PARTIAL |
| UC-04 View Active Borrowings | App is open. | Staff opens Checkout & Returns; active rows load with student and item details. | Returned rows are excluded; empty and database-error states are distinct. | No data changes. | BR-14, BR-19 | A04 PARTIAL |
| UC-05 Return Equipment | Selected borrowing is active and references existing equipment. | Staff selects an active row and returns it; transaction stores local return date and marks equipment available. | Repeated/stale return is rejected; restricted student may still return; unknown commit is not retried. | Borrowing remains in history as returned. | BR-04, BR-13–BR-16, BR-18, BR-19 | A05 PARTIAL |
| UC-06 Check Out Multiple Equipment Items | Student and all selected items exist; student is eligible; total count stays within limit. | Staff selects one student, at least two distinct items, and one due date; all rows and availability changes commit together. | Invalid item, student, date, duplicate, count, write failure, or concurrency conflict creates no partial checkout. | Each requested item has one active record, or none were changed. | BR-08–BR-12, BR-15, BR-16, BR-18, BR-19 | A06 PASS |
| UC-07 View Student Borrowing History | Student is registered. | Staff selects a student and loads active and returned records with linked equipment and dates. | Empty/missing results are distinct from database errors; retry is available. | No data changes. | BR-03, BR-14, BR-19 | A07 PARTIAL |
| UC-08 View Equipment Borrowing History | Equipment is registered. | Staff selects an item and loads active and returned records with linked students and dates. | Empty/missing results are distinct from database errors; results remain item-scoped. | No data changes. | BR-14, BR-19 | A08 PARTIAL |
| UC-09 Search, Filter, and Sort Borrowings | App is open. | Staff combines partial text, status, sort field, and direction; matching rows load asynchronously. | No matches are explicit; reset restores defaults; database errors show retry feedback. | No data changes. | BR-17, BR-19 | A09 PARTIAL |
| UC-12 Register Student | Staff can access Students. | Staff enters unique institutional number, full name, and optional contact; new student is eligible. | Blank required values, whitespace-only supplied contact, duplicates, and database errors are reported. | Student details persist. | BR-01, BR-02, BR-16, BR-19 | A12 PARTIAL |
| UC-13 Update Student Information | Student is registered and selected. | Staff updates number/name/contact; blank contact clears the stored value. | Blank required values, duplicates, stale student, or database errors leave stored data unchanged. | Stable ID and borrowing history remain. | BR-01–BR-03, BR-16 | A13 PASS |
| UC-14 Change Student Borrowing Eligibility | Student is registered and selected. | Staff permits or restricts the student; repeated choice is idempotent. | Restricted student cannot borrow new items but can return existing items; stale/database error is reported. | Eligibility changes; existing history and active borrowings remain. | BR-04, BR-08, BR-14 | A14 PASS |
| UC-15 Register Equipment | Staff can access Equipment. | Staff enters unique identifier, name, and optional description; item starts available without active borrowing. | Blank required values, duplicates, or database errors are reported without creating a row. | New item persists as available. | BR-05, BR-06, BR-16 | A15 PARTIAL |
| UC-16 Update Equipment Information | Equipment is registered and selected. | Staff edits identifier/name/description; blank description clears. | Blank required values, duplicates, stale item, or database errors leave stored fields unchanged; availability cannot be edited. | Stable ID, availability, and borrowing history remain. | BR-05, BR-07, BR-14, BR-16 | A16 PASS |

UC-10 and UC-11 are deferred to Laboratory Activity 5. UC-17 through UC-27 and other future-scope features listed in the task prompt are excluded from this suite.

## UC-01 - View Equipment Catalog

**Goal:** Review registered equipment and its current availability.

**Preconditions:** The application is open. PostgreSQL is configured for persisted data.

**Trigger:** Staff opens the Equipment tab or selects Refresh.

**Main success scenario:**

1. The application loads equipment asynchronously.
2. The catalog shows each item's identifier, name, description, and availability.
3. Staff selects an item to view or edit its descriptive fields.

**Alternatives and exceptions:** An empty catalog displays an empty state. A database or connection error appears in the status area and staff can retry.

**Postconditions:** No data changes.

**Rules:** BR-05, BR-06, BR-07, BR-19.

## UC-02 - Find Available Equipment

**Goal:** See which equipment can be checked out now.

**Preconditions:** The application is open.

**Trigger:** Staff opens the Equipment or Checkout & Returns tab or refreshes the available list.

**Main success scenario:**

1. The application requests available equipment from the repository.
2. Only equipment whose persisted availability is true is shown.
3. Staff reviews the results and may select items for checkout.

**Alternatives and exceptions:** No available equipment displays an empty state. If the query fails, staff receives an error and can retry.

**Postconditions:** No data changes.

**Rules:** BR-10, BR-17, BR-19.

## UC-03 - Borrow Equipment

**Goal:** Check out one equipment item to an eligible student.

**Preconditions:** Student and equipment are registered. Student is eligible and has fewer than three active borrowings. Equipment is available.

**Trigger:** Staff chooses one student, exactly one available item, a future expected-return date, and submits.

**Main success scenario:**

1. Application validates the submitted values and current student/item state.
2. Infrastructure opens a database transaction, locks the student and equipment rows, and repeats the checks against current database state.
3. Infrastructure inserts one active borrowing and marks the item unavailable in the same transaction.
4. The transaction commits and the UI refreshes inventory and active borrowings.

**Alternatives and exceptions:** Missing student/item, restricted eligibility, unavailable item, invalid date, or borrowing limit produces a validation message and no committed changes. A concurrent request that wins the equipment lock makes the later request fail cleanly. If commit outcome cannot be established, the application inspects a fresh connection, reports unknown if still unresolved, and does not repeat checkout.

**Postconditions:** On success, one active record exists and the equipment is unavailable.

**Rules:** BR-08, BR-09, BR-10, BR-11, BR-15, BR-16, BR-18, BR-19.

## UC-04 - View Active Borrowings

**Goal:** Review all current checkouts and act on an active record.

**Preconditions:** The application is open.

**Trigger:** Staff opens the Checkout & Returns tab or refreshes.

**Main success scenario:**

1. The application asynchronously joins active borrowing records with student and equipment identity.
2. Staff sees the student, equipment, identifiers, and due date.
3. Staff selects one record to return it.

**Alternatives and exceptions:** An empty result shows that there are no active borrowings. Database errors are surfaced with a retry action.

**Postconditions:** No data changes.

**Rules:** BR-14, BR-19.

## UC-05 - Return Equipment

**Goal:** Record an item's return and make it available again.

**Preconditions:** The selected borrowing is active and references existing equipment.

**Trigger:** Staff selects an active borrowing and chooses Return.

**Main success scenario:**

1. Infrastructure starts a transaction and locks the borrowing and equipment rows.
2. It confirms the borrowing is still active.
3. It records the actual return date, changes the status to Returned, and marks the equipment available.
4. All changes commit together; the active list and catalog refresh.

**Alternatives and exceptions:** A missing or already returned record is rejected without changes. A restricted student can still return an existing item. Unknown commit results are inspected through a fresh context and are never automatically retried.

**Postconditions:** The borrowing remains as history with its actual return date; equipment is available.

**Rules:** BR-04, BR-13, BR-14, BR-15, BR-16, BR-18, BR-19.

## UC-06 - Check Out Multiple Equipment Items

**Goal:** Check out two or more distinct available items to one eligible student as one all-or-nothing operation.

**Preconditions:** The student is registered and eligible; each item is registered and available; total active count after checkout will not exceed three.

**Trigger:** Staff selects a student, at least two distinct available items, a future expected-return date, and submits multiple checkout.

**Main success scenario:**

1. Application rejects fewer than two items, duplicate selections, missing records, restricted students, unavailable items, invalid dates, and requests that exceed the limit.
2. Infrastructure begins one PostgreSQL transaction and locks the student and all requested equipment in stable ID order.
3. It reloads and revalidates eligibility, capacity, and availability while holding the locks.
4. It adds every active borrowing and updates every selected equipment row inside the transaction.
5. All changes commit together, with a shared checkout batch identifier.
6. UI refreshes equipment and borrowing views.

**Alternatives and exceptions:** Any failed validation or write rolls back every selected item. Concurrent requests serialize on the student and item rows; the filtered unique active-equipment index is a final database guard. A lost commit response triggers a fresh-context inspection of the batch ID. The UI never repeats an uncertain operation.

**Postconditions:** On success, every requested item has exactly one new active borrowing and is unavailable. On failure, no requested item is changed by the transaction.

**Rules:** BR-08 through BR-12, BR-15, BR-16, BR-18, BR-19.

## UC-07 - View Student Borrowing History

**Goal:** Review active and returned records for one selected student.

**Preconditions:** The student is registered.

**Trigger:** Staff selects a student and chooses Load student history.

**Main success scenario:**

1. Application loads records filtered by the selected student ID.
2. Results show student number/name, equipment identity, checkout date, expected return date, status, and actual return date when present.

**Alternatives and exceptions:** A student with no records sees an empty state. Database errors allow retry. Refreshing the student selector preserves identity by database ID.

**Postconditions:** No data changes.

**Rules:** BR-03, BR-14, BR-19.

## UC-08 - View Equipment Borrowing History

**Goal:** Review all active and returned records for one selected equipment item.

**Preconditions:** Equipment is registered.

**Trigger:** Staff selects equipment and chooses Load equipment history.

**Main success scenario:**

1. Application loads records filtered by the selected equipment ID.
2. Results show equipment identity, student, checkout date, expected return date, status, and actual return date when present.

**Alternatives and exceptions:** An item without history shows an empty state. Database errors allow retry. Results for other items are excluded by the equipment ID filter.

**Postconditions:** No data changes.

**Rules:** BR-14, BR-19.

## UC-09 - Search, Filter, and Sort Borrowings

**Goal:** Find borrowing records using a combined search term, status filter, and date ordering.

**Preconditions:** The application is open.

**Trigger:** Staff enters a search term, chooses All/Active/Returned, chooses checkout or expected-return date, sets direction, and selects Search.

**Main success scenario:**

1. Application asynchronously queries borrowing, student, and equipment data.
2. Search matches student name, institutional student number, or equipment name.
3. Status filter and sort order apply to the same result set.
4. Staff reviews results or selects Clear filters / reset.

**Alternatives and exceptions:** No matches show an empty state. Query errors show the shared error status and staff can refresh or retry.

**Postconditions:** No data changes.

**Rules:** BR-17, BR-19.

## UC-12 - Register Student

**Goal:** Register a student for future borrowing.

**Preconditions:** Staff can access Students.

**Trigger:** Staff enters institutional student number and full name, optionally contact information, then submits.

**Main success scenario:**

1. Application rejects blank required values and normalizes the number by trimming and case-folding.
2. Application checks for a duplicate and PostgreSQL enforces a unique normalized-number index.
3. A new student is saved as eligible with the three-item borrowing limit.
4. UI refreshes the student list.

**Alternatives and exceptions:** Duplicate normalized number or database failure is reported; no duplicate row is created.

**Postconditions:** Student identity and details persist.

**Rules:** BR-01, BR-02, BR-16, BR-19.

## UC-13 - Update Student Information

**Goal:** Edit a student's number, name, or optional contact details without changing student identity or history.

**Preconditions:** Student is registered and selected.

**Trigger:** Staff edits permitted fields and submits.

**Main success scenario:**

1. Application validates the required number and name.
2. It checks normalized number uniqueness excluding the selected student.
3. The same student ID is updated; contact may be cleared.
4. Existing borrowing links remain unchanged.

**Alternatives and exceptions:** Blank required values, duplicate number, stale student, or database error leaves the stored record unchanged.

**Postconditions:** Student details persist; borrowing history remains linked to the same ID.

**Rules:** BR-01, BR-02, BR-03, BR-16.

## UC-14 - Change Student Borrowing Eligibility

**Goal:** Permit or restrict new checkouts for one student.

**Preconditions:** Student is registered and selected.

**Trigger:** Staff chooses Allow borrowing or Restrict borrowing.

**Main success scenario:**

1. Application loads the selected student's current state.
2. It changes eligibility only when the requested state differs.
3. New checkout requests revalidate eligibility inside their database transaction.
4. Existing active borrowings remain, and returns remain available.

**Alternatives and exceptions:** Repeating the current state makes no change. A missing student or database error is reported.

**Postconditions:** Eligibility is updated; existing history and active items are preserved.

**Rules:** BR-04, BR-08, BR-14.

## UC-15 - Register Equipment

**Goal:** Add equipment to the catalog.

**Preconditions:** Staff can access Equipment.

**Trigger:** Staff enters identifier and name, optionally description, then submits.

**Main success scenario:**

1. Application validates required values and normalizes the identifier.
2. It checks for a duplicate and PostgreSQL enforces uniqueness.
3. New equipment is saved as available and has no borrowing record.
4. UI refreshes the catalog and available list.

**Alternatives and exceptions:** Blank required values, duplicate identifier, or database error is reported without creating a duplicate item.

**Postconditions:** The new item persists as available.

**Rules:** BR-05, BR-06, BR-16.

## UC-16 - Update Equipment Information

**Goal:** Edit equipment identifier, name, or description without altering availability or history.

**Preconditions:** Equipment is registered and selected.

**Trigger:** Staff edits descriptive fields and submits.

**Main success scenario:**

1. Application validates required identifier and name.
2. It checks normalized identifier uniqueness excluding the selected item.
3. It updates the same equipment ID and descriptive fields only.
4. Availability and borrowing references remain unchanged.

**Alternatives and exceptions:** Blank required fields, duplicate identifier, stale item, or database error leaves persisted state unchanged.

**Postconditions:** Descriptive values persist; availability and history remain intact.

**Rules:** BR-05, BR-07, BR-14, BR-16.
