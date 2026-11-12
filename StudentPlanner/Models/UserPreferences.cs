namespace StudentPlanner.Models;

public sealed class UserPreferences
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "English";
    public string TimeZone { get; set; } = "Asia/Almaty";
    public string Semester { get; set; } = "2nd Semester";
    public string AcademicYear { get; set; } = "2026-2027";
    public string GpaScale { get; set; } = "4.0";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string StudentId { get; set; } = "";
    public string Program { get; set; } = "";
    public bool AssignmentNotifications { get; set; } = true;
    public bool ExamNotifications { get; set; } = true;
    public bool DeadlineNotifications { get; set; } = true;
    public bool WeeklySummary { get; set; }
}
