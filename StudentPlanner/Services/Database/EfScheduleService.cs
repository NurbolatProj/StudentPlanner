using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfScheduleService(IDbContextFactory<AppDbContext> factory, ILogger<EfScheduleService> logger)
    : EfServiceBase<EfScheduleService>(factory, logger), IScheduleService, IScheduleQueryService
{
    public async Task<IReadOnlyList<ScheduleEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.ScheduleEvents.AsNoTracking().Include(item => item.Course)
            .OrderBy(item => item.StartTime).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        return items.Select(ToFrontendModel).ToArray();
    }

    public async Task<IReadOnlyList<ScheduleEvent>> FindEventsAsync(DateTime fromInclusive, DateTime throughExclusive, int? courseId = null, CancellationToken cancellationToken = default)
    {
        if (throughExclusive <= fromInclusive) throw new ArgumentException("The end of the range must be later than its start.", nameof(throughExclusive));
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var events = db.ScheduleEvents.AsNoTracking().Include(item => item.Course)
            .Where(item => item.StartTime < throughExclusive && item.EndTime > fromInclusive);
        if (courseId is int id) events = events.Where(item => item.CourseId == id);
        var items = await events.OrderBy(item => item.StartTime).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        return items.Select(ToFrontendModel).ToArray();
    }

    public async Task<ScheduleEvent> CreateEventAsync(ScheduleEvent item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var entity = Normalize(item);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCourseExistsAsync(db, entity.CourseId, cancellationToken);
        await EnsureNoConflictAsync(db, entity, null, cancellationToken);
        db.ScheduleEvents.Add(entity);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The selected course no longer exists.", exception);
        }
        if (entity.CourseId is int courseId)
            entity.Course = await db.Courses.AsNoTracking().SingleAsync(course => course.Id == courseId, cancellationToken);
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<ScheduleEvent?> UpdateEventAsync(ScheduleEvent item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var input = Normalize(item);
        Validate(input);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ScheduleEvents.SingleOrDefaultAsync(value => value.Id == input.Id, cancellationToken);
        if (entity is null) return null;
        await EnsureCourseExistsAsync(db, input.CourseId, cancellationToken);
        await EnsureNoConflictAsync(db, input, input.Id, cancellationToken);
        entity.CourseId = input.CourseId;
        entity.Title = input.Title;
        entity.Description = input.Description;
        entity.StartTime = input.StartTime;
        entity.EndTime = input.EndTime;
        entity.Location = input.Location;
        entity.Type = input.Type;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The selected course no longer exists.", exception);
        }
        if (entity.CourseId is int courseId)
            entity.Course = await db.Courses.AsNoTracking().SingleAsync(course => course.Id == courseId, cancellationToken);
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<bool> DeleteEventAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ScheduleEvents.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return false;
        db.ScheduleEvents.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        NotifyChanged();
        return true;
    }

    private static async Task EnsureNoConflictAsync(AppDbContext db, ScheduleEvent item, int? excludedId, CancellationToken cancellationToken)
    {
        var overlaps = await db.ScheduleEvents.AsNoTracking().AnyAsync(existing =>
            (excludedId == null || existing.Id != excludedId.Value)
            && existing.StartTime < item.EndTime
            && existing.EndTime > item.StartTime,
            cancellationToken);
        if (overlaps)
            throw new ValidationException("This event overlaps another event in the calendar.");
    }

    private static ScheduleEvent Normalize(ScheduleEvent item) => new()
    {
        Id = item.Id, CourseId = item.CourseId, Title = item.Title?.Trim() ?? string.Empty,
        Description = item.Description?.Trim() ?? string.Empty, StartTime = item.StartTime,
        EndTime = item.EndTime, Location = item.Location?.Trim() ?? string.Empty, Type = item.Type
    };

    private static ScheduleEvent ToFrontendModel(ScheduleEvent item) => new()
    {
        Id = item.Id, CourseId = item.CourseId, CourseName = item.Course?.Code ?? item.CourseName,
        Title = item.Title, Description = item.Description, StartTime = item.StartTime,
        EndTime = item.EndTime, Location = item.Location, Type = item.Type
    };
}
