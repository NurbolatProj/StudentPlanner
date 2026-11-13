using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;

namespace StudentPlanner.Services.Database;

public abstract class EfServiceBase<TService>(IDbContextFactory<AppDbContext> contextFactory, ILogger<TService> logger)
{
    protected IDbContextFactory<AppDbContext> ContextFactory { get; } = contextFactory;
    protected ILogger<TService> Logger { get; } = logger;
    public event Action? Changed;

    protected void NotifyChanged()
    {
        foreach (var handler in Changed?.GetInvocationList().Cast<Action>() ?? Enumerable.Empty<Action>())
        {
            try { handler(); }
            catch (Exception exception) { Logger.LogError(exception, "A planner change notification handler failed."); }
        }
    }

    protected static void Validate(object model)
    {
        Validator.ValidateObject(model, new ValidationContext(model), validateAllProperties: true);
        if (model is Assignment assignment
            && (!Enum.IsDefined(assignment.Priority) || !Enum.IsDefined(assignment.Status)))
            throw new ValidationException("Select a valid assignment priority and status.");
        if (model is ScheduleEvent scheduleEvent && !Enum.IsDefined(scheduleEvent.Type))
            throw new ValidationException("Select a valid event type.");
    }

    protected static async Task EnsureCourseExistsAsync(AppDbContext db, int? courseId, CancellationToken cancellationToken)
    {
        if (courseId is int id && !await db.Courses.AsNoTracking().AnyAsync(course => course.Id == id, cancellationToken))
            throw new ValidationException("The selected course no longer exists.");
    }

    protected static bool IsForeignKeyViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 787 };

    protected static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 or 1555 };
}
