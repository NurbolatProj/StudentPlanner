# Student 1 Backend Implementation Report

## Implemented

- EF Core 10 / SQLite integration using the existing planner models and service contracts.
- `AppDbContext`, relationship/index configuration, configurable persistent database path and an initial migration.
- EF-backed course, assignment, grade, schedule and dashboard services; query contracts for assignment filtering and schedule ranges.
- Safe course deletion restrictions, optional schedule course references, foreign-key validation and calendar overlap checks.
- Non-destructive startup migration and transactional Development-only sample seeding for an entirely empty database.
- A deterministic, explainable assignment priority scorer integrated into the current Dashboard assignment card.
- SQLite relational integration tests, including file-backed persistence across fresh factories/services, and backend technical documentation.
- Priority policy validation that rejects negative or non-finite weights and recommendation scores.

## Architecture decisions

Database services use `IDbContextFactory<AppDbContext>` and create/dispose a context per operation. Existing interfaces and change notifications remain in place. Browser settings are intentionally not copied into a second database store. `CourseId` is the persisted relationship; UI-only course labels and calculated/convenience properties are excluded from EF mapping.

The GPA demonstration formula was preserved. Assignment priority is a heuristic with visible scoring factors, not an institutional or measured prediction.

## Verification status (2026-11-13)

- `dotnet restore StudentPlanner.slnx`: succeeded; projects were up to date.
- `dotnet build StudentPlanner.slnx --no-restore --verbosity minimal`: succeeded with 0 warnings and 0 errors.
- `dotnet test StudentPlanner.slnx --verbosity minimal`: 27 passed, 0 failed, 0 skipped.
- Runtime: the application previously started in Development against a disposable SQLite file, applied `InitialCreate`, and issued seed inserts. Automated file-backed integration coverage now creates/updates/deletes courses, assignments, grades, and events, closes the first set of contexts/services, then verifies persistence and migrations through a fresh factory and services. Seeder idempotency is also verified.
- Browser/HTTP: the host listened on `127.0.0.1:5087`, but a local HTTP request from this execution environment timed out. Interactive browser CRUD, visible dashboard synchronization, and browser settings persistence remain unverified here; the equivalent service/database behaviors are automated.
- The host logged a Data Protection key-ring access error for the sandboxed user profile, then continued startup. This is an environment file-permission limitation observed during the run; it did not prevent migration or app startup.

Backend persistence and automated integration acceptance are verified. Run the documented manual browser checklist during final project QA to confirm interactive rendering and UI behavior.

## Student 1 contribution summary

Student 1 implemented database persistence, relational integrity, service integration, dashboard data calculations, recommendation logic, migrations, backend tests, and backend documentation. No cloud services or database reset operations are used.
