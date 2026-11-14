using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfCourseService(IDbContextFactory<AppDbContext> factory, ILogger<EfCourseService> logger, ICurrentUserService currentUser)
    : EfServiceBase<EfCourseService>(factory, logger, currentUser), ICourseService
{
    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var ownerId = await GetUserIdAsync(cancellationToken);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Courses.AsNoTracking().Where(course => course.OwnerId == ownerId).OrderBy(course => course.Name).ToListAsync(cancellationToken);
    }

    public async Task<Course> CreateCourseAsync(Course course, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(course);
        var entity = Normalize(course);
        entity.OwnerId = await GetUserIdAsync(cancellationToken);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Courses.AnyAsync(item => item.OwnerId == entity.OwnerId && item.Code == entity.Code, cancellationToken))
            throw new ValidationException("A course with this code already exists.");

        db.Courses.Add(entity);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ValidationException("A course with this code already exists.", exception);
        }
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<Course?> UpdateCourseAsync(Course course, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(course);
        var entity = Normalize(course);
        entity.OwnerId = await GetUserIdAsync(cancellationToken);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Courses.SingleOrDefaultAsync(item => item.Id == entity.Id && item.OwnerId == entity.OwnerId, cancellationToken);
        if (existing is null) return null;
        if (await db.Courses.AnyAsync(item => item.OwnerId == entity.OwnerId && item.Id != entity.Id && item.Code == entity.Code, cancellationToken))
            throw new ValidationException("A course with this code already exists.");

        db.Entry(existing).CurrentValues.SetValues(entity);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new ValidationException("A course with this code already exists.", exception);
        }
        NotifyChanged();
        return ToFrontendModel(existing);
    }

    public async Task<bool> DeleteCourseAsync(int id, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetUserIdAsync(cancellationToken);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.Courses.SingleOrDefaultAsync(course => course.Id == id && course.OwnerId == ownerId, cancellationToken);
        if (existing is null) return false;

        if (await db.Assignments.AnyAsync(assignment => assignment.CourseId == id && assignment.OwnerId == ownerId, cancellationToken)
            || await db.Grades.AnyAsync(grade => grade.CourseId == id && grade.OwnerId == ownerId, cancellationToken))
            throw new ValidationException("A course cannot be deleted while it has assignments or grades. Remove or reassign them first.");

        var linkedEvents = await db.ScheduleEvents.Where(item => item.CourseId == id && item.OwnerId == ownerId).ToListAsync(cancellationToken);
        foreach (var item in linkedEvents) item.CourseId = null;

        db.Courses.Remove(existing);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The course still has related academic records and cannot be deleted.", exception);
        }
        await transaction.CommitAsync(cancellationToken);
        NotifyChanged();
        return true;
    }

    private static Course Normalize(Course course) => new()
    {
        Id = course.Id,
        OwnerId = course.OwnerId,
        Name = course.Name?.Trim() ?? string.Empty,
        Code = course.Code?.Trim().ToUpperInvariant() ?? string.Empty,
        Instructor = course.Instructor?.Trim() ?? string.Empty,
        Credits = course.Credits,
        Color = course.Color,
        IsActive = course.IsActive,
        Description = course.Description?.Trim() ?? string.Empty,
        Room = course.Room?.Trim() ?? string.Empty,
        Schedule = course.Schedule?.Trim() ?? string.Empty
    };

    private static Course ToFrontendModel(Course course) => new()
    {
        Id = course.Id, OwnerId = course.OwnerId, Name = course.Name, Code = course.Code, Instructor = course.Instructor,
        Credits = course.Credits, Color = course.Color, IsActive = course.IsActive,
        Description = course.Description, Room = course.Room, Schedule = course.Schedule
    };
}
