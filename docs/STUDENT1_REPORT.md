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
- ASP.NET Core Identity accounts, HTTP form registration/login/POST logout, protected Blazor routes, and account profile editing.
- Server-derived owner scoping for course, assignment, grade, schedule, dashboard and recommendation data.
- Per-account SQLite settings and an additive migration that preserves pre-Identity records as unowned.
- Authentication and multi-user security documentation and HTTP/security regression tests.

## Architecture decisions

Database services use `IDbContextFactory<AppDbContext>` and create/dispose a context per operation. Existing service interfaces and change notifications remain in place. Identity and planner data share one SQLite `AppDbContext`; browser settings were moved to per-user `UserPlannerSettings`. `CourseId` is the persisted relationship; UI-only course labels and calculated/convenience properties are excluded from EF mapping.

The GPA demonstration formula was preserved. Assignment priority is a heuristic with visible scoring factors, not an institutional or measured prediction.

## Verification status (2026-11-13)

- `dotnet restore StudentPlanner.slnx`: succeeded; projects were up to date.
- `dotnet build StudentPlanner.slnx --no-restore --verbosity minimal`: succeeded with 0 warnings and 0 errors.
- `dotnet test StudentPlanner.slnx --no-build --verbosity minimal`: 34 passed, 0 failed, 0 skipped.
- HTTP TestServer verification: registration, antiforgery, Identity password hashing, invalid login, lockout, Remember Me cookie, logout POST/cookie revocation, protected-route redirects, and cookie validation after recreating the host passed with isolated SQLite and Data Protection files.
- SQLite security verification: User A/B ownership isolation, settings separation, cross-owner CourseId rejection, Dashboard scoping, and legacy-row preservation/invisibility passed.
- Runtime: the `http` launch profile started on `http://localhost:5048` with a unique temporary database and key directory; migrations and development seeding completed, and the persistent XML key was written. ASP.NET Core emitted its expected warning that key files are not encrypted at rest. A direct local HTTP request was denied by the execution sandbox's socket permissions.
- Browser: a local browser tab previously failed to connect with `ERR_CONNECTION_TIMED_OUT`; interactive visual QA remains unverified. Static UI rendering and server auth flows are covered through HTTP integration tests.
- `dotnet ef migrations list`: both checked-in migrations are pending; the default local database file was absent at verification. No migration was applied to a pre-existing user database during this task. Application startup applies pending migrations when run, unless disabled in config.

Backend persistence, authentication HTTP flow, multi-user service authorization, and automated integration acceptance are verified. Run the documented browser checklist on the target machine to confirm interactive rendering, responsive behavior, and visual appearance.

## Student 1 contribution summary

Student 1 implemented database persistence, relational integrity, service integration, dashboard calculations, recommendations, Identity, record ownership, safe additive migrations, security tests, and backend/security documentation. No cloud services, real database resets, or commit/push operations were used.

## Final academic features
Focus Lab and Study Insights are implemented as authenticated features backed by user-scoped SQLite services and a new migration. The workload planner is deterministic and exposes capacity shortfalls. Validation status is recorded with the current test/build results after the final implementation run.

Final feature verification: solution restore completed with all packages current; solution build succeeded with 0 warnings and 0 errors; full solution tests passed 39/39. Focus Lab and Study Insights route authorization and active `aria-current` rendering are covered by HTTP integration assertions. Runtime migrations and development startup succeeded against a separate temporary SQLite database. A browser could not connect to localhost from the in-app browser, so visual browser QA remains outstanding.
