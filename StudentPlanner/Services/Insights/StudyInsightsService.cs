using Microsoft.EntityFrameworkCore;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Insights;

public interface IStudyInsightsService { Task<StudyInsightsResult> GetAsync(DateTime today,int horizonDays,CancellationToken cancellationToken=default); }
public sealed class StudyInsightsService(IDbContextFactory<AppDbContext> factory,ICurrentUserService currentUser) : IStudyInsightsService
{
    public async Task<StudyInsightsResult> GetAsync(DateTime today,int horizonDays,CancellationToken cancellationToken=default)
    {
        if (horizonDays is not (7 or 14 or 30)) throw new ArgumentOutOfRangeException(nameof(horizonDays));
        var owner=await currentUser.GetRequiredUserIdAsync(cancellationToken);
        await using var db=await factory.CreateDbContextAsync(cancellationToken);
        var settings=await db.UserPlannerSettings.AsNoTracking().SingleOrDefaultAsync(s=>s.UserId==owner,cancellationToken);
        var end=today.Date.AddDays(horizonDays);
        var assignments=await db.Assignments.AsNoTracking().Where(a=>a.OwnerId==owner&&a.Status!=AssignmentStatus.Completed&&a.DueDate>=today.Date&&a.DueDate<end)
            .Select(a=>new WorkItem(a.Id,a.Title,a.Course!.Code,a.DueDate,a.EstimatedMinutes,(int)a.Priority)).ToListAsync(cancellationToken);
        var calendarEvents=await db.ScheduleEvents.AsNoTracking().Where(e=>e.OwnerId==owner&&e.StartTime>=today.Date&&e.StartTime<end)
            .Select(e=>new {e.StartTime,e.EndTime,e.Title}).ToListAsync(cancellationToken);
        var events=calendarEvents.Select(e=>new CalendarBlock(e.StartTime.Date,Math.Max(0,(int)(e.EndTime-e.StartTime).TotalMinutes),e.Title)).ToList();
        return WorkloadPlanner.Analyze(today,horizonDays,settings?.DailyStudyCapacityMinutes??120,assignments,events);
    }
}
