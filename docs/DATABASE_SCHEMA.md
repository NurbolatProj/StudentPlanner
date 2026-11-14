# Database Schema

The migrations are `InitialCreate` and `AddIdentityAndUserOwnership`. Identity tables and account-specific records use the same SQLite database and `AppDbContext`.

```mermaid
erDiagram
    COURSE ||--o{ ASSIGNMENT : has
    COURSE ||--o{ GRADE : has
    COURSE o|--o{ SCHEDULE_EVENT : optionally_references
    IDENTITY_USER ||--o{ COURSE : owns
    IDENTITY_USER ||--o{ ASSIGNMENT : owns
    IDENTITY_USER ||--o{ GRADE : owns
    IDENTITY_USER ||--o{ SCHEDULE_EVENT : owns
    IDENTITY_USER ||--o| USER_PLANNER_SETTINGS : preferences
    STUDENT {
        int Id PK
        string FullName
        string Email UK
        datetime CreatedAt
    }
    COURSE {
        int Id PK
        string OwnerId FK "nullable for legacy rows"
        string Name
        string Code "unique per owner"
        string Instructor
        int Credits
        string Color
        bool IsActive
        string Room
        string Schedule
        string Description
    }
    ASSIGNMENT {
        int Id PK
        string OwnerId FK "nullable for legacy rows"
        int CourseId FK
        string Title
        string Description
        datetime DueDate
        int Priority
        int Status
        int EstimatedMinutes
        datetime CompletedAt
    }
    GRADE {
        int Id PK
        string OwnerId FK "nullable for legacy rows"
        int CourseId FK
        string Title
        decimal Score
        decimal MaxScore
        decimal Weight
        datetime Date
    }
    SCHEDULE_EVENT {
        int Id PK
        string OwnerId FK "nullable for legacy rows"
        int CourseId FK "nullable"
        string Title
        string Description
        datetime StartTime
        datetime EndTime
        string Location
        int Type
    }
    IDENTITY_USER {
        string Id PK
        string FullName
        string Email UK
        string PasswordHash
    }
    USER_PLANNER_SETTINGS {
        string UserId PK_FK
        string Theme
        int GpaScale
        bool AssignmentReminders
        bool ExamReminders
        bool DeadlineAlerts
        bool WeeklySummary
    }
```

## Constraints and indexes

- Course code is trimmed, uppercased by the service, and unique per non-null owner with SQLite `NOCASE` collation.
- Assignment and Grade course foreign keys use `RESTRICT`; academic history must be removed or reassigned before course deletion.
- Schedule event course foreign key uses `SET NULL`, preserving event details if its course is deleted.
- Owner IDs are nullable only to preserve data created before accounts existed. Authenticated service queries never return null-owner records. New writes always set an owner from the authenticated principal.
- User planner settings have a one-to-one Identity user foreign key. Profile name and email are authoritative in `AspNetUsers`.
- Schedule start time and course ID, course activity, assignment status/deadline, assignment course/priority, grade course/date, and owner columns have indexes.
- Grade decimal properties declare precision for providers that enforce it. SQLite stores decimal values as text and application calculations use the shared decimal `GpaCalculator`.
- Enum values are stored as integers.
- Date/time values preserve the existing local wall-clock `DateTime` contract. A production multi-time-zone deployment should agree on UTC conversion before changing this contract.

`UserPlannerSettings` stores academic preferences, theme, GPA scale, and notification choices per Identity user. The legacy browser localStorage key is not read anymore. Legacy academic rows are not claimed by the first registering user; a future import must be an explicit, audited administrative action after verifying ownership.

## Database file

By default the database is `%LOCALAPPDATA%/StudentPlanner/studentplanner.db`. The configured path can be absolute or relative to `%LOCALAPPDATA%/StudentPlanner`. Directory creation is handled by `DatabasePath`. No startup operation drops a database or deletes records.

## Focus Lab and workload insights
`FocusSessions` stores the authenticated `OwnerId`, optional `AssignmentId`, start/end UTC instants, server-measured active seconds, session type, and completion state. Indexes cover `(OwnerId, StartTime)` and `AssignmentId`. Owner deletion is restricted; deleting a linked assignment sets its session reference to null.

`UserPlannerSettings.DailyStudyCapacityMinutes` adds a 30–720 minute per-user planning capacity (default 120). The new migration preserves existing data and supplies 120 minutes to existing settings rows.
