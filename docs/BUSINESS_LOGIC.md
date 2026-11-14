# Business Logic

## Validation

Services run the shared model's DataAnnotations and `IValidatableObject` rules before writing. They also validate foreign keys before writes so forms receive a useful `ValidationException`. SQLite foreign keys remain enabled as the authoritative integrity backstop. Duplicate course codes are normalized and rejected before save; database uniqueness errors are translated to a domain validation message.

Assignments require an existing `CourseId`; a populated navigation object is not required. Grades require an existing course and take credits from that course. Personal calendar events may omit `CourseId`; linked events must reference an existing course. Calendar times must have a positive duration and may not overlap another stored event.

## Course deletion

`EfCourseService` rejects deletion while assignments or grades reference the course and runs the check and delete in a transaction. Foreign-key restrictions protect against races. Related schedule events survive with a null `CourseId` as specified in the schema.

## GPA

The existing `GpaCalculator` remains the single calculation policy:

1. Normalize each score to a percentage using `Score / MaxScore × 100`.
2. Within a course, calculate the assessment-weighted mean.
3. Convert that percentage linearly to the selected 4, 5, or 100 point scale.
4. Weight course results by course credits.

Zero/invalid maximum scores and scores outside the supported range are rejected by validation. Courses without grades do not add GPA weight. A zero total assessment weight is excluded, and empty grade input returns zero. These are demonstration rules, not an institutional grading standard.

## Dashboard

The EF dashboard obtains assignment totals, completed and overdue counts, upcoming unfinished deadlines, events starting today, course rows, grade rows, and current browser GPA settings. GPA uses `GpaCalculator`, and completion percentage is zero for an empty assignment set. The existing Dashboard page derives weekly completed-work minutes from persisted `CompletedAt` and `EstimatedMinutes` values; no fabricated activity rows are stored.

## Functional C# use

- `GpaCalculator` is a deterministic calculation over enumerable inputs and uses LINQ grouping and aggregation.
- EF query services compose filters as expression/LINQ pipelines before executing them asynchronously.
- `AssignmentPriorityService` separates pure score/reason functions from ordering and recommendation projection.
- `PrioritizeUsing` accepts a `Func<Assignment, DateOnly, double>` policy delegate, allowing alternate scoring policies to reuse the same completed filtering and deterministic ordering pipeline.
- Dashboard projections produce frontend snapshots without holding tracked entity graphs.
- Recommendation DTOs are immutable records.

These are used for real filtering, scoring, aggregation, and projection rather than added as unrelated demonstrations.
