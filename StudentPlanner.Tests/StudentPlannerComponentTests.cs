using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using StudentPlanner.Components.Layout;
using StudentPlanner.Components.Pages;
using StudentPlanner.Models;
using StudentPlanner.Services;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services.Mock;
using Xunit;

namespace StudentPlanner.Tests;

public sealed class StudentPlannerComponentTests
{
    [Fact]
    public void NavigationLinksRenderAndThemeTogglePersists()
    {
        using var context = new TestContext();
        var preferences = new TestPreferencesService();
        context.Services.AddScoped<IUserPreferencesService>(_ => preferences);

        var layout = context.RenderComponent<MainLayout>(parameters => parameters
            .Add(component => component.Body, (RenderFragment)(builder => builder.AddContent(0, "Test page"))));

        Assert.Contains("Test page", layout.Markup);
        Assert.Contains("href=\"/assignments\"", layout.Markup);
        Assert.Contains("href=\"/courses\"", layout.Markup);
        Assert.Contains("href=\"/grades\"", layout.Markup);

        layout.Find("button.theme-button").Click();

        Assert.Equal("Light", preferences.Current.Theme);
    }

    [Fact]
    public async Task DashboardReadsAndUpdatesAssignmentsFromSharedService()
    {
        using var context = new TestContext();
        var assignments = new AssignmentService();
        context.Services.AddScoped<IAssignmentService>(_ => assignments);
        context.Services.AddScoped<ICourseService, CourseService>();
        context.Services.AddScoped<IGradeService, GradeService>();
        context.Services.AddScoped<IScheduleEventService, ScheduleEventService>();
        context.Services.AddScoped<IStudentService, StudentService>();
        context.Services.AddScoped<IUserPreferencesService, TestPreferencesService>();

        var page = context.RenderComponent<Home>();
        var before = await assignments.GetAllAsync();
        Assert.Contains(before, item => item.Status == "Pending");

        page.Find("button[aria-label='Toggle task completion']").Click();

        var after = await assignments.GetAllAsync();
        Assert.Contains(after, item => item.Status == "Completed");
    }

    [Fact]
    public void CoursesCanBeSortedByCourseCode()
    {
        using var context = new TestContext();
        context.Services.AddScoped<ICourseService, CourseService>();

        var page = context.RenderComponent<Courses>();
        page.Find("select[aria-label='Sort courses']").Change("code");

        var names = page.FindAll(".course-card h3").Select(element => element.TextContent.Trim()).ToArray();
        Assert.Equal("Programming", names[0]);
        Assert.Equal("Database Systems", names[1]);
    }

    [Fact]
    public void GradesCanBeSortedByCourse()
    {
        using var context = new TestContext();
        context.Services.AddScoped<IGradeService, GradeService>();
        context.Services.AddScoped<IUserPreferencesService, TestPreferencesService>();

        var page = context.RenderComponent<Grades>();
        page.Find("select[aria-label='Sort grades']").Change("course");

        var firstCourse = page.Find("tbody tr td").TextContent.Trim();
        Assert.Contains("Database Systems", firstCourse);
    }

    [Fact]
    public void AssignmentFormOpensAsAccessibleDialog()
    {
        using var context = new TestContext();
        context.Services.AddScoped<IAssignmentService, AssignmentService>();

        var page = context.RenderComponent<Assignments>();
        page.Find("button.add-button").Click();

        var dialog = page.Find("[role='dialog'][aria-modal='true']");
        Assert.Contains("Create Assignment", dialog.TextContent);
        Assert.False(string.IsNullOrWhiteSpace(dialog.GetAttribute("aria-labelledby")));
    }

    [Fact]
    public void SettingsValidateEmailAndPersistThemeAndGpaScale()
    {
        using var context = new TestContext();
        var preferences = new TestPreferencesService();
        context.Services.AddScoped<IUserPreferencesService>(_ => preferences);
        context.Services.AddScoped<IStudentService, StudentService>();

        var page = context.RenderComponent<Settings>();
        page.Find("#settings-email").Change("not-an-email");
        page.Find("button.save-button").Click();
        Assert.Contains("Please check your name and email address", page.Markup);

        page.Find("#settings-email").Change("student.updated@example.edu");
        page.FindAll("select")[0].Change("Light");
        page.FindAll("select")[^1].Change("5.0");
        page.Find("button.save-button").Click();

        Assert.Equal("Light", preferences.Current.Theme);
        Assert.Equal("5.0", preferences.Current.GpaScale);
        Assert.Equal("student.updated@example.edu", preferences.Current.Email);
    }

    private sealed class TestPreferencesService : IUserPreferencesService
    {
        public UserPreferences Current { get; private set; } = new();

        public Task<UserPreferences> LoadAsync() => Task.FromResult(Current);

        public Task SaveAsync(UserPreferences preferences)
        {
            Current = preferences;
            return Task.CompletedTask;
        }
    }
}
