# Activity 4 Traceability Matrix

This matrix maps the BR and acceptance scenario IDs from the pasted task text. The official PDFs were not available for comparison. **COMPLETE** means implementation and relevant verification ran; **PARTIAL** means implementation exists but a material check remains; **NOT STARTED** means not implemented; **BLOCKED** means an external dependency or decision prevents progress.

## Use case-to-acceptance traceability

| Use case | Screen/workflow | Business rules | Acceptance scenario | Verification evidence | Status |
| --- | --- | --- | --- | --- | --- |
| UC-01 View Equipment Catalog | Equipment tab catalog and Refresh / Retry | BR-05, BR-06, BR-07, BR-19 | A01 | Desktop startup observation; empty/error paths not exercised | PARTIAL |
| UC-02 Find Available Equipment | Equipment tab available list; Checkout & Returns selector | BR-10, BR-17, BR-19 | A02 | Startup list plus repository filtering; no-match/error states not exercised | PARTIAL |
| UC-03 Borrow Equipment | Checkout & Returns single-item action | BR-08, BR-09, BR-10, BR-11, BR-15, BR-16, BR-18, BR-19 | A03 | Workflow and PostgreSQL transaction/persistence tests | COMPLETE |
| UC-04 View Active Borrowings | Checkout & Returns active list | BR-14, BR-19 | A04 | Active-summary query and returned-row exclusion checks | COMPLETE |
| UC-05 Return Equipment | Checkout & Returns selected-row return action | BR-13, BR-14, BR-15, BR-16, BR-18 | A05 | PostgreSQL return, availability/history readback, and repeated-return rejection | COMPLETE |
| UC-06 Check Out Multiple Equipment Items | Checkout & Returns multi-item selection/action | BR-08–BR-12, BR-15, BR-16, BR-18 | A06 | PostgreSQL batch, rollback-trigger, concurrency, and workflow rejection tests | COMPLETE |
| UC-07 View Student Borrowing History | Histories & Search student history selector | BR-14, BR-19 | A07 | Two-student scoped history, empty/missing IDs, active/returned DB readback | COMPLETE |
| UC-08 View Equipment Borrowing History | Histories & Search equipment history selector | BR-14, BR-19 | A08 | Two-item scoped history, empty/missing IDs, returned-history DB readback | COMPLETE |
| UC-09 Search, Filter, and Sort Borrowings | Histories & Search criteria and reset controls | BR-17, BR-19 | A09 | Field/status/sort/tie tests and combined PostgreSQL query; UI reset not manually exercised | PARTIAL |
| UC-12 Register Student | Students tab registration form | BR-01, BR-02 | A12 | PostgreSQL registration/default/unique checks and workflow validation | COMPLETE |
| UC-13 Update Student Information | Students tab selected-student edit form | BR-02, BR-03 | A13 | PostgreSQL update/contact clear/history plus invalid update tests | COMPLETE |
| UC-14 Change Student Borrowing Eligibility | Students tab permit/restrict action | BR-04 | A14 | PostgreSQL restricted checkout, existing return, and repeated-state checks | COMPLETE |
| UC-15 Register Equipment | Equipment tab registration form | BR-05, BR-06 | A15 | PostgreSQL registration/default/unique checks and workflow validation | COMPLETE |
| UC-16 Update Equipment Information | Equipment tab selected-item edit form | BR-05, BR-07, BR-14 | A16 | PostgreSQL description clear, availability, and history checks | COMPLETE |

| Requirement | Business rule | Use case | Implementation component | Verification scenario / test | Evidence | Result |
| --- | --- | --- | --- | --- | --- | --- |
| Student number uniqueness | BR-01 | UC-12, UC-13 | `StudentConfiguration`, `EfStudentRepository`, `ManageStudentsService` | Normalized duplicates rejected by service and PostgreSQL unique index | workflow + PostgreSQL integration tests | COMPLETE |
| Required student number/name; contact optional | BR-02 | UC-12, UC-13 | `ManageStudentsService`, `StudentConfiguration` | Blank required values and whitespace-only registration contact rejected; empty contact clears/means absent; migration applies required-field constraints | workflow + PostgreSQL integration tests | COMPLETE |
| Student edits preserve identity/history | BR-03 | UC-13 | `ManageStudentsService`, `EfStudentRepository` | PostgreSQL edit and contact clear preserve borrowing FK/history | management integration test | COMPLETE |
| Restriction blocks new checkout, permits return | BR-04 | UC-05, UC-14 | `ManageStudentsService`, `BorrowEquipmentService`, `EfBorrowingRepository` | Restricted checkout rejected; existing item returned; eligibility persists | workflow + management integration tests | COMPLETE |
| Equipment identifier uniqueness | BR-05 | UC-15, UC-16 | `EquipmentConfiguration`, `EfEquipmentRepository`, `ManageEquipmentService` | Normalized duplicate rejected by service and PostgreSQL unique index | PostgreSQL integration tests | COMPLETE |
| Required equipment fields; new item available | BR-06 | UC-15 | `ManageEquipmentService`, `EquipmentConfiguration` | Blank required field rejected; new PostgreSQL item available by default | workflow + management integration tests | COMPLETE |
| Equipment edits cannot change availability | BR-07 | UC-16 | `Equipment.UpdateInformation`, `EfEquipmentRepository.UpdateAsync` | Updates preserve available and borrowed states and history | workflow + management integration tests | COMPLETE |
| Eligible existing student/item required | BR-08 | UC-03, UC-06 | `BorrowEquipmentService`, `EfBorrowingRepository.CreateCheckoutAsync` | Missing student, restricted student, and unavailable equipment rejected; state revalidated in transaction | workflow + PostgreSQL concurrency tests | COMPLETE |
| Maximum three active items | BR-09 | UC-03, UC-06 | Student row lock and active count in `EfBorrowingRepository` | Existing limit and concurrent student requests leave no more than three active | workflow + PostgreSQL concurrency test | COMPLETE |
| Unavailable equipment rejected | BR-10 | UC-03, UC-06 | Checkout validation, row lock, filtered unique index | Unit rejection and two-student race for the same item | workflow + PostgreSQL concurrency tests | COMPLETE |
| Expected return after checkout | BR-11 | UC-03, UC-06 | `BorrowingRules`, application validation, `CK_Borrowings_ValidDates` | Local calendar date is compared to local today and stored at UTC midnight; PostgreSQL rejects invalid date row | local-date workflow + PostgreSQL constraint test | COMPLETE |
| Multi-checkout requires at least two distinct items | BR-12 | UC-06 | `BorrowEquipmentService.BorrowMultipleEquipmentAsync` | Fewer than two and duplicate selections rejected without changes | workflow test | COMPLETE |
| Only active borrowing can be returned | BR-13 | UC-05 | `EfBorrowingRepository.ReturnAsync`, `Borrowing.MarkAsReturned` | Local return calendar date is recorded; repeated return rejected by workflow and PostgreSQL-backed path | workflow + management integration tests | COMPLETE |
| Preserve history and relationships | BR-14 | UC-05, UC-07, UC-08, UC-13, UC-16 | Restricted-delete FKs and history query models | Student/equipment histories, edits, actual return, and fresh-context readback | workflow + PostgreSQL integration tests | COMPLETE |
| Revalidate and atomically save multi-item checkout | BR-15 | UC-03, UC-06 | Transactional repository with student/item locks and one transaction | Trigger failure after first equipment update rolls all writes back; concurrent requests serialize | PostgreSQL rollback + concurrency integration tests | COMPLETE |
| Database integrity constraints | BR-16 | UC-03, UC-05, UC-06, UC-12, UC-15 | PostgreSQL EF mappings and `InitialPostgreSql` migration | Unique indexes, FK, valid-date checks, and filtered active-item unique index | PostgreSQL constraint integration test | COMPLETE |
| Search/filter/sort | BR-17 | UC-09 | `BrowseBorrowingsService`, `EfBorrowingRepository.SearchAsync` | All search fields, status filters, both sort fields/directions, combined PostgreSQL criteria, and stable borrowing-ID ties | workflow + management integration tests | COMPLETE |
| Known failures and unknown commit outcomes | BR-18 | UC-03, UC-05, UC-06 | `OperationNotCommittedException`, `OperationOutcomeUnknownException`, fresh-context inspection | Known write failure and rollback verified; forced commit connection loss not injected | rollback integration test; recovery code review | PARTIAL |
| Persistence/restart/asynchronous history | BR-19 | UC-01, UC-04, UC-07, UC-08, UC-09 | Async EF repositories and factory context lifetime | New contexts read back checkout, return, and history state; a full desktop process restart was not exercised | SQLite persistence + PostgreSQL integration tests | PARTIAL |

## Acceptance scenario index

| Scenario | Scope from task text | Verification evidence | Result |
| --- | --- | --- | --- |
| A01 | Catalog, empty/error/retry | Desktop startup catalog loaded; empty and connection-error UI states not manually walked through | PARTIAL |
| A02 | Available equipment filtering | Startup available-only list excluded unavailable item; no-match/error states were not exercised | PARTIAL |
| A03 | Single borrowing and invalid requests | Workflow and PostgreSQL tests passed; app-process restart persistence not exercised | PARTIAL |
| A04 | Active borrowing records | Active summary query/readback excludes returned ID; empty/error UI states not exercised | PARTIAL |
| A05 | Return and repeated return | Return/date/availability/history/repeat rejection passed; app-process restart not exercised | PARTIAL |
| A06 | Multiple checkout, atomicity, rollback | PostgreSQL one-batch, rollback trigger, and concurrency checks | COMPLETE |
| A07 | Student history | Two-student scoping, active/returned readback, empty/missing IDs; DB-error path not injected | PARTIAL |
| A08 | Equipment history | Two-item scoping and history readback; DB-error path not injected | PARTIAL |
| A09 | Search, filter, sort, combined criteria, reset | Combined database search/filter/sort tested; UI reset and full manual controls not walked through | PARTIAL |
| A10 | UC-10 (Activity 5) | Deferred by task text | NOT STARTED (out of scope) |
| A11 | UC-11 (Activity 5) | Deferred by task text | NOT STARTED (out of scope) |
| A12 | Student registration | Required fields, defaults, normalized duplicates, and PostgreSQL persistence; app-process restart not run | PARTIAL |
| A13 | Student editing | Duplicate prevention, contact clear, identity/history preservation | COMPLETE |
| A14 | Eligibility change | Restricted checkout and permitted existing return | COMPLETE |
| A15 | Equipment registration | Required fields, normalized duplicates, availability default, and PostgreSQL persistence; app-process restart not run | PARTIAL |
| A16 | Equipment editing | Descriptive update preserves availability and history | COMPLETE |
| A17 | PostgreSQL schema, migrations, constraints | Migration applied on clean schemas; unique, FK, date, and active-item constraints verified | COMPLETE |
| A18 | Database failure, recovery, transactions | Trigger rollback and concurrency passed; unknown commit loss remains untested | PARTIAL |
| A19 | Documentation and traceability | Deliverables present and cross-linked; official PDF comparison unavailable | PARTIAL |
