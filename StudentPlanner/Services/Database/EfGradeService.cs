using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfGradeService(IDbContextFactory<AppDbContext> factory, ILogger<EfGradeService> logger)
    : EfServiceBase<EfGradeService>(factory, logger), IGradeService
{
    public async Task<IReadOnlyList<Grade>> GetGradesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.Grades.AsNoTracking().Include(grade => grade.Course)
            .OrderByDescending(grade => grade.Date).ThenBy(grade => grade.Id).ToListAsync(cancellationToken);
        return items.Select(ToFrontendModel).ToArray();
    }

    public async Task<Grade> CreateGradeAsync(Grade grade, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grade);
        var entity = Normalize(grade);
        Validate(entity);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == entity.CourseId, cancellationToken)
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
        Validate(input);
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Grades.SingleOrDefaultAsync(item => item.Id == input.Id, cancellationToken);
        if (entity is null) return null;
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == input.CourseId, cancellationToken)
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
        await using var db = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Grades.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return false;
        db.Grades.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        NotifyChanged();
        return true;
    }

    private static Grade Normalize(Grade grade) => new()
    {
        Id = grade.Id, CourseId = grade.CourseId, Title = grade.Title?.Trim() ?? string.Empty,
        Score = grade.Score, MaxScore = grade.MaxScore, Weight = grade.Weight, Date = grade.Date
    };

    private static Grade ToFrontendModel(Grade grade) => new()
    {
        Id = grade.Id, CourseId = grade.CourseId, CourseName = grade.Course?.Code ?? grade.CourseName,
        Title = grade.Title, Score = grade.Score, MaxScore = grade.MaxScore, Weight = grade.Weight,
        Date = grade.Date, Credits = grade.Course?.Credits ?? grade.Credits
    };
}
