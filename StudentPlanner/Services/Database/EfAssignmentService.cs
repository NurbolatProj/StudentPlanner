using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfAssignmentService(IDbContextFactory<AppDbContext> factory, ILogger<EfAssignmentService> logger)
    : EfServiceBase<EfAssignmentService>(factory, logger), IAssignmentService, IAssignmentQueryService
{
    public async Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Assignments.AsNoTracking().Include(item => item.Course).OrderBy(item => item.DueDate).ToListAsync(cancellationToken);
        return items.Select(ToFrontendModel).ToArray();
    }

    public async Task<IReadOnlyList<Assignment>> FindAssignmentsAsync(AssignmentQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var filtered = db.Assignments.AsNoTracking().Include(item => item.Course).AsQueryable();
        if (query.CourseId is int courseId) filtered = filtered.Where(item => item.CourseId == courseId);
        if (query.Status is AssignmentStatus status) filtered = filtered.Where(item => item.Status == status);
        if (query.Priority is AssignmentPriority priority) filtered = filtered.Where(item => item.Priority == priority);
        if (query.DueFrom is DateTime from) filtered = filtered.Where(item => item.DueDate >= from);
        if (query.DueThrough is DateTime through) filtered = filtered.Where(item => item.DueDate <= through);
        if (!query.IncludeCompleted) filtered = filtered.Where(item => item.Status != AssignmentStatus.Completed);
        filtered = filtered.OrderBy(item => item.DueDate).ThenByDescending(item => item.Priority).ThenBy(item => item.Id);
        if (query.Take is > 0) filtered = filtered.Take(query.Take.Value);
        return (await filtered.ToListAsync(cancellationToken)).Select(ToFrontendModel).ToArray();
    }

    public async Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var entity = Normalize(assignment);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureCourseExistsAsync(db, entity.CourseId, cancellationToken);
        entity.CompletedAt = entity.Status == AssignmentStatus.Completed ? entity.CompletedAt ?? DateTime.Now : null;
        db.Assignments.Add(entity);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The selected course no longer exists.", exception);
        }
        entity.Course = await db.Courses.AsNoTracking().SingleAsync(course => course.Id == entity.CourseId, cancellationToken);
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<Assignment?> UpdateAssignmentAsync(Assignment assignment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var input = Normalize(assignment);
        Validate(input);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Assignments.SingleOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (entity is null) return null;
        await EnsureCourseExistsAsync(db, input.CourseId, cancellationToken);
        entity.CourseId = input.CourseId;
        entity.Title = input.Title;
        entity.Description = input.Description;
        entity.DueDate = input.DueDate;
        entity.Priority = input.Priority;
        entity.Status = input.Status;
        entity.EstimatedMinutes = input.EstimatedMinutes;
        entity.CompletedAt = input.Status == AssignmentStatus.Completed ? entity.CompletedAt ?? input.CompletedAt ?? DateTime.Now : null;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The selected course no longer exists.", exception);
        }
        entity.Course = await db.Courses.AsNoTracking().SingleAsync(course => course.Id == entity.CourseId, cancellationToken);
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<bool> DeleteAssignmentAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Assignments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return false;
        db.Assignments.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        NotifyChanged();
        return true;
    }

    private static Assignment Normalize(Assignment assignment) => new()
    {
        Id = assignment.Id, CourseId = assignment.CourseId, Title = assignment.Title?.Trim() ?? string.Empty,
        Description = assignment.Description?.Trim() ?? string.Empty, DueDate = assignment.DueDate,
        Priority = assignment.Priority, Status = assignment.Status, EstimatedMinutes = assignment.EstimatedMinutes,
        CompletedAt = assignment.CompletedAt
    };

    private static Assignment ToFrontendModel(Assignment assignment) => new()
    {
        Id = assignment.Id, CourseId = assignment.CourseId, CourseName = assignment.Course?.Code ?? assignment.CourseName,
        Title = assignment.Title, Description = assignment.Description, DueDate = assignment.DueDate,
        Priority = assignment.Priority, Status = assignment.Status, EstimatedMinutes = assignment.EstimatedMinutes,
        CompletedAt = assignment.CompletedAt
    };
}
