# Backend Integration Contract

## Shared contracts

Definitions and validation attributes are in `StudentPlanner/Models/PlannerModels.cs`.

- `Student`: integer `Id`, required `FullName`, validated `Email`.
- `Course`: integer `Id`, `Name`, `Code`, `Instructor`, `Credits`, `Color`, `IsActive`, and presentation-compatible `Room`, `Schedule`, `Description`.
- `Assignment`: integer `Id`, required `CourseId`, `Title`, `Description`, `DueDate`, `AssignmentPriority`, `AssignmentStatus`, `EstimatedMinutes`, nullable `CompletedAt`.
- `Grade`: integer `Id`, required `CourseId`, `Title`, `Score`, `MaxScore`, `Weight`, and `Date`.
- `ScheduleEvent`: integer `Id`, nullable `CourseId`, `Title`, `Description`, `StartTime`, `EndTime`, `Location`, and `ScheduleEventType`.
- `StudentSettings`: profile, `ThemePreference`, English-only `AppLanguage`, time zone, semester, academic year, `GpaScale`, and notification preferences.

Dates and times currently use `DateTime` local wall-clock values. Agree on UTC storage and user time-zone conversion before introducing a persistent backend. IDs are positive integers; UI mock IDs are generated per scoped service and are not durable across app restarts.

## Interfaces to implement

Implement the signatures in `StudentPlanner/Services/Interfaces/IPlannerServices.cs`:

- `ICourseService`: `GetCoursesAsync`, `CreateCourseAsync`, `UpdateCourseAsync`, `DeleteCourseAsync`.
- `IAssignmentService`: `GetAssignmentsAsync`, `CreateAssignmentAsync`, `UpdateAssignmentAsync`, `DeleteAssignmentAsync`.
- `IGradeService`: `GetGradesAsync`, `CreateGradeAsync`, `UpdateGradeAsync`, `DeleteGradeAsync`.
- `IScheduleService`: `GetEventsAsync`, `CreateEventAsync`, `UpdateEventAsync`, `DeleteEventAsync`.
- `ISettingsService`: `GetSettingsAsync`, `SaveSettingsAsync`.
- `IDashboardService`: `GetSnapshotAsync`.

Every asynchronous operation accepts an optional `CancellationToken`. List results are read-only snapshots. Missing update/delete targets return `null`/`false`. Invalid data should be reported through a documented validation exception or validation result; persistence failures should propagate as service exceptions and be handled by the UI.

`IChangeNotifier.Changed` is the current mock synchronization mechanism. EF-backed services must publish a notification after successful writes, or Student 1 should replace it with an equivalent circuit-scoped state publisher.

## Relationships and deletion

Assignments and grades reference `CourseId`; schedule events may have no course. The UI currently prevents deleting courses referenced by an assignment or grade. Keep that restrict-delete policy in the database/service layer. Do not cascade-delete academic records silently.

## GPA demonstration rule

`GpaCalculator` first computes a weighted percentage within each course (`sum(percentage × Weight) / sum(Weight)`), converts that percentage linearly to the selected scale, then weights course GPA by course credits. The percentage conversion is a transparent demonstration rule, not an institutional grading policy. Confirm the university's grading bands before replacing it with a production rule. `MaxScore` normalizes each grade to a percentage; zero or invalid maximum scores are rejected by validation.

## Service replacement

The mock implementations are registered in `Program.cs`. Student 1 may add EF Core implementations and swap DI registrations while preserving interfaces and model semantics. Do not make Razor pages depend on `DbContext` or entities directly. Backend validation remains authoritative even when frontend forms validate the same fields.
