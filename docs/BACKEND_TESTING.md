# Backend Testing

Backend tests are in `StudentPlanner.Tests/BackendIntegrationTests.cs`; existing frontend/service tests remain in `FrontendServiceTests.cs`.

Most integration tests use Microsoft.Data.Sqlite with `Data Source=:memory:` and hold the SQLite connection open for the full test lifetime. Each test applies the real checked-in EF migration. A separate file-backed test uses a uniquely named disposable SQLite file, applies migrations twice, then creates, updates, reads, and deletes course, assignment, grade, and schedule records using new factories/services. This verifies persistence across independent service/context lifetimes without touching a user database. Together these tests exercise SQLite foreign keys, unique indexes, migration metadata and provider behavior rather than relying only on EF's non-relational InMemory provider.

Coverage includes:

- Initial migration and records loaded from a fresh context.
- File-backed records survive creating fresh context factories and service instances; migrations are safe to apply again.
- SQLite foreign-key enforcement and invalid course relationships.
- Unique course codes, updates, delete restrictions, and schedule `SET NULL` behavior.
- Assignment create/update/completion and missing-record results.
- Grade validation, credit normalization, GPA calculation and CRUD.
- Personal calendar events, linked events, date-range queries and overlap rejection.
- Dashboard counts, deadline projection, completion changes, settings scale and GPA synchronization.
- Recommendation score, reason, completed exclusion, deterministic ordering and urgency.
- Priority weights and scores reject negative or non-finite values.

The integration tests do not automate interactive browser rendering. Use `docs/FRONTEND_TESTING.md` for the remaining manual browser checklist.

Run all tests with:

```powershell
dotnet restore StudentPlanner.slnx
dotnet test StudentPlanner.slnx
```

Current verification outcomes must be recorded in `docs/STUDENT1_REPORT.md`; do not infer results from a build alone.
