# Demonstration Guide

This sequence covers the 14 in-scope use cases summarized in the task text. Adjust labels and ordering to match the official integrated demonstration sequence once the source PDF is available.

## Prepare

1. Start PostgreSQL and create an empty `equipment_borrowing` database.
2. Configure `EQUIPMENT_BORROWING_CONNECTION_STRING` or ignored `appsettings.Local.json`.
3. Run `dotnet build EquipmentBorrowing.slnx` and `dotnet run --project EquipmentBorrowing.Desktop`.
4. Confirm startup applies migrations and seeds only an empty database.

## Walkthrough

1. **UC-01 / UC-02:** Open Equipment. Show the complete catalog and the separate available-only list.
2. **UC-15 / UC-16:** Register an equipment item. Select it, edit its description, and confirm availability is not editable.
3. **UC-12 / UC-13 / UC-14:** Register a student. Change contact/name, clear contact, then restrict the student. Attempt checkout and show it is rejected. Restore eligibility.
4. **UC-03:** Borrow one available item using a student and future due date. Confirm the item becomes unavailable and an active row appears.
5. **UC-06:** Select two distinct available items and submit multi-checkout. Confirm both appear and both disappear from the available list together.
6. **UC-04 / UC-05:** Review active borrowings, return one item, and confirm it leaves the active list while remaining in history with an actual return date.
7. **UC-07 / UC-08:** Load history by student, then by equipment. Confirm each result set stays scoped to the selected identity.
8. **UC-09:** Search by student name/number/equipment name; combine with Active/Returned, expected-return/checkout date, ascending/descending; then reset.
9. Verify the database state with fresh EF contexts or SQL readbacks. Do not treat screenshots alone as proof of persistence or transaction atomicity.

## PostgreSQL transaction evidence

Run the opt-in PostgreSQL integration tests against a disposable database configured by `EQUIPMENT_BORROWING_TEST_CONNECTION_STRING`. The trigger-failure test verifies that writes attempted for a multi-item checkout roll back together. Retain the test output and database readbacks alongside the walkthrough screenshots.
