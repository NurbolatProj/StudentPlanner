using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfGradeService(IDbContextFactory<AppDbContext> factory, ILogger<EfGradeService> logger, ICurrentUserService currentUser)
    : EfServiceBase<EfGradeService>(factory, logger, currentUser), IGradeService
{
    public async Task<IReadOnlyList<Grade>> GetGradesAsync(CancellationToken cancellationToken = default)
    {
        var ownerId = await GetUserIdAsync(cancellationToken);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Grades.AsNoTracking().Where(grade => grade.OwnerId == ownerId).Include(grade => grade.Course)
            .OrderByDescending(grade => grade.Date).ThenBy(grade => grade.Id).ToListAsync(cancellationToken);
        return items.Select(ToFrontendModel).ToArray();
    }

    public async Task<Grade> CreateGradeAsync(Grade grade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grade);
        var entity = Normalize(grade);
        entity.OwnerId = await GetUserIdAsync(cancellationToken);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == entity.CourseId && item.OwnerId == entity.OwnerId, cancellationToken)
            ?? throw new ValidationException("The selected course no longer exists.");
        entity.Credits = course.Credits;
        db.Grades.Add(entity);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (IsForeignKeyViolation(exception))
        {
            throw new ValidationException("The selected course no longer exists.", exception);
        }
        entity.Course = course;
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<Grade?> UpdateGradeAsync(Grade grade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grade);
        var input = Normalize(grade);
        input.OwnerId = await GetUserIdAsync(cancellationToken);
        Validate(input);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Grades.SingleOrDefaultAsync(item => item.Id == input.Id && item.OwnerId == input.OwnerId, cancellationToken);
        if (entity is null) return null;
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == input.CourseId && item.OwnerId == input.OwnerId, cancellationToken)
            ?? throw new ValidationException("The selected course no longer exists.");
        entity.CourseId = input.CourseId;
        entity.Title = input.Title;
        entity.Score = input.Score;
        entity.MaxScore = input.MaxScore;
        entity.Weight = input.Weight;
        entity.Date = input.Date;
        entity.Credits = course.Credits;
        await db.SaveChangesAsync(cancellationToken);
        entity.Course = course;
        NotifyChanged();
        return ToFrontendModel(entity);
    }

    public async Task<bool> DeleteGradeAsync(int id, CancellationToken cancellationToken = default)
    {
        var ownerId = await GetUserIdAsync(cancellationToken);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Grades.SingleOrDefaultAsync(item => item.Id == id && item.OwnerId == ownerId, cancellationToken);
        if (entity is null) return false;
        db.Grades.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        NotifyChanged();
        return true;
    }

    private static Grade Normalize(Grade grade) => new()
    {
        Id = grade.Id, OwnerId = grade.OwnerId, CourseId = grade.CourseId, Title = grade.Title?.Trim() ?? string.Empty,
        Score = grade.Score, MaxScore = grade.MaxScore, Weight = grade.Weight, Date = grade.Date
    };

    private static Grade ToFrontendModel(Grade grade) => new()
    {
        Id = grade.Id, OwnerId = grade.OwnerId, CourseId = grade.CourseId, CourseName = grade.Course?.Code ?? grade.CourseName,
        Title = grade.Title, Score = grade.Score, MaxScore = grade.MaxScore, Weight = grade.Weight,
        Date = grade.Date, Credits = grade.Course?.Credits ?? grade.Credits
    };
}
