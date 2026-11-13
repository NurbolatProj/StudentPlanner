using StudentPlanner.Models;

namespace StudentPlanner.Services.Interfaces;

public sealed record AssignmentQuery(
    int? CourseId = null,
    AssignmentStatus? Status = null,
    AssignmentPriority? Priority = null,
    DateTime? DueFrom = null,
    DateTime? DueThrough = null,
    bool IncludeCompleted = true,
    int? Take = null);

public interface IAssignmentQueryService
{
    Task<IReadOnlyList<Assignment>> FindAssignmentsAsync(AssignmentQuery query, CancellationToken cancellationToken = default);
}

public interface IScheduleQueryService
{
    Task<IReadOnlyList<ScheduleEvent>> FindEventsAsync(DateTime fromInclusive, DateTime throughExclusive, int? courseId = null, CancellationToken cancellationToken = default);
}

public sealed record PrioritizedAssignmentDto(int AssignmentId, string Title, double PriorityScore, string Reason, DateTime DueDate);

public interface IAssignmentPriorityService
{
    IReadOnlyList<PrioritizedAssignmentDto> Prioritize(IEnumerable<Assignment> assignments, DateOnly today, int take = 5);
}
