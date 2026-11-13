using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Business;
using StudentPlanner.Services.Database;
using StudentPlanner.Services.Interfaces;
using Xunit;

namespace StudentPlanner.Tests;

public sealed class BackendIntegrationTests
{
    [Fact]
    public async Task MigrationsCreateRelationalSchemaAndServicesPersistAcrossContexts()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance);
        var course = await courses.CreateCourseAsync(NewCourse());
        var created = await assignments.CreateAssignmentAsync(new Assignment
        {
            CourseId = course.Id, Title = "Persistent assignment", DueDate = DateTime.Today.AddDays(2)
        });

        await using var freshContext = await database.Factory.CreateDbContextAsync();
        var stored = await freshContext.Assignments.AsNoTracking().Include(item => item.Course).SingleAsync();
        Assert.Equal("Persistent assignment", stored.Title);
        Assert.Equal(course.Id, stored.CourseId);
        Assert.Equal(course.Code, stored.Course!.Code);
        Assert.Equal(course.Code, created.CourseName);
        Assert.Single(await freshContext.Database.GetAppliedMigrationsAsync());
        Assert.Throws<SqliteException>(() => freshContext.Database.ExecuteSqlRaw(
            "INSERT INTO Assignments (CourseId, Title, Description, DueDate, Priority, Status, EstimatedMinutes, CompletedAt) VALUES (999, 'Bad FK', '', '2026-11-14 00:00:00', 1, 0, 60, NULL)"));
    }

    [Fact]
    public async Task FileDatabasePersistsCourseAssignmentGradeAndEventAcrossServiceRestarts()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"studentplanner-persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "planner.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False")
                .Options;
            var firstFactory = new TestContextFactory(options);
            await using (var first = await firstFactory.CreateDbContextAsync())
            {
                await first.Database.MigrateAsync();
                await first.Database.MigrateAsync();
            }

            var firstCourses = new EfCourseService(firstFactory, NullLogger<EfCourseService>.Instance);
            var firstAssignments = new EfAssignmentService(firstFactory, NullLogger<EfAssignmentService>.Instance);
            var firstGrades = new EfGradeService(firstFactory, NullLogger<EfGradeService>.Instance);
            var firstSchedule = new EfScheduleService(firstFactory, NullLogger<EfScheduleService>.Instance);
            var course = await firstCourses.CreateCourseAsync(NewCourse("FILE"));
            var assignment = await firstAssignments.CreateAssignmentAsync(new Assignment
            {
                CourseId = course.Id, Title = "Survives restart", DueDate = DateTime.Today.AddDays(2)
            });
            var grade = await firstGrades.CreateGradeAsync(new Grade
            {
                CourseId = course.Id, Title = "Persistent exam", Score = 82, MaxScore = 100, Weight = 100
            });
            var schedule = await firstSchedule.CreateEventAsync(new ScheduleEvent
            {
                CourseId = course.Id, Title = "Persistent class", StartTime = DateTime.Today.AddDays(1).AddHours(10),
                EndTime = DateTime.Today.AddDays(1).AddHours(11)
            });

            // New factory/services model a fresh application process connecting to the same on-disk DB.
            var restartedFactory = new TestContextFactory(options);
            var restartedCourses = new EfCourseService(restartedFactory, NullLogger<EfCourseService>.Instance);
            var restartedAssignments = new EfAssignmentService(restartedFactory, NullLogger<EfAssignmentService>.Instance);
            var restartedGrades = new EfGradeService(restartedFactory, NullLogger<EfGradeService>.Instance);
            var restartedSchedule = new EfScheduleService(restartedFactory, NullLogger<EfScheduleService>.Instance);
            Assert.Equal("FILE", Assert.Single(await restartedCourses.GetCoursesAsync()).Code);
            Assert.Equal("Survives restart", Assert.Single(await restartedAssignments.GetAssignmentsAsync()).Title);
            Assert.Equal("Persistent exam", Assert.Single(await restartedGrades.GetGradesAsync()).Title);
            Assert.Equal("Persistent class", Assert.Single(await restartedSchedule.GetEventsAsync()).Title);

            assignment.Title = "Updated after restart";
            grade.Score = 91;
            schedule.Title = "Updated class";
            Assert.Equal("Updated after restart", (await restartedAssignments.UpdateAssignmentAsync(assignment))!.Title);
            Assert.Equal(91m, (await restartedGrades.UpdateGradeAsync(grade))!.Score);
            Assert.Equal("Updated class", (await restartedSchedule.UpdateEventAsync(schedule))!.Title);

            var finalFactory = new TestContextFactory(options);
            var finalAssignments = new EfAssignmentService(finalFactory, NullLogger<EfAssignmentService>.Instance);
            var finalGrades = new EfGradeService(finalFactory, NullLogger<EfGradeService>.Instance);
            var finalSchedule = new EfScheduleService(finalFactory, NullLogger<EfScheduleService>.Instance);
            Assert.True(await finalAssignments.DeleteAssignmentAsync(assignment.Id));
            Assert.True(await finalGrades.DeleteGradeAsync(grade.Id));
            Assert.True(await finalSchedule.DeleteEventAsync(schedule.Id));
            Assert.Empty(await restartedAssignments.GetAssignmentsAsync());
            Assert.Empty(await restartedGrades.GetGradesAsync());
            Assert.Empty(await restartedSchedule.GetEventsAsync());
            Assert.Single(await restartedCourses.GetCoursesAsync());
        }
        finally
        {
            // This directory is uniquely created by this test and contains no user data.
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DevelopmentSeederPopulatesAnEmptyDatabaseOnceAndKeepsRecords()
    {
        await using var database = await TestDatabase.OpenAsync();
        var initializer = new DbInitializer(database.Factory, new TestWebHostEnvironment(), NullLogger<DbInitializer>.Instance);

        await initializer.InitializeAsync();
        await initializer.InitializeAsync();

        await using var db = await database.Factory.CreateDbContextAsync();
        Assert.Equal(6, await db.Courses.CountAsync());
        Assert.Equal(5, await db.Assignments.CountAsync());
        Assert.Equal(6, await db.Grades.CountAsync());
        Assert.Equal(7, await db.ScheduleEvents.CountAsync());
        Assert.Single(await db.Students.ToListAsync());
    }

    [Fact]
    public async Task CourseCodesAreUniqueAndRelatedAcademicRecordsRestrictDeletion()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance);
        var course = await courses.CreateCourseAsync(NewCourse());

        await Assert.ThrowsAsync<ValidationException>(() => courses.CreateCourseAsync(NewCourse("test")));
        await assignments.CreateAssignmentAsync(new Assignment { CourseId = course.Id, Title = "Linked task" });
        await Assert.ThrowsAsync<ValidationException>(() => courses.DeleteCourseAsync(course.Id));
        Assert.Null(await courses.UpdateCourseAsync(new Course { Id = 999, Name = "Absent Course", Code = "ABS", Instructor = "Test" }));
        var storedTask = (await assignments.GetAssignmentsAsync()).Single();
        Assert.True(await assignments.DeleteAssignmentAsync(storedTask.Id));
        Assert.True(await courses.DeleteCourseAsync(course.Id));
    }

    [Fact]
    public async Task AssignmentRejectsMissingCourseAndUpdateKeepsCompletionSemantics()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance);
        await Assert.ThrowsAsync<ValidationException>(() => assignments.CreateAssignmentAsync(new Assignment { CourseId = 999, Title = "Bad reference" }));

        var course = await courses.CreateCourseAsync(NewCourse());
        var item = await assignments.CreateAssignmentAsync(new Assignment { CourseId = course.Id, Title = "Task to complete" });
        item.Status = AssignmentStatus.Completed;
        var updated = await assignments.UpdateAssignmentAsync(item);
        Assert.Equal(AssignmentStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Null(await assignments.UpdateAssignmentAsync(new Assignment { Id = 500, CourseId = course.Id, Title = "Missing task" }));
    }

    [Fact]
    public async Task GradesValidateScoresUseCourseCreditsAndUpdateGpaInputs()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var grades = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance);
        var course = await courses.CreateCourseAsync(NewCourse(credits: 4));
        await Assert.ThrowsAsync<ValidationException>(() => grades.CreateGradeAsync(new Grade
        {
            CourseId = course.Id, Title = "Invalid", Score = 101, MaxScore = 100, Weight = 100
        }));

        var grade = await grades.CreateGradeAsync(new Grade
        {
            CourseId = course.Id, Title = "Midterm", Score = 80, MaxScore = 100, Weight = 100
        });
        Assert.Equal(4, grade.Credits);
        Assert.Equal(3.2m, GpaCalculator.Calculate(await grades.GetGradesAsync(), await courses.GetCoursesAsync(), GpaScale.FourPoint));
        grade.Score = 90;
        Assert.Equal(90m, (await grades.UpdateGradeAsync(grade))!.Score);
        Assert.True(await grades.DeleteGradeAsync(grade.Id));
    }

    [Fact]
    public async Task CalendarAllowsPersonalEventsValidatesCoursesAndRejectsOverlaps()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var schedule = new EfScheduleService(database.Factory, NullLogger<EfScheduleService>.Instance);
        var today = DateTime.Today.AddHours(10);
        var personal = await schedule.CreateEventAsync(new ScheduleEvent
        {
            Title = "Personal study", CourseId = null, StartTime = today, EndTime = today.AddHours(1), Type = ScheduleEventType.Personal
        });
        Assert.Null(personal.CourseId);
        await Assert.ThrowsAsync<ValidationException>(() => schedule.CreateEventAsync(new ScheduleEvent
        {
            Title = "Overlapping", StartTime = today.AddMinutes(30), EndTime = today.AddHours(2)
        }));
        await Assert.ThrowsAsync<ValidationException>(() => schedule.CreateEventAsync(new ScheduleEvent
        {
            CourseId = 900, Title = "Unknown course event", StartTime = today.AddHours(3), EndTime = today.AddHours(4)
        }));
        var course = await courses.CreateCourseAsync(NewCourse());
        var linked = await schedule.CreateEventAsync(new ScheduleEvent
        {
            CourseId = course.Id, Title = "Course session", StartTime = today.AddHours(2), EndTime = today.AddHours(3)
        });
        Assert.Equal(course.Code, linked.CourseName);
        Assert.Equal(2, (await schedule.FindEventsAsync(today.Date, today.Date.AddDays(1))).Count);
        Assert.True(await courses.DeleteCourseAsync(course.Id));
        Assert.Null((await schedule.GetEventsAsync()).Single(item => item.Id == linked.Id).CourseId);
    }

    [Fact]
    public async Task DashboardCountsMatchPersistentRecordsAndNotifySubscribers()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance);
        var grades = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance);
        var settings = new MockSettingsForTests();
        var dashboard = new EfDashboardService(database.Factory, settings);
        var changedCount = 0;
        assignments.Changed += () => changedCount++;

        var course = await courses.CreateCourseAsync(NewCourse());
        var task = await assignments.CreateAssignmentAsync(new Assignment
        {
            CourseId = course.Id, Title = "Dashboard task", DueDate = DateTime.Today.AddDays(1)
        });
        var gradeService = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance);
        var grade = await gradeService.CreateGradeAsync(new Grade { CourseId = course.Id, Title = "Final", Score = 50, MaxScore = 100, Weight = 100 });
        await settings.SaveSettingsAsync(new StudentSettings { GpaScale = GpaScale.FivePoint });
        var before = await dashboard.GetSnapshotAsync();
        Assert.Equal(1, before.CourseCount);
        Assert.Equal(1, before.AssignmentCount);
        Assert.Equal(0, before.CompletedCount);
        Assert.Equal(2.5m, before.Gpa);
        Assert.Contains(before.UpcomingDeadlines, assignment => assignment.Id == task.Id);

        task.Status = AssignmentStatus.Completed;
        await assignments.UpdateAssignmentAsync(task);
        var after = await dashboard.GetSnapshotAsync();
        Assert.Equal(1, after.CompletedCount);
        Assert.Equal(100m, after.CompletionPercentage);
        grade.Score = 80;
        await gradeService.UpdateGradeAsync(grade);
        Assert.Equal(4m, (await dashboard.GetSnapshotAsync()).Gpa);
        Assert.Equal(2, changedCount);
    }

    [Fact]
    public void PriorityRecommendationsAreDeterministicExplainableAndExcludeCompleted()
    {
        var today = new DateOnly(2026, 11, 13);
        var assignments = new[]
        {
            new Assignment { Id = 1, Title = "Tomorrow high priority", Priority = AssignmentPriority.High, DueDate = today.AddDays(1).ToDateTime(TimeOnly.MinValue), EstimatedMinutes = 180 },
            new Assignment { Id = 2, Title = "Next month low priority", Priority = AssignmentPriority.Low, DueDate = today.AddDays(30).ToDateTime(TimeOnly.MinValue), EstimatedMinutes = 30 },
            new Assignment { Id = 3, Title = "Overdue", Priority = AssignmentPriority.Low, DueDate = today.AddDays(-3).ToDateTime(TimeOnly.MinValue) },
            new Assignment { Id = 4, Title = "Already done", Status = AssignmentStatus.Completed, DueDate = today.ToDateTime(TimeOnly.MinValue) }
        };
        var service = new AssignmentPriorityService();
        var first = service.Prioritize(assignments, today, 5);
        var second = service.Prioritize(assignments, today, 5);

        Assert.Equal(first, second);
        Assert.Equal("Tomorrow high priority", first[0].Title);
        Assert.Contains("Due tomorrow", first[0].Reason);
        Assert.Contains(first, item => item.Title == "Overdue" && item.Reason.Contains("Overdue by 3 day(s)", StringComparison.Ordinal));
        Assert.DoesNotContain(first, item => item.Title == "Already done");
        Assert.True(AssignmentPriorityService.CalculateScore(assignments[0], today)
            > AssignmentPriorityService.CalculateScore(assignments[1], today));
        var alternatePolicy = AssignmentPriorityService.PrioritizeUsing(assignments, today,
            (assignment, _) => assignment.Id == 2 ? 999d : 0d, 1);
        Assert.Equal(2, Assert.Single(alternatePolicy).AssignmentId);
    }

    [Fact]
    public void PriorityPolicyRejectsNegativeAndNonFiniteWeightsOrScores()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssignmentPriorityService(
            new AssignmentPriorityWeights { HighPriority = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssignmentPriorityService(
            new AssignmentPriorityWeights { FutureBase = double.NaN }));
        var task = new[] { new Assignment { Id = 1, Title = "Task", DueDate = DateTime.Today } };
        Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentPriorityService.PrioritizeUsing(
            task, DateOnly.FromDateTime(DateTime.Today), (_, _) => -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AssignmentPriorityService.PrioritizeUsing(
            task, DateOnly.FromDateTime(DateTime.Today), (_, _) => double.PositiveInfinity));
    }

    private static Course NewCourse(string code = "TEST", int credits = 3) => new()
    {
        Name = "Test Course", Code = code, Instructor = "Test Instructor", Credits = credits
    };

    private sealed class TestDatabase(SqliteConnection connection, IDbContextFactory<AppDbContext> factory) : IAsyncDisposable
    {
        public IDbContextFactory<AppDbContext> Factory { get; } = factory;

        public static async Task<TestDatabase> OpenAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var factory = new TestContextFactory(options);
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            return new(connection, factory);
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class TestContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "StudentPlanner.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class MockSettingsForTests : ISettingsService
    {
        private StudentSettings settings = new();
        public event Action? Changed;
        public Task<StudentSettings> GetSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task<StudentSettings> SaveSettingsAsync(StudentSettings value, CancellationToken cancellationToken = default)
        {
            settings = value;
            Changed?.Invoke();
            return Task.FromResult(settings);
        }
    }
}
