using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Database;

public sealed class IdentitySettingsService(
    IDbContextFactory<AppDbContext> factory,
    ICurrentUserService currentUser,
    UserManager<ApplicationUser> users) : ISettingsService
{
    public event Action? Changed;

    public async Task<StudentSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("The signed-in account is no longer available.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var stored = await db.UserPlannerSettings.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? new UserPlannerSettings { UserId = userId };
        return ToSettings(stored, user);
    }

    public async Task<StudentSettings> SaveSettingsAsync(StudentSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settings.NotificationPreferences);
        if (settings.DailyStudyCapacityMinutes is < 30 or > 720) throw new ValidationException("Daily study capacity must be between 30 and 720 minutes.");
        if (!Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.GpaScale) || !Enum.IsDefined(settings.Language))
            throw new ValidationException("Choose supported account preference values.");
        var userId = await currentUser.GetRequiredUserIdAsync(cancellationToken);
        var user = await users.FindByIdAsync(userId)
            ?? throw new UnauthorizedAccessException("The signed-in account is no longer available.");
        var stored = ToEntity(settings, userId);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var existing = await db.UserPlannerSettings.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (existing is null) db.UserPlannerSettings.Add(stored);
        else
        {
            existing.StudentId = stored.StudentId;
            existing.Program = stored.Program;
            existing.Theme = stored.Theme;
            existing.Language = stored.Language;
            existing.TimeZone = stored.TimeZone;
            existing.Semester = stored.Semester;
            existing.AcademicYear = stored.AcademicYear;
            existing.GpaScale = stored.GpaScale;
            existing.AssignmentReminders = stored.AssignmentReminders;
            existing.ExamReminders = stored.ExamReminders;
            existing.DeadlineAlerts = stored.DeadlineAlerts;
            existing.WeeklySummary = stored.WeeklySummary;
            existing.DailyStudyCapacityMinutes = stored.DailyStudyCapacityMinutes;
        }
        await db.SaveChangesAsync(cancellationToken);
        Changed?.Invoke();
        return ToSettings(stored, user);
    }

    private static UserPlannerSettings ToEntity(StudentSettings value, string userId) => new()
    {
        UserId = userId,
        StudentId = value.StudentId,
        Program = value.Program,
        Theme = value.Theme,
        Language = value.Language,
        TimeZone = value.TimeZone,
        Semester = value.Semester,
        AcademicYear = value.AcademicYear,
        GpaScale = value.GpaScale,
        DailyStudyCapacityMinutes = value.DailyStudyCapacityMinutes,
        AssignmentReminders = value.NotificationPreferences.AssignmentReminders,
        ExamReminders = value.NotificationPreferences.ExamReminders,
        DeadlineAlerts = value.NotificationPreferences.DeadlineAlerts,
        WeeklySummary = value.NotificationPreferences.WeeklySummary,
    };

    private static StudentSettings ToSettings(UserPlannerSettings value, ApplicationUser user) => new()
    {
        FullName = user.FullName,
        Email = user.Email ?? string.Empty,
        StudentId = value.StudentId,
        Program = value.Program,
        Theme = value.Theme,
        Language = value.Language,
        TimeZone = value.TimeZone,
        Semester = value.Semester,
        AcademicYear = value.AcademicYear,
        GpaScale = value.GpaScale,
        DailyStudyCapacityMinutes = value.DailyStudyCapacityMinutes,
        NotificationPreferences = new()
        {
            AssignmentReminders = value.AssignmentReminders,
            ExamReminders = value.ExamReminders,
            DeadlineAlerts = value.DeadlineAlerts,
            WeeklySummary = value.WeeklySummary
        }
    };
}
