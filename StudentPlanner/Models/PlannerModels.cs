using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentPlanner.Models;

public enum AssignmentPriority { Low, Medium, High }
public enum AssignmentStatus { Pending, InProgress, Completed }
public enum ScheduleEventType { Class, Exam, Assignment, Personal }
public enum GpaScale { FourPoint = 4, FivePoint = 5, HundredPoint = 100 }
public enum ThemePreference { Dark, Light }
public enum AppLanguage { English }

public sealed class Student
{
    public int Id { get; set; } = 1;
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "Nurbolat Otep";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "student@astanait.edu.kz";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Course
{
    public int Id { get; set; }
    public string? OwnerId { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Name { get; set; } = "";
    [Required, StringLength(15, MinimumLength = 2)] public string Code { get; set; } = "";
    [Required, StringLength(100)] public string Instructor { get; set; } = "";
    [Range(1, 10)] public int Credits { get; set; } = 3;
    [RegularExpression("purple|blue|green|orange|pink")] public string Color { get; set; } = "purple";
    public bool IsActive { get; set; } = true;
    [StringLength(500)] public string Description { get; set; } = "";
    [StringLength(40)] public string Room { get; set; } = "";
    [StringLength(100)] public string Schedule { get; set; } = "";
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
    public ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();
}

public sealed class Assignment
{
    public int Id { get; set; }
    public string? OwnerId { get; set; }
    [Range(1, int.MaxValue)] public int CourseId { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Title { get; set; } = "";
    [StringLength(500)] public string Description { get; set; } = "";
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(7);
    public AssignmentPriority Priority { get; set; } = AssignmentPriority.Medium;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    [Range(0, 10000)] public int EstimatedMinutes { get; set; } = 60;
    public DateTime? CompletedAt { get; set; }
    // Display-only course label. CourseId is the persisted relationship.
    [NotMapped]
    public string CourseName { get; set; } = "";
    public Course? Course { get; set; }
    [NotMapped]
    public bool IsCompleted { get => Status == AssignmentStatus.Completed; set { Status=value?AssignmentStatus.Completed:AssignmentStatus.Pending; CompletedAt=value?(CompletedAt??DateTime.Now):null; } }
}

public sealed class Grade : IValidatableObject
{
    public int Id { get; set; }
    public string? OwnerId { get; set; }
    [Range(1, int.MaxValue)] public int CourseId { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string Title { get; set; } = "";
    [Range(typeof(decimal), "0", "100")] public decimal Score { get; set; }
    [Range(typeof(decimal), "0.01", "100")] public decimal MaxScore { get; set; } = 100;
    [Range(typeof(decimal), "0", "100")] public decimal Weight { get; set; } = 100;
    public DateTime Date { get; set; } = DateTime.Today;
    [NotMapped] public string CourseName { get; set; } = "";
    public Course? Course { get; set; }
    [NotMapped] public int Credits { get; set; } = 3;
    [NotMapped]
    public decimal Percentage => MaxScore <= 0 ? 0 : Math.Clamp(Score / MaxScore * 100m, 0m, 100m);
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Score > MaxScore) yield return new ValidationResult("Score cannot exceed the maximum score.", new[] { nameof(Score), nameof(MaxScore) });
    }
}

public sealed class ScheduleEvent : IValidatableObject
{
    public int Id { get; set; }
    public string? OwnerId { get; set; }
    public int? CourseId { get; set; }
    [Required, StringLength(100, MinimumLength = 3)] public string Title { get; set; } = "";
    [StringLength(500)] public string Description { get; set; } = "";
    public DateTime StartTime { get; set; } = DateTime.Today.AddHours(9);
    public DateTime EndTime { get; set; } = DateTime.Today.AddHours(10);
    [StringLength(100)] public string Location { get; set; } = "";
    public ScheduleEventType Type { get; set; } = ScheduleEventType.Class;
    [NotMapped] public string CourseName { get; set; } = "";
    public Course? Course { get; set; }
    [NotMapped] public DateTime Date { get => StartTime.Date; set { StartTime = value.Date.Add(StartTime.TimeOfDay); EndTime = value.Date.Add(EndTime.TimeOfDay); } }
    [NotMapped] public string Time { get => StartTime.ToString("HH:mm"); set { if (TimeSpan.TryParse(value, out var time)) StartTime = StartTime.Date.Add(time); } }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndTime <= StartTime) yield return new ValidationResult("End time must be later than start time.", new[] { nameof(EndTime) });
    }
}

public sealed class NotificationPreferences
{
    public bool AssignmentReminders { get; set; } = true;
    public bool ExamReminders { get; set; } = true;
    public bool DeadlineAlerts { get; set; } = true;
    public bool WeeklySummary { get; set; }
}

public sealed class StudentSettings
{
    [Required, StringLength(100, MinimumLength = 2)] public string FullName { get; set; } = "Student";
    public string StudentId { get; set; } = "IT-2508";
    public string Program { get; set; } = "Computer Science";
    [Required, EmailAddress] public string Email { get; set; } = "student@example.invalid";
    public ThemePreference Theme { get; set; } = ThemePreference.Dark;
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public string TimeZone { get; set; } = "Asia/Almaty";
    public string Semester { get; set; } = "2nd Semester";
    public string AcademicYear { get; set; } = "2026-2027";
    public GpaScale GpaScale { get; set; } = GpaScale.FourPoint;
    [Range(30, 720)] public int DailyStudyCapacityMinutes { get; set; } = 120;
    public NotificationPreferences NotificationPreferences { get; set; } = new();
}

public sealed class UserPlannerSettings
{
    [Key, StringLength(450)] public string UserId { get; set; } = string.Empty;
    public string StudentId { get; set; } = "";
    public string Program { get; set; } = "";
    public ThemePreference Theme { get; set; } = ThemePreference.Dark;
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public string TimeZone { get; set; } = "Asia/Almaty";
    public string Semester { get; set; } = "";
    public string AcademicYear { get; set; } = "";
    public GpaScale GpaScale { get; set; } = GpaScale.FourPoint;
    public bool AssignmentReminders { get; set; } = true;
    public bool ExamReminders { get; set; } = true;
    public bool DeadlineAlerts { get; set; } = true;
    public bool WeeklySummary { get; set; }
    [Range(30, 720)] public int DailyStudyCapacityMinutes { get; set; } = 120;
}

public sealed record DashboardSnapshot(int CourseCount, int AssignmentCount, int CompletedCount,
    int PendingCount, int OverdueCount, decimal CompletionPercentage, decimal Gpa,
    IReadOnlyList<Assignment> UpcomingDeadlines, IReadOnlyList<ScheduleEvent> TodayEvents);

public static class GpaCalculator
{
    // Demonstration conversion: linear percentage-to-scale. Institution-specific bands must be agreed separately.
    public static decimal ConvertPercentage(decimal percentage, GpaScale scale) =>
        Math.Clamp(percentage, 0m, 100m) * (int)scale / 100m;

    // Scores of assessments are weighted within a course; course GPAs are then weighted by course credits.
    public static decimal Calculate(IEnumerable<Grade> grades, IEnumerable<Course> courses, GpaScale scale)
    {
        var courseMap = courses.ToDictionary(c => c.Id, c => Math.Max(1, c.Credits));
        var courseResults = grades.GroupBy(g => g.CourseId)
            .Select(group => new {
                Credits = courseMap.GetValueOrDefault(group.Key, Math.Max(1, group.First().Credits)),
                Weight = group.Sum(g => g.Weight),
                Score = group.Sum(g => g.Percentage * g.Weight)
            }).Where(x => x.Weight > 0).ToList();
        var totalCredits = courseResults.Sum(x => x.Credits);
        if (totalCredits == 0) return 0m;
        return courseResults.Sum(x => ConvertPercentage(x.Score / x.Weight, scale) * x.Credits) / totalCredits;
    }
}

public enum FocusSessionType { Focus, ShortBreak, LongBreak }
public enum FocusSessionStatus { Completed, Stopped, Interrupted }

public sealed class FocusSession
{
    public int Id { get; set; }
    [Required, StringLength(450)] public string OwnerId { get; set; } = string.Empty;
    public int? AssignmentId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    [Range(0, 86400)] public int ActualDurationSeconds { get; set; }
    public FocusSessionType Type { get; set; } = FocusSessionType.Focus;
    public FocusSessionStatus Status { get; set; } = FocusSessionStatus.Interrupted;
    public Assignment? Assignment { get; set; }
}

public sealed record FocusSessionSummary(int Id, int? AssignmentId, string AssignmentTitle, string CourseCode,
    DateTimeOffset StartTime, DateTimeOffset? EndTime, int ActualDurationSeconds, FocusSessionType Type, FocusSessionStatus Status);
public sealed record FocusStatistics(int TodaySeconds, int WeekSeconds, int CompletedSessions, int AverageSessionSeconds,
    IReadOnlyList<FocusSessionSummary> RecentSessions);
