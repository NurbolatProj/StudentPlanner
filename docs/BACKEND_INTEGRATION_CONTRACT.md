# Backend Integration Contract

## Shared contracts

Definitions and validation attributes are in `StudentPlanner/Models/PlannerModels.cs`.

- `Student`: integer `Id`, required `FullName`, validated `Email`.
- `Course`: integer `Id`, `Name`, `Code`, `Instructor`, `Credits`, `Color`, `IsActive`, and presentation-compatible `Room`, `Schedule`, `Description`.
- `Assignment`: integer `Id`, required `CourseId`, `Title`, `Description`, `DueDate`, `AssignmentPriority`, `AssignmentStatus`, `EstimatedMinutes`, nullable `CompletedAt`.
- `Grade`: integer `Id`, required `CourseId`, `Title`, `Score`, `MaxScore`, `Weight`, and `Date`.
- `ScheduleEvent`: integer `Id`, nullable `CourseId`, `Title`, `Description`, `StartTime`, `EndTime`, `Location`, and `ScheduleEventType`.
- `StudentSettings`: profile, `ThemePreference`, English-only `AppLanguage`, time zone, semester, academic year, `GpaScale`, and notification preferences.

Dates and times continue to use the shared `DateTime` local wall-clock contract and are stored as SQLite date/time values. Agree on UTC storage and user time-zone conversion before changing this contract. IDs are database-generated positive integers and are durable across restarts.

## Interfaces to implement

Implement the signatures in `StudentPlanner/Services/Interfaces/IPlannerServices.cs`:

- `ICourseService`: `GetCoursesAsync`, `CreateCourseAsync`, `UpdateCourseAsync`, `DeleteCourseAsync`.
- `IAssignmentService`: `GetAssignmentsAsync`, `CreateAssignmentAsync`, `UpdateAssignmentAsync`, `DeleteAssignmentAsync`.
- `IGradeService`: `GetGradesAsync`, `CreateGradeAsync`, `UpdateGradeAsync`, `DeleteGradeAsync`.
- `IScheduleService`: `GetEventsAsync`, `CreateEventAsync`, `UpdateEventAsync`, `DeleteEventAsync`.
- `ISettingsService`: `GetSettingsAsync`, `SaveSettingsAsync`.
- `IDashboardService`: `GetSnapshotAsync`.

EF-backed implementation classes live in `StudentPlanner/Services/Database`. Additional `IAssignmentQueryService` and `IScheduleQueryService` contracts provide database-side filtering/range queries without changing the existing component-facing interfaces. `IAssignmentPriorityService` returns deterministic Dashboard recommendations.

Every asynchronous operation accepts an optional `CancellationToken`. List results are read-only snapshots. Missing update/delete targets return `null`/`false`. Invalid data should be reported through a documented validation exception or validation result; persistence failures should propagate as service exceptions and be handled by the UI.

`IChangeNotifier.Changed` remains the synchronization mechanism. Scoped EF-backed services publish a notification after successful writes. Services use `IDbContextFactory<AppDbContext>` and create a short-lived context per operation.

## Relationships and deletion

Assignments and grades reference `CourseId`; schedule events may have no course. Assignment and grade relationships use `RESTRICT`; the service rejects deleting courses referenced by those records. Schedule event relationships use `SET NULL`, preserving an event if its course is removed. No academic records are cascade-deleted.

## GPA demonstration rule

`GpaCalculator` first computes a weighted percentage within each course (`sum(percentage × Weight) / sum(Weight)`), converts that percentage linearly to the selected scale, then weights course GPA by course credits from the Course relationship. The percentage conversion is a transparent demonstration rule, not an institutional grading policy. Confirm the university's grading bands before replacing it with a production rule. `MaxScore` normalizes each grade to a percentage; zero or invalid maximum scores are rejected by validation. Course display labels and derived grade credits are frontend conveniences and are not persisted.

## Service replacement

`Program.cs` registers EF Core SQLite implementations and applies migrations at startup by default. Configure `Database:Path` to override the default `%LOCALAPPDATA%/StudentPlanner/studentplanner.db` path. Development sample data is inserted only into an entirely empty database. Browser preferences remain in local storage; no second settings store is introduced. Do not make Razor pages depend on `DbContext`. Backend validation remains authoritative even when frontend forms validate the same fields.
