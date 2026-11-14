using StudentPlanner.Models;

namespace StudentPlanner.Services.Interfaces;

public interface IFocusSessionService
{
    Task<int> StartAsync(int? assignmentId, FocusSessionType type, int targetMinutes, CancellationToken cancellationToken = default);
    int GetElapsedSeconds();
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task<FocusSessionSummary?> EndAsync(FocusSessionStatus status, CancellationToken cancellationToken = default);
    Task<FocusStatistics> GetStatisticsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
