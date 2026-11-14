namespace StudentPlanner.Services.Insights;

public sealed record WorkItem(int AssignmentId, string Title, string Course, DateTime DueDate, int EstimatedMinutes, int Priority);
public sealed record CalendarBlock(DateTime Date, int Minutes, string Title);
public sealed record DayLoad(DateTime Date, int ScheduledMinutes, int PlannedMinutes, int CapacityMinutes, int AvailableMinutes, IReadOnlyList<string> Assignments);
public sealed record PlannedTask(int AssignmentId, string Title, DateTime Date, int Minutes);
public sealed record WorkloadRisk(string Code, string Severity, string Title, string Explanation, int? AssignmentId = null, DateTime? Date = null);
public sealed record StudyInsightsResult(int HorizonDays, int DailyCapacityMinutes, int PendingMinutes, int PlannedMinutes,
    int UnplannedMinutes, IReadOnlyList<DayLoad> Days, IReadOnlyList<PlannedTask> Plan, IReadOnlyList<WorkloadRisk> Risks,
    DateTime? MostDemandingDate, int DeadlineConcentration);

public static class WorkloadPlanner
{
    public static StudyInsightsResult Analyze(DateTime today, int horizonDays, int dailyCapacityMinutes,
        IEnumerable<WorkItem> assignments, IEnumerable<CalendarBlock> calendar)
    {
        if (horizonDays is not (7 or 14 or 30)) throw new ArgumentOutOfRangeException(nameof(horizonDays));
        if (dailyCapacityMinutes is < 30 or > 720) throw new ArgumentOutOfRangeException(nameof(dailyCapacityMinutes));
        var start = today.Date;
        var end = start.AddDays(horizonDays - 1);
        Func<WorkItem, DateTime> deadlineKey = item => item.DueDate.Date;
        Func<WorkItem, int> priorityKey = item => item.Priority;
        var tasks = assignments.Where(item => deadlineKey(item) >= start && deadlineKey(item) <= end && item.EstimatedMinutes > 0)
            .OrderBy(deadlineKey).ThenByDescending(priorityKey).ThenBy(item => item.Title, StringComparer.Ordinal).ToArray();
        var blocks = calendar.Where(e => e.Date.Date >= start && e.Date.Date <= end && e.Minutes > 0).ToArray();
        var blockByDay = blocks.GroupBy(b => b.Date.Date).ToDictionary(g => g.Key, g => g.Aggregate(0, (total, block) => total + block.Minutes));
        var loads = Enumerable.Range(0,horizonDays).Select(offset =>
        {
            var date=start.AddDays(offset); var scheduled=blockByDay.GetValueOrDefault(date);
            return new MutableDay(date, scheduled, dailyCapacityMinutes, Math.Max(0,dailyCapacityMinutes-scheduled));
        }).ToArray();
        var plan = new List<PlannedTask>(); var unplanned = new Dictionary<int,int>();
        foreach (var task in tasks)
        {
            var remaining=task.EstimatedMinutes;
            foreach (var day in loads.Where(d => d.Date <= task.DueDate.Date).OrderBy(d => d.PlannedMinutes).ThenBy(d => d.Date))
            {
                if (remaining == 0) break;
                var minutes=Math.Min(remaining,day.AvailableMinutes-day.PlannedMinutes);
                if (minutes <= 0) continue;
                day.PlannedMinutes+=minutes; day.TaskTitles.Add(task.Title); remaining-=minutes;
                plan.Add(new(task.AssignmentId,task.Title,day.Date,minutes));
            }
            if (remaining > 0) unplanned[task.AssignmentId]=remaining;
        }
        var risks=new List<WorkloadRisk>();
        foreach (var day in loads.Where(d=>d.ScheduledMinutes>d.CapacityMinutes))
            risks.Add(new("calendar-over-capacity","high","Existing events exceed daily study capacity",$"{day.Date:ddd, MMM d}: {day.ScheduledMinutes} scheduled minutes against {day.CapacityMinutes} available study minutes.",Date:day.Date));
        var concentrated=tasks.GroupBy(t=>t.DueDate.Date).Where(g=>g.Count()>=2).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).FirstOrDefault();
        if (concentrated is not null) risks.Add(new("deadline-cluster","medium","Several deadlines share a day",$"{concentrated.Count()} unfinished assignments are due {concentrated.Key:ddd, MMM d}.",Date:concentrated.Key));
        foreach (var task in tasks.Where(t=>t.Priority>=2 && t.DueDate.Date<=start.AddDays(3)))
            risks.Add(new("near-high-priority","high","High-priority deadline is approaching",$"{task.Title} is due {task.DueDate:ddd, MMM d}; the plan prioritizes earlier deadlines.",task.AssignmentId,task.DueDate.Date));
        foreach (var task in tasks.Where(t=>unplanned.ContainsKey(t.AssignmentId)))
            risks.Add(new("capacity-shortfall","high","Available capacity is insufficient",$"{task.Title}: {unplanned[task.AssignmentId]} minutes could not be placed before its deadline.",task.AssignmentId,task.DueDate.Date));
        var dayLoads=loads.Select(d=>new DayLoad(d.Date,d.ScheduledMinutes,d.PlannedMinutes,d.CapacityMinutes,d.AvailableMinutes,d.TaskTitles.ToArray())).ToArray();
        var busiest=dayLoads.OrderByDescending(d=>d.PlannedMinutes+d.ScheduledMinutes).ThenBy(d=>d.Date).FirstOrDefault();
        return new(horizonDays,dailyCapacityMinutes,tasks.Sum(t=>t.EstimatedMinutes),plan.Sum(p=>p.Minutes),unplanned.Values.Sum(),dayLoads,plan,Risks: risks.ToArray(),busiest?.Date,concentrated?.Count() ?? 0);
    }
    private sealed class MutableDay(DateTime date,int scheduledMinutes,int capacityMinutes,int availableMinutes)
    {
        public DateTime Date { get; }=date; public int ScheduledMinutes { get; }=scheduledMinutes; public int CapacityMinutes { get; }=capacityMinutes; public int AvailableMinutes { get; }=availableMinutes; public int PlannedMinutes { get; set; } public List<string> TaskTitles { get; }=[];
    }
}
