using Microsoft.EntityFrameworkCore;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace StudentPlanner.Services.Focus;

public sealed class FocusSessionService(IDbContextFactory<AppDbContext> factory, ICurrentUserService currentUser) : IFocusSessionService, IDisposable
{
    private readonly object gate = new();
    private SessionClock? active;

    public async Task<int> StartAsync(int? assignmentId, FocusSessionType type, int targetMinutes, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type) || targetMinutes is < 1 or > 240) throw new ValidationException("Choose a valid session type and duration (1–240 minutes).");
        var owner = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (assignmentId is int assignmentKey && !await db.Assignments.AsNoTracking().AnyAsync(a => a.Id == assignmentKey && a.OwnerId == owner, cancellationToken))
            throw new ValidationException("That assignment is unavailable.");
        lock (gate) { if (active is not null) throw new InvalidOperationException("A focus session is already active."); }
        var entity = new FocusSession { OwnerId = owner, AssignmentId = assignmentId, Type = type, StartTime = DateTimeOffset.UtcNow };
        db.FocusSessions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        lock (gate)
        {
            if (active is not null) throw new InvalidOperationException("A focus session is already active.");
            active = new SessionClock(entity.Id, owner, assignmentId, entity.StartTime, type, targetMinutes * 60);
        }
        return entity.Id;
    }

    public int GetElapsedSeconds() { lock (gate) return active?.ElapsedSeconds ?? 0; }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        lock (gate) { if (active is null) throw new InvalidOperationException("No active session."); active.Pause(); }
        return Task.CompletedTask;
    }
    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        lock (gate) { if (active is null) throw new InvalidOperationException("No active session."); active.Resume(); }
        return Task.CompletedTask;
    }
    public async Task<FocusSessionSummary?> EndAsync(FocusSessionStatus status, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status)) throw new ValidationException("Invalid completion status.");
        SessionClock? clock;
        lock (gate) { clock = active; active = null; }
        if (clock is null) return null;
        var seconds = clock.ElapsedSeconds;
        var ended = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await db.FocusSessions.SingleOrDefaultAsync(s => s.Id == clock.Id && s.OwnerId == clock.OwnerId, cancellationToken);
        if (entity is null) return null;
        entity.EndTime = ended;
        entity.ActualDurationSeconds = seconds;
        entity.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        var assignment = clock.AssignmentId is int assignmentId ? await db.Assignments.AsNoTracking().Where(a => a.Id == assignmentId && a.OwnerId == clock.OwnerId)
            .Select(a => new { a.Title, Code = a.Course!.Code }).SingleOrDefaultAsync(cancellationToken) : null;
        return new(entity.Id, entity.AssignmentId, assignment?.Title ?? "Independent study", assignment?.Code ?? "—", entity.StartTime, entity.EndTime, seconds, entity.Type, entity.Status);
    }

    public async Task<FocusStatistics> GetStatisticsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var owner = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var today = new DateTimeOffset(now.Date, now.Offset);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var loaded = await db.FocusSessions.AsNoTracking().Where(s => s.OwnerId == owner && s.EndTime != null && s.Type == FocusSessionType.Focus)
            .Include(s => s.Assignment).ThenInclude(a => a!.Course).ToListAsync(cancellationToken);
        var records = loaded.OrderByDescending(s => s.StartTime).ToArray();
        var weekItems = records.Where(s => s.StartTime >= weekStart).ToArray();
        var todayItems = weekItems.Where(s => s.StartTime >= today).ToArray();
        var recent = records.Take(8).Select(ToSummary).ToArray();
        var completed = records.Where(s => s.Status == FocusSessionStatus.Completed).ToArray();
        return new(todayItems.Sum(s => s.ActualDurationSeconds), weekItems.Sum(s => s.ActualDurationSeconds), completed.Length,
            completed.Length == 0 ? 0 : (int)Math.Round(completed.Average(s => s.ActualDurationSeconds)), recent);
    }

    private static FocusSessionSummary ToSummary(FocusSession s) => new(s.Id, s.AssignmentId, s.Assignment?.Title ?? "Independent study", s.Assignment?.Course?.Code ?? "—", s.StartTime, s.EndTime, s.ActualDurationSeconds, s.Type, s.Status);
    public void Dispose() { lock (gate) { active?.Pause(); active = null; } }

    private sealed class SessionClock(int id, string ownerId, int? assignmentId, DateTimeOffset start, FocusSessionType type, int targetSeconds)
    {
        private long segmentStart = Stopwatch.GetTimestamp();
        private long accruedTicks;
        private bool paused;
        public int Id { get; } = id;
        public string OwnerId { get; } = ownerId;
        public int? AssignmentId { get; } = assignmentId;
        public DateTimeOffset Start { get; } = start;
        public FocusSessionType Type { get; } = type;
        public int TargetSeconds { get; } = targetSeconds;
        public int ElapsedSeconds { get { var ticks=accruedTicks+(paused?0:Stopwatch.GetTimestamp()-segmentStart); return Math.Clamp((int)(ticks/(double)Stopwatch.Frequency),0,86400); } }
        public void Pause() { if (paused) return; accruedTicks += Stopwatch.GetTimestamp()-segmentStart; paused=true; }
        public void Resume() { if (!paused) return; segmentStart=Stopwatch.GetTimestamp(); paused=false; }
    }
}
