# Student Planner

Student Planner is a Blazor Interactive Server frontend prototype for courses, assignments, grades, a calendar and student preferences. Data records are provided by scoped in-memory mock services. Browser preferences use local storage. SQLite and EF Core are planned backend work and are not included.

## Requirements

- .NET 10 SDK

## Run

```bash
dotnet restore
dotnet build
dotnet run --project StudentPlanner/StudentPlanner.csproj
```

## Test

```bash
dotnet test
```

See `docs/FRONTEND_ARCHITECTURE.md`, `docs/BACKEND_INTEGRATION_CONTRACT.md` and `docs/FRONTEND_TESTING.md` for implementation and integration details.
