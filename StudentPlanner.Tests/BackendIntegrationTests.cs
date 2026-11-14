using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Business;
using StudentPlanner.Services;
using StudentPlanner.Services.Database;
using StudentPlanner.Services.Interfaces;
using Xunit;

namespace StudentPlanner.Tests;

public sealed class BackendIntegrationTests
{
    private static readonly ICurrentUserService TestUser = new FixedCurrentUserService("integration-user");

    [Fact]
    public void DataProtectionKeyPathIsStableAndCreatesAnIsolatedWritableDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"studentplanner-dp-{Guid.NewGuid():N}");
        try
        {
            var resolved = DataProtectionKeyPath.Resolve(directory);
            Assert.Equal(Path.GetFullPath(directory), resolved);
            Assert.True(Directory.Exists(resolved));
            Assert.Empty(Directory.GetFiles(resolved));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task HttpAuthenticationSupportsRegistrationLoginAntiforgeryAndLogout()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"studentplanner-auth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "accounts.db");
        try
        {
            using var app = new TestApplicationFactory(path);
            using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var anonymous = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
            Assert.Contains("/login", anonymous.Headers.Location?.ToString());
            var anonymousAccount = await client.GetAsync("/account");
            Assert.Equal(HttpStatusCode.Redirect, anonymousAccount.StatusCode);
            foreach (var route in new[] { "/focus", "/insights" })
            {
                var anonymousFeature = await client.GetAsync(route);
                Assert.Equal(HttpStatusCode.Redirect, anonymousFeature.StatusCode);
                Assert.Contains("/login", anonymousFeature.Headers.Location?.ToString());
            }

            var registerPage = await client.GetAsync("/register");
            Assert.Equal(HttpStatusCode.OK, registerPage.StatusCode);
            Assert.True(File.Exists(path), "The HTTP test host must use its disposable SQLite path.");
            var email = $"http-student-{Guid.NewGuid():N}@example.test";
            var registerToken = GetAntiforgeryToken(await registerPage.Content.ReadAsStringAsync());
            var registration = await client.PostAsync("/register", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = registerToken,
                ["Input.FullName"] = "HTTP Test Student",
                ["Input.Email"] = email,
                ["Input.Password"] = "SecurePassword!9",
                ["Input.ConfirmPassword"] = "SecurePassword!9"
            }));
            Assert.True(registration.StatusCode == HttpStatusCode.Redirect, await registration.Content.ReadAsStringAsync());
            Assert.True(Directory.Exists(Path.Combine(directory, "keys")), "Identity Data Protection keys are persisted in the disposable test directory.");
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(directory, "keys"), "*.xml"));
            Assert.Equal("/", registration.Headers.Location?.ToString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

            var focusPage = await client.GetAsync("/focus");
            var focusHtml = await focusPage.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, focusPage.StatusCode);
            Assert.Contains("Focus Lab", focusHtml);
            Assert.Matches("<a(?=[^>]*href=\"/focus\")(?=[^>]*aria-current=\"page\")[^>]*>", focusHtml);
            var insightsPage = await client.GetAsync("/insights");
            var insightsHtml = await insightsPage.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, insightsPage.StatusCode);
            Assert.Contains("Study Insights", insightsHtml);
            Assert.Matches("<a(?=[^>]*href=\"/insights\")(?=[^>]*aria-current=\"page\")[^>]*>", insightsHtml);

            var logoutPage = await client.GetAsync("/logout");
            Assert.Equal(HttpStatusCode.OK, logoutPage.StatusCode);
            var logoutToken = GetAntiforgeryToken(await logoutPage.Content.ReadAsStringAsync());
            var logout = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = logoutToken
            }));
            Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
            Assert.Equal("/login", logout.Headers.Location?.ToString());
            var afterLogout = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
            Assert.Contains("/login", afterLogout.Headers.Location?.ToString());

            var loginPage = await client.GetAsync("/login");
            var loginToken = GetAntiforgeryToken(await loginPage.Content.ReadAsStringAsync());
            var invalidLogin = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = loginToken,
                ["Input.Email"] = email,
                ["Input.Password"] = "WrongPassword!9"
            }));
            Assert.Equal(HttpStatusCode.OK, invalidLogin.StatusCode);
            Assert.Contains("Invalid sign-in attempt.", await invalidLogin.Content.ReadAsStringAsync());
            var login = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = loginToken,
                ["Input.Email"] = email,
                ["Input.Password"] = "SecurePassword!9",
                ["Input.RememberMe"] = "true"
            }));
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/", login.Headers.Location?.ToString());
            var cookie = string.Join(";", login.Headers.GetValues("Set-Cookie"));
            Assert.Contains("StudentPlanner.Auth", cookie);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);

            var identityCookie = login.Headers.GetValues("Set-Cookie")
                .First(value => value.StartsWith("StudentPlanner.Auth=", StringComparison.OrdinalIgnoreCase))
                .Split(';', 2)[0];
            client.Dispose();
            app.Dispose();

            using var restartedApp = new TestApplicationFactory(path);
            using var restartedClient = restartedApp.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            restartedClient.DefaultRequestHeaders.Add("Cookie", identityCookie);
            var restoredSession = await restartedClient.GetAsync("/account");
            Assert.Equal(HttpStatusCode.OK, restoredSession.StatusCode);
            Assert.Contains(email, await restoredSession.Content.ReadAsStringAsync());

            using var lockoutClient = restartedApp.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
            var lockoutPage = await lockoutClient.GetAsync("/login");
            var lockoutToken = GetAntiforgeryToken(await lockoutPage.Content.ReadAsStringAsync());
            HttpResponseMessage? lastFailure = null;
            for (var attempt = 0; attempt < 5; attempt++)
                lastFailure = await lockoutClient.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = lockoutToken,
                    ["Input.Email"] = email,
                    ["Input.Password"] = "WrongPassword!9"
                }));
            Assert.NotNull(lastFailure);
            Assert.Contains("temporarily locked", await lastFailure.Content.ReadAsStringAsync());
        }
        finally
        {
            // The application host may retain SQLite connections until pool cleanup.
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static string GetAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, "The server rendered an antiforgery token for the form.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [Fact]
    public async Task IdentityRegistrationHashesPasswordsAndRejectsDuplicatesAndWeakPasswords()
    {
        await using var database = await TestDatabase.OpenAsync();
        using var provider = BuildIdentityProvider(database.Options);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "new@example.test", Email = "new@example.test", FullName = "New Student" };
        var created = await users.CreateAsync(user, "SecurePassword!9");
        Assert.True(created.Succeeded);
        Assert.NotEqual("SecurePassword!9", user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash!, "SecurePassword!9"));
        Assert.False((await users.CreateAsync(new ApplicationUser { UserName = "new@example.test", Email = "new@example.test", FullName = "Duplicate" }, "SecurePassword!9")).Succeeded);
        Assert.False((await users.CreateAsync(new ApplicationUser { UserName = "weak@example.test", Email = "weak@example.test", FullName = "Weak" }, "weak")).Succeeded);
    }

    [Fact]
    public async Task AccountSettingsAreStoredAndReadSeparatelyForEachUser()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            db.Users.Add(NewIdentityUser("user-b"));
            await db.SaveChangesAsync();
        }
        using var provider = BuildIdentityProvider(database.Options);
        using var scope = provider.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var settingsA = new IdentitySettingsService(database.Factory, TestUser, users);
        var settingsB = new IdentitySettingsService(database.Factory, new FixedCurrentUserService("user-b"), users);
        var a = await settingsA.GetSettingsAsync();
        a.Theme = ThemePreference.Light;
        a.GpaScale = GpaScale.FivePoint;
        await settingsA.SaveSettingsAsync(a);
        var identityUserA = await users.FindByIdAsync("integration-user");
        Assert.NotNull(identityUserA);
        identityUserA!.FullName = "Updated Integration Name";
        Assert.True((await users.UpdateAsync(identityUserA)).Succeeded);
        var b = await settingsB.GetSettingsAsync();
        b.Theme = ThemePreference.Dark;
        b.GpaScale = GpaScale.FourPoint;
        await settingsB.SaveSettingsAsync(b);
        Assert.Equal(ThemePreference.Light, (await settingsA.GetSettingsAsync()).Theme);
        Assert.Equal(GpaScale.FivePoint, (await settingsA.GetSettingsAsync()).GpaScale);
        Assert.Equal("Updated Integration Name", (await settingsA.GetSettingsAsync()).FullName);
        Assert.Equal(ThemePreference.Dark, (await settingsB.GetSettingsAsync()).Theme);
        Assert.Equal(GpaScale.FourPoint, (await settingsB.GetSettingsAsync()).GpaScale);
    }

    [Fact]
    public async Task AnonymousCurrentUserIsRejected()
    {
        var currentUser = new CurrentUserService(new FixedAuthenticationStateProvider(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => currentUser.GetRequiredUserIdAsync());
    }

    [Fact]
    public async Task UserOwnedRecordsCannotBeReadModifiedDeletedOrLinkedAcrossAccounts()
    {
        await using var database = await TestDatabase.OpenAsync();
        await using (var db = await database.Factory.CreateDbContextAsync())
        {
            db.Users.Add(NewIdentityUser("user-b"));
            await db.SaveChangesAsync();
        }

        var userA = TestUser;
        var userB = new FixedCurrentUserService("user-b");
        var coursesA = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, userA);
        var assignmentsA = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, userA);
        var gradesA = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance, userA);
        var scheduleA = new EfScheduleService(database.Factory, NullLogger<EfScheduleService>.Instance, userA);
        var course = await coursesA.CreateCourseAsync(NewCourse("SHARED"));
        var assignment = await assignmentsA.CreateAssignmentAsync(new Assignment { CourseId = course.Id, Title = "User A task" });
        var grade = await gradesA.CreateGradeAsync(new Grade { CourseId = course.Id, Title = "User A grade", Score = 90, MaxScore = 100, Weight = 100 });
        var calendarEvent = await scheduleA.CreateEventAsync(new ScheduleEvent
        {
            CourseId = course.Id, Title = "User A lecture", StartTime = DateTime.Today.AddHours(12), EndTime = DateTime.Today.AddHours(13)
        });

        var coursesB = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, userB);
        var assignmentsB = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, userB);
        var gradesB = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance, userB);
        var scheduleB = new EfScheduleService(database.Factory, NullLogger<EfScheduleService>.Instance, userB);
        Assert.Empty(await coursesB.GetCoursesAsync());
        var forgedCourse = NewCourse("SHARED");
        forgedCourse.OwnerId = "integration-user";
        var courseB = await coursesB.CreateCourseAsync(forgedCourse);
        Assert.NotEqual(course.Id, courseB.Id);
        Assert.Equal("user-b", courseB.OwnerId);
        Assert.Equal(courseB.Id, Assert.Single(await coursesB.GetCoursesAsync()).Id);
        Assert.Empty(await assignmentsB.GetAssignmentsAsync());
        Assert.Empty(await gradesB.GetGradesAsync());
        Assert.Empty(await scheduleB.GetEventsAsync());
        Assert.Null(await assignmentsB.UpdateAssignmentAsync(new Assignment { Id = assignment.Id, CourseId = course.Id, Title = "Tampered task" }));
        Assert.False(await gradesB.DeleteGradeAsync(grade.Id));
        Assert.False(await scheduleB.DeleteEventAsync(calendarEvent.Id));
        Assert.False(await coursesB.DeleteCourseAsync(course.Id));
        Assert.False(await assignmentsB.DeleteAssignmentAsync(assignment.Id));
        await Assert.ThrowsAsync<ValidationException>(() => assignmentsB.CreateAssignmentAsync(new Assignment { CourseId = course.Id, Title = "Cross-owner link" }));
        await Assert.ThrowsAsync<ValidationException>(() => gradesB.CreateGradeAsync(new Grade { CourseId = course.Id, Title = "Cross-owner grade", Score = 90, MaxScore = 100, Weight = 100 }));
        await Assert.ThrowsAsync<ValidationException>(() => scheduleB.CreateEventAsync(new ScheduleEvent
        {
            CourseId = course.Id, Title = "Cross-owner event", StartTime = DateTime.Today.AddHours(15), EndTime = DateTime.Today.AddHours(16)
        }));

        var dashboardB = new EfDashboardService(database.Factory, new MockSettingsForTests(), userB);
        var snapshot = await dashboardB.GetSnapshotAsync();
        Assert.Equal(1, snapshot.CourseCount);
        Assert.Equal(0, snapshot.AssignmentCount);
        Assert.Empty(snapshot.UpcomingDeadlines);
    }

    [Fact]
    public async Task MigrationsCreateRelationalSchemaAndServicesPersistAcrossContexts()
    {
        await using var database = await TestDatabase.OpenAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, TestUser);
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
        Assert.Contains("20261113141314_AddIdentityAndUserOwnership", await freshContext.Database.GetAppliedMigrationsAsync());
        Assert.Throws<SqliteException>(() => freshContext.Database.ExecuteSqlRaw(
            "INSERT INTO Assignments (CourseId, Title, Description, DueDate, Priority, Status, EstimatedMinutes, CompletedAt) VALUES (999, 'Bad FK', '', '2026-11-14 00:00:00', 1, 0, 60, NULL)"));
    }

    [Fact]
    public async Task AddIdentityMigrationPreservesLegacyRecordsAsUnowned()
    {
        await using var database = await TestDatabase.OpenLegacyAsync();
        await using (var beforeMigration = await database.Factory.CreateDbContextAsync())
        {
            await beforeMigration.Database.ExecuteSqlRawAsync("INSERT INTO Courses (Name, Code, Instructor, Credits, Color, IsActive, Description, Room, Schedule) VALUES ('Legacy Course', 'LEGACY', 'Instructor', 3, 'purple', 1, '', '', '')");
            await beforeMigration.Database.ExecuteSqlRawAsync("INSERT INTO Assignments (CourseId, Title, Description, DueDate, Priority, Status, EstimatedMinutes, CompletedAt) VALUES (1, 'Legacy assignment', '', '2026-11-20 00:00:00', 1, 0, 60, NULL)");
            await beforeMigration.Database.ExecuteSqlRawAsync("INSERT INTO Grades (CourseId, Title, Score, MaxScore, Weight, Date) VALUES (1, 'Legacy grade', '88', '100', '100', '2026-11-13 00:00:00')");
            await beforeMigration.Database.ExecuteSqlRawAsync("INSERT INTO ScheduleEvents (CourseId, Title, Description, StartTime, EndTime, Location, Type) VALUES (1, 'Legacy event', '', '2026-11-13 08:00:00', '2026-11-13 09:00:00', '', 0)");
        }
        await using (var migrated = await database.Factory.CreateDbContextAsync())
            await migrated.Database.MigrateAsync();
        await using var verify = await database.Factory.CreateDbContextAsync();
        Assert.Null((await verify.Courses.SingleAsync()).OwnerId);
        Assert.Null((await verify.Assignments.SingleAsync()).OwnerId);
        Assert.Null((await verify.Grades.SingleAsync()).OwnerId);
        Assert.Null((await verify.ScheduleEvents.SingleAsync()).OwnerId);
        Assert.Equal("LEGACY", (await verify.Assignments.Include(item => item.Course).SingleAsync()).Course!.Code);
        verify.Users.Add(NewIdentityUser("new-user"));
        await verify.SaveChangesAsync();
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, new FixedCurrentUserService("new-user"));
        Assert.Empty(await courses.GetCoursesAsync());
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
                first.Users.Add(NewIdentityUser("integration-user"));
                await first.SaveChangesAsync();
            }

            var firstCourses = new EfCourseService(firstFactory, NullLogger<EfCourseService>.Instance, TestUser);
            var firstAssignments = new EfAssignmentService(firstFactory, NullLogger<EfAssignmentService>.Instance, TestUser);
            var firstGrades = new EfGradeService(firstFactory, NullLogger<EfGradeService>.Instance, TestUser);
            var firstSchedule = new EfScheduleService(firstFactory, NullLogger<EfScheduleService>.Instance, TestUser);
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
            var restartedCourses = new EfCourseService(restartedFactory, NullLogger<EfCourseService>.Instance, TestUser);
            var restartedAssignments = new EfAssignmentService(restartedFactory, NullLogger<EfAssignmentService>.Instance, TestUser);
            var restartedGrades = new EfGradeService(restartedFactory, NullLogger<EfGradeService>.Instance, TestUser);
            var restartedSchedule = new EfScheduleService(restartedFactory, NullLogger<EfScheduleService>.Instance, TestUser);
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
            var finalAssignments = new EfAssignmentService(finalFactory, NullLogger<EfAssignmentService>.Instance, TestUser);
            var finalGrades = new EfGradeService(finalFactory, NullLogger<EfGradeService>.Instance, TestUser);
            var finalSchedule = new EfScheduleService(finalFactory, NullLogger<EfScheduleService>.Instance, TestUser);
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
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, TestUser);
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
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, TestUser);
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
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var grades = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance, TestUser);
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
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var schedule = new EfScheduleService(database.Factory, NullLogger<EfScheduleService>.Instance, TestUser);
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
        var courses = new EfCourseService(database.Factory, NullLogger<EfCourseService>.Instance, TestUser);
        var assignments = new EfAssignmentService(database.Factory, NullLogger<EfAssignmentService>.Instance, TestUser);
        var grades = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance, TestUser);
        var settings = new MockSettingsForTests();
        var dashboard = new EfDashboardService(database.Factory, settings, TestUser);
        var changedCount = 0;
        assignments.Changed += () => changedCount++;

        var course = await courses.CreateCourseAsync(NewCourse());
        var task = await assignments.CreateAssignmentAsync(new Assignment
        {
            CourseId = course.Id, Title = "Dashboard task", DueDate = DateTime.Today.AddDays(1)
        });
        var gradeService = new EfGradeService(database.Factory, NullLogger<EfGradeService>.Instance, TestUser);
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

    private static ServiceProvider BuildIdentityProvider(DbContextOptions<AppDbContext> options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
        })
            .AddEntityFrameworkStores<AppDbContext>();
        return services.BuildServiceProvider();
    }

    private sealed class TestApplicationFactory(string databasePath) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:Path", databasePath);
            builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetDirectoryName(databasePath)!, "keys"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = databasePath,
                ["Database:Pooling"] = "false",
                ["DataProtection:KeysPath"] = Path.Combine(Path.GetDirectoryName(databasePath)!, "keys"),
                ["Database:ApplyMigrationsOnStartup"] = "true"
            }));
        }
    }

    private static ApplicationUser NewIdentityUser(string id) => new()
    {
        Id = id, UserName = $"{id}@example.test", NormalizedUserName = $"{id}@example.test".ToUpperInvariant(),
        Email = $"{id}@example.test", NormalizedEmail = $"{id}@example.test".ToUpperInvariant(), FullName = "Integration User"
    };

    private sealed class FixedCurrentUserService(string userId) : ICurrentUserService
    {
        public Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(userId);
    }

    private sealed class FixedAuthenticationStateProvider(System.Security.Claims.ClaimsPrincipal principal) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(principal));
    }

    private sealed class TestDatabase(SqliteConnection connection, IDbContextFactory<AppDbContext> factory) : IAsyncDisposable
    {
        public IDbContextFactory<AppDbContext> Factory { get; } = factory;
        public DbContextOptions<AppDbContext> Options { get; private init; } = null!;

        public static async Task<TestDatabase> OpenAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var factory = new TestContextFactory(options);
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
            db.Users.Add(NewIdentityUser("integration-user"));
            await db.SaveChangesAsync();
            return new(connection, factory) { Options = options };
        }

        public static async Task<TestDatabase> OpenLegacyAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var factory = new TestContextFactory(options);
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.MigrateAsync("20261113112016_InitialCreate");
            return new(connection, factory) { Options = options };
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
