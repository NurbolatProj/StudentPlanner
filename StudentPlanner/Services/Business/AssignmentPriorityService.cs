using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services.Business;

public sealed record AssignmentPriorityWeights
{
    public double OverdueBase { get; init; } = 80;
    public double OverduePerDay { get; init; } = 5;
    public double OverdueCap { get; init; } = 20;
    public double DueToday { get; init; } = 75;
    public double DueTomorrow { get; init; } = 70;
    public double DueWithinThreeDays { get; init; } = 55;
    public double DueWithinSevenDays { get; init; } = 40;
    public double FutureBase { get; init; } = 35;
    public double FutureDecayPerDay { get; init; } = .5;
    public double MinimumUrgency { get; init; } = 5;
    public double HighPriority { get; init; } = 35;
    public double MediumPriority { get; init; } = 20;
    public double LowPriority { get; init; } = 5;
    public double WorkloadPerHour { get; init; } = 3;
    public double WorkloadCap { get; init; } = 15;
}

public sealed class AssignmentPriorityService(AssignmentPriorityWeights? weights = null) : IAssignmentPriorityService
{
    private static readonly AssignmentPriorityWeights DefaultWeights = new();
    private readonly AssignmentPriorityWeights scoringWeights = ValidateWeights(weights ?? DefaultWeights);

    private static AssignmentPriorityWeights ValidateWeights(AssignmentPriorityWeights value)
    {
        var values = new[]
        {
            value.OverdueBase, value.OverduePerDay, value.OverdueCap, value.DueToday,
            value.DueTomorrow, value.DueWithinThreeDays, value.DueWithinSevenDays,
            value.FutureBase, value.FutureDecayPerDay, value.MinimumUrgency,
            value.HighPriority, value.MediumPriority, value.LowPriority,
            value.WorkloadPerHour, value.WorkloadCap
        };
        if (values.Any(number => !double.IsFinite(number) || number < 0))
            throw new ArgumentOutOfRangeException(nameof(value), "Priority weights must be finite, non-negative values.");
        return value;
    }

    public IReadOnlyList<PrioritizedAssignmentDto> Prioritize(IEnumerable<Assignment> assignments, DateOnly today, int take = 5)
        => PrioritizeUsing(assignments, today, (assignment, date) => CalculateScore(assignment, date, scoringWeights), take);

    public static IReadOnlyList<PrioritizedAssignmentDto> PrioritizeUsing(
        IEnumerable<Assignment> assignments,
        DateOnly today,
        Func<Assignment, DateOnly, double> scoreSelector,
        int take = 5)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(scoreSelector);
        if (take <= 0) return Array.Empty<PrioritizedAssignmentDto>();

        var recommendations = assignments
            .Where(assignment => assignment.Status != AssignmentStatus.Completed)
            .Select(assignment => ToRecommendation(assignment, today, scoreSelector))
            .ToArray();
        if (recommendations.Any(item => !double.IsFinite(item.PriorityScore) || item.PriorityScore < 0))
            throw new ArgumentOutOfRangeException(nameof(scoreSelector), "Priority scores must be finite and non-negative.");

        return recommendations
            .OrderByDescending(recommendation => recommendation.PriorityScore)
            .ThenBy(recommendation => recommendation.DueDate)
            .ThenBy(recommendation => recommendation.AssignmentId)
            .Take(take)
            .ToArray();
    }

    public static double CalculateScore(Assignment assignment, DateOnly today) => CalculateScore(assignment, today, DefaultWeights);

    private static double CalculateScore(Assignment assignment, DateOnly today, AssignmentPriorityWeights weights)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.Status == AssignmentStatus.Completed) return double.NegativeInfinity;

        var daysRemaining = assignment.DueDate.Date.Subtract(today.ToDateTime(TimeOnly.MinValue)).Days;
        var urgency = daysRemaining switch
        {
            < 0 => weights.OverdueBase + Math.Min(Math.Abs(daysRemaining) * weights.OverduePerDay, weights.OverdueCap),
            0 => weights.DueToday,
            1 => weights.DueTomorrow,
            <= 3 => weights.DueWithinThreeDays,
            <= 7 => weights.DueWithinSevenDays,
            _ => Math.Max(weights.MinimumUrgency, weights.FutureBase - (daysRemaining - 7) * weights.FutureDecayPerDay)
        };

        var importance = assignment.Priority switch
        {
            AssignmentPriority.High => weights.HighPriority,
            AssignmentPriority.Medium => weights.MediumPriority,
            _ => weights.LowPriority
        };
        var workload = Math.Min(Math.Max(assignment.EstimatedMinutes, 0) / 60d * weights.WorkloadPerHour, weights.WorkloadCap);
        return Math.Round(urgency + importance + workload, 2, MidpointRounding.AwayFromZero);
    }

    private static PrioritizedAssignmentDto ToRecommendation(Assignment assignment, DateOnly today, Func<Assignment, DateOnly, double> scoreSelector)
    {
        var due = DateOnly.FromDateTime(assignment.DueDate.Date);
        var days = due.DayNumber - today.DayNumber;
        var urgency = days < 0 ? $"Overdue by {-days} day(s)" : days == 0 ? "Due today" : days == 1 ? "Due tomorrow" : $"Due in {days} days";
        var priority = $"{assignment.Priority} priority";
        var workload = assignment.EstimatedMinutes >= 120 ? $"about {Math.Ceiling(assignment.EstimatedMinutes / 60d):0} hours of work" : null;
        var reason = string.Join(" · ", new[] { urgency, priority, workload }.Where(part => part is not null));
        return new(assignment.Id, assignment.Title, scoreSelector(assignment, today), reason, assignment.DueDate);
    }
}
