using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class EfDashboardService(IDbContextFactory<AppDbContext> factory, ISettingsService settings) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var today = DateTime.Today;
        var courses = await db.Courses.AsNoTracking().ToListAsync(cancellationToken);
        var totalAssignments = await db.Assignments.CountAsync(cancellationToken);
        var completed = await db.Assignments.CountAsync(assignment => assignment.Status == AssignmentStatus.Completed, cancellationToken);
        var pending = totalAssignments - completed;
        var overdue = await db.Assignments.CountAsync(assignment =>
            assignment.Status != AssignmentStatus.Completed && assignment.DueDate < today, cancellationToken);
        var upcoming = await db.Assignments.AsNoTracking().Include(assignment => assignment.Course)
            .Where(assignment => assignment.Status != AssignmentStatus.Completed && assignment.DueDate >= today)
            .OrderBy(assignment => assignment.DueDate).ThenByDescending(assignment => assignment.Priority)
            .Take(5).ToListAsync(cancellationToken);
        var todayEvents = await db.ScheduleEvents.AsNoTracking().Include(item => item.Course)
            .Where(item => item.StartTime >= today && item.StartTime < today.AddDays(1))
            .OrderBy(item => item.StartTime).ToListAsync(cancellationToken);
        var grades = await db.Grades.AsNoTracking().ToListAsync(cancellationToken);
        var preferences = await settings.GetSettingsAsync(cancellationToken);

        return new DashboardSnapshot(
            courses.Count,
            totalAssignments,
            completed,
            pending,
            overdue,
            totalAssignments == 0 ? 0 : completed * 100m / totalAssignments,
            GpaCalculator.Calculate(grades, courses, preferences.GpaScale),
            upcoming.Select(item => new Assignment
            {
                Id = item.Id, CourseId = item.CourseId, CourseName = item.Course?.Code ?? string.Empty,
                Title = item.Title, Description = item.Description, DueDate = item.DueDate,
                Priority = item.Priority, Status = item.Status, EstimatedMinutes = item.EstimatedMinutes,
                CompletedAt = item.CompletedAt
            }).ToArray(),
            todayEvents.Select(item => new ScheduleEvent
            {
                Id = item.Id, CourseId = item.CourseId, CourseName = item.Course?.Code ?? "Personal",
                Title = item.Title, Description = item.Description, StartTime = item.StartTime,
                EndTime = item.EndTime, Location = item.Location, Type = item.Type
            }).ToArray());
    }
}
