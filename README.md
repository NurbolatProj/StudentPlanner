# Student Planner

Student Planner is a .NET 10 Blazor Interactive Server application for course planning, assignments, grades, calendar events, and student preferences. ASP.NET Core Identity manages accounts in the same EF Core 10 / SQLite database as academic records. Courses, assignments, grades, calendar events, and preferences are scoped to the signed-in account.

## Requirements

- .NET 10 SDK
- NuGet access for package restore on a fresh machine

## Run

```powershell
dotnet restore StudentPlanner.slnx
dotnet build StudentPlanner.slnx
dotnet run --project StudentPlanner/StudentPlanner.csproj
```

On startup the application applies checked-in EF migrations. In Development, legacy sample records are added only if the database has no student or academic records. They remain unowned and are not visible to registered accounts. Existing database rows are never cleared or reassigned.

The default database is `%LOCALAPPDATA%/StudentPlanner/studentplanner.db`. Configure `Database:Path` in `StudentPlanner/appsettings.json` or set `Database__Path` to an absolute path. Relative paths are rooted under the same application data directory. `Database:ApplyMigrationsOnStartup` defaults to `true`; set it to `false` only when migrations are applied externally.

## Migrations

The repository includes a local `dotnet-ef` tool manifest pinned to EF Core 10. Run from the repository root:

```powershell
dotnet tool restore
dotnet ef migrations list --project StudentPlanner/StudentPlanner.csproj
dotnet ef database update --project StudentPlanner/StudentPlanner.csproj
```

Create a later migration after changing the EF model:

```powershell
dotnet ef migrations add DescribeTheChange --project StudentPlanner/StudentPlanner.csproj --output-dir Data/Migrations
```

Do not use `EnsureCreated` or `EnsureDeleted`; migrations preserve and evolve existing data. The Identity ownership migration adds nullable owner fields so pre-account data remains intact and inaccessible to arbitrary accounts. An explicit verified legacy import workflow is not currently exposed in the UI.

## Accounts and security

Open `/register` to create an account, then sign in at `/login`. Identity hashes passwords, enforces a strong password policy, prevents duplicate email addresses, locks accounts after repeated failed sign-ins, and issues an HTTP-only authentication cookie. Login, registration, and logout use server-side Razor Pages forms with antiforgery tokens. Use the POST sign-out button in the sidebar.

Data Protection cookie keys persist in `%LOCALAPPDATA%/StudentPlanner/DataProtectionKeys`; set `DataProtection:KeysPath` if the application runs with a different writable data directory. Startup fails if the directory is unavailable rather than issuing cookies with ephemeral keys. Key files use OS filesystem permissions and are not encrypted at rest by this application.

Use `/account` to update the account display name. Email changes are intentionally not offered until a verified email-change flow is configured. Settings are stored per account in SQLite; the old shared localStorage settings key is no longer read. Read [Authentication and Security](docs/AUTHENTICATION_AND_SECURITY.md) before deploying or importing legacy data.

## Tests

```powershell
dotnet test StudentPlanner.slnx
```

Backend integration tests use disposable SQLite databases and apply the real migrations. HTTP tests cover registration, cookie sign-in/out, antiforgery, and anonymous route protection. Relational tests cover owner isolation, account settings, legacy migration preservation, CRUD, analytics, and recommendations.

## Architecture and integration

Read [Authentication and Security](docs/AUTHENTICATION_AND_SECURITY.md), [Backend Architecture](docs/BACKEND_ARCHITECTURE.md), [Database Schema](docs/DATABASE_SCHEMA.md), [Business Logic](docs/BUSINESS_LOGIC.md), [Priority Algorithm](docs/PRIORITY_ALGORITHM.md), [Backend Testing](docs/BACKEND_TESTING.md), and [Student 1 Report](docs/STUDENT1_REPORT.md). Existing frontend behavior and service interfaces are described in [Backend Integration Contract](docs/BACKEND_INTEGRATION_CONTRACT.md).

## Focus Lab
The authenticated `/focus` page provides focus, short-break, and long-break timers, optional assignment linking, persisted session history, and per-account focus totals. `FocusSessionService` validates selected assignment ownership, stores the authenticated owner ID on the server, and uses `Stopwatch.GetTimestamp()` to measure active duration across pause/resume. Session records use a nullable assignment FK with `SET NULL`; account deletion is restricted by the Identity owner FK.

## Study Insights
The authenticated `/insights` page analyzes pending assignments and scheduled events for 7, 14, or 30 days. `WorkloadPlanner.Analyze` is a deterministic pure function: sort by due date, priority descending, then ordinal title; subtract scheduled minutes from configured daily capacity; distribute each task only over dates through its deadline; report leftover minutes as a capacity risk. No grades or speculative performance predictions are used. Daily capacity is stored per account and defaults to 120 minutes.
