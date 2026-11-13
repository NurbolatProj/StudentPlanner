# Student Planner

Student Planner is a .NET 10 Blazor Interactive Server application for course planning, assignments, grades, calendar events, and student preferences. Academic records use EF Core 10 and SQLite. Browser preferences remain in local storage.

## Requirements

- .NET 10 SDK
- NuGet access for package restore on a fresh machine

## Run

```powershell
dotnet restore StudentPlanner.slnx
dotnet build StudentPlanner.slnx
dotnet run --project StudentPlanner/StudentPlanner.csproj
```

On startup the application applies checked-in EF migrations. In Development, sample records are added only if the database has no student or academic records. Existing database rows are never cleared or reseeded.

The default database is `%LOCALAPPDATA%/StudentPlanner/studentplanner.db`. Configure `Database:Path` in `StudentPlanner/appsettings.json` or set `Database__Path` to an absolute path. Relative paths are rooted under the same application data directory. `Database:ApplyMigrationsOnStartup` defaults to `true`; set it to `false` only when migrations are applied externally.

## Migrations

The repository includes a local `dotnet-ef` tool manifest pinned to EF Core 10. Run from the repository root:

```powershell
dotnet tool restore
dotnet dotnet-ef migrations list --project StudentPlanner/StudentPlanner.csproj
dotnet dotnet-ef database update --project StudentPlanner/StudentPlanner.csproj
```

Create a later migration after changing the EF model:

```powershell
dotnet dotnet-ef migrations add DescribeTheChange --project StudentPlanner/StudentPlanner.csproj --output-dir Data/Migrations
```

Do not use `EnsureCreated` or `EnsureDeleted`; migrations preserve and evolve existing data.

## Tests

```powershell
dotnet test StudentPlanner.slnx
```

Backend integration tests use SQLite in-memory with an open connection and apply the real migration. They exercise foreign keys, uniqueness, CRUD, delete restrictions, analytics, schedule conflicts, persistence across contexts, and priority recommendations.

## Architecture and integration

Read [Backend Architecture](docs/BACKEND_ARCHITECTURE.md), [Database Schema](docs/DATABASE_SCHEMA.md), [Business Logic](docs/BUSINESS_LOGIC.md), [Priority Algorithm](docs/PRIORITY_ALGORITHM.md), [Backend Testing](docs/BACKEND_TESTING.md), and [Student 1 Report](docs/STUDENT1_REPORT.md). Existing frontend behavior and service interfaces are described in [Backend Integration Contract](docs/BACKEND_INTEGRATION_CONTRACT.md).
