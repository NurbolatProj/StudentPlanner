using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.JSInterop;
using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services;

/// <summary>Per-browser frontend preferences. A backend settings service can replace this DI registration later.</summary>
public sealed class BrowserSettingsService(IJSRuntime js) : ISettingsService, ISettingsInitializer
{
    private const string StorageKey = "student-planner.settings.v1";
    private readonly SemaphoreSlim gate = new(1, 1);
    private StudentSettings value = new();
    private bool initialized;
    public event Action? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (initialized) return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (initialized) return;
            string? json = null;
            try { json = await js.InvokeAsync<string?>("studentPlannerStorage.get", cancellationToken, StorageKey); }
            catch (JSException) { /* Storage may be unavailable in a restricted browser; keep circuit defaults. */ }
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { value = JsonSerializer.Deserialize<StudentSettings>(json) ?? new StudentSettings(); }
                catch (JsonException) { value = new StudentSettings(); }
            }
            initialized = true;
        }
        finally { gate.Release(); }
        Changed?.Invoke();
    }

    public async Task<StudentSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        // During prerender, return safe defaults. Browser storage is loaded by the layout's first interactive render.
        if (!initialized) return Clone(value);
        await gate.WaitAsync(cancellationToken);
        try { return Clone(value); }
        finally { gate.Release(); }
    }

    public async Task<StudentSettings> SaveSettingsAsync(StudentSettings settings, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        Validator.ValidateObject(settings, new ValidationContext(settings), true);
        var copy = Clone(settings);
        var json = JsonSerializer.Serialize(copy);
        await js.InvokeVoidAsync("studentPlannerStorage.set", cancellationToken, StorageKey, json);
        await gate.WaitAsync(cancellationToken);
        try { value = copy; }
        finally { gate.Release(); }
        Changed?.Invoke();
        return Clone(copy);
    }

    private static StudentSettings Clone(StudentSettings x) => new()
    {
        FullName=x.FullName, StudentId=x.StudentId, Program=x.Program, Email=x.Email, Theme=x.Theme, Language=x.Language,
        TimeZone=x.TimeZone, Semester=x.Semester, AcademicYear=x.AcademicYear,
        GpaScale=x.GpaScale, NotificationPreferences=new()
        {
            AssignmentReminders=x.NotificationPreferences.AssignmentReminders,
            ExamReminders=x.NotificationPreferences.ExamReminders,
            DeadlineAlerts=x.NotificationPreferences.DeadlineAlerts,
            WeeklySummary=x.NotificationPreferences.WeeklySummary
        }
    };
}
