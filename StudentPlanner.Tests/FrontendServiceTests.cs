using System.ComponentModel.DataAnnotations;
using StudentPlanner.Models;
using StudentPlanner.Services.Mock;
using Xunit;

namespace StudentPlanner.Tests;

public sealed class FrontendServiceTests
{
    [Fact]
    public void GpaUsesAssessmentWeightsAndCourseCredits()
    {
        var courses = new[] { new Course { Id = 1, Credits = 2 }, new Course { Id = 2, Credits = 1 } };
        var grades = new[] {
            new Grade { CourseId=1, Score=80, Weight=60 }, new Grade { CourseId=1, Score=100, Weight=40 },
            new Grade { CourseId=2, Score=100, Weight=100 }
        };

        Assert.Equal(3.68m, GpaCalculator.Calculate(grades, courses, GpaScale.FourPoint), 2);
    }

    [Theory]
    [InlineData(50, GpaScale.FourPoint, 2)]
    [InlineData(50, GpaScale.FivePoint, 2.5)]
    [InlineData(50, GpaScale.HundredPoint, 50)]
    public void GpaScaleConversionIsConsistent(decimal percentage, GpaScale scale, decimal expected) =>
        Assert.Equal(expected, GpaCalculator.ConvertPercentage(percentage, scale));

    [Fact]
    public async Task AssignmentCrudReturnsDetachedSharedRecords()
    {
        var courseService = new MockCourseService();
        var service = new MockAssignmentService(courseService);
        var created = await service.CreateAssignmentAsync(new Assignment { CourseId=1, Title="Test task", CourseName="DBMS" });
        created.Status = AssignmentStatus.Completed;
        await service.UpdateAssignmentAsync(created);

        Assert.Contains(await service.GetAssignmentsAsync(), x => x.Id == created.Id && x.Status == AssignmentStatus.Completed);
        Assert.True(await service.DeleteAssignmentAsync(created.Id));
        Assert.DoesNotContain(await service.GetAssignmentsAsync(), x => x.Id == created.Id);
    }

    [Fact]
    public async Task AssignmentCreationNeedsCourseIdButNoCourseDisplayProperty()
    {
        var courses = new MockCourseService();
        var service = new MockAssignmentService(courses);
        var created = await service.CreateAssignmentAsync(new Assignment { CourseId = 1, Title = "CourseId only assignment" });

        Assert.Equal(1, created.CourseId);
        Assert.Equal("DBMS", created.CourseName);
    }

    [Fact]
    public async Task GradeDeleteRemovesGrade()
    {
        var service = new MockGradeService(new MockCourseService());
        var grade = (await service.GetGradesAsync()).First();
        Assert.True(await service.DeleteGradeAsync(grade.Id));
        Assert.DoesNotContain(await service.GetGradesAsync(), x => x.Id == grade.Id);
    }

    [Fact]
    public async Task DashboardSnapshotUsesSharedServiceState()
    {
        var courses = new MockCourseService();
        var assignments = new MockAssignmentService(courses);
        var grades = new MockGradeService(courses);
        var schedule = new MockScheduleService(courses);
        var settings = new MockSettingsService();
        var dashboard = new MockDashboardService(courses, assignments, grades, schedule, settings);
        var before = await dashboard.GetSnapshotAsync();
        await assignments.CreateAssignmentAsync(new Assignment { CourseId=1, Title="New shared assignment", DueDate=DateTime.Today.AddDays(1) });
        var after = await dashboard.GetSnapshotAsync();
        Assert.Equal(before.AssignmentCount+1, after.AssignmentCount);
        Assert.Contains(after.UpcomingDeadlines, a => a.Title == "New shared assignment");
    }

    [Fact]
    public async Task AssignmentRejectsUnknownCourseRelationship()
    {
        var service = new MockAssignmentService(new MockCourseService());
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAssignmentAsync(new Assignment { CourseId=999, Title="Invalid relation" }));
    }

    [Fact]
    public async Task InvalidAssignmentDoesNotMutateSharedState()
    {
        var service = new MockAssignmentService(new MockCourseService());
        var before = await service.GetAssignmentsAsync();

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAssignmentAsync(
            new Assignment { CourseId = 999, Title = "Invalid course relationship" }));

        Assert.Equal(before.Count, (await service.GetAssignmentsAsync()).Count);
    }

    [Fact]
    public async Task AssignmentEditUpdatesExistingRecord()
    {
        var service = new MockAssignmentService(new MockCourseService());
        var original = (await service.GetAssignmentsAsync()).First();
        original.Title = "Updated title";
        original.CourseId = 2;

        var updated = await service.UpdateAssignmentAsync(original);

        Assert.NotNull(updated);
        Assert.Equal("Updated title", updated!.Title);
        Assert.Equal(2, updated.CourseId);
        Assert.Equal("OOP", updated.CourseName);
        Assert.Equal(1, (await service.GetAssignmentsAsync()).Count(x => x.Id == original.Id));
    }

    [Fact]
    public async Task ScheduleRejectsEndBeforeStart()
    {
        var service = new MockScheduleService();
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateEventAsync(new ScheduleEvent { Title="Invalid event", StartTime=DateTime.Today.AddHours(11), EndTime=DateTime.Today.AddHours(10) }));
    }

    [Fact]
    public async Task SettingsServiceReturnsPersistedCopy()
    {
        var service = new MockSettingsService();
        await service.SaveSettingsAsync(new StudentSettings { FullName="Test Student", Email="student@school.edu", GpaScale=GpaScale.FivePoint });
        var actual = await service.GetSettingsAsync();
        Assert.Equal("Test Student", actual.FullName);
        Assert.Equal(GpaScale.FivePoint, actual.GpaScale);
    }

    [Fact]
    public async Task OtherCreateFormsValidateForeignKeysWithoutRequiredNavigationAliases()
    {
        var courses = new MockCourseService();
        var gradeService = new MockGradeService(courses);
        var grade = await gradeService.CreateGradeAsync(new Grade { CourseId = 1, Title = "Midterm", Score = 80, MaxScore = 100, Weight = 40 });
        Assert.Equal("DBMS", grade.CourseName);

        var schedule = new MockScheduleService(courses);
        var item = await schedule.CreateEventAsync(new ScheduleEvent {
            CourseId = 1, Title = "Review session", StartTime = DateTime.Today.AddHours(11), EndTime = DateTime.Today.AddHours(12)
        });
        Assert.Equal(1, item.CourseId);

        var course = await courses.CreateCourseAsync(new Course { Name = "New Course", Code = "NEW", Instructor = "Instructor", Credits = 3 });
        Assert.True(course.Id > 0);
    }

    [Theory]
    [InlineData("student@school.edu", true)]
    [InlineData("not-an-email", false)]
    [InlineData("x@", false)]
    public void StudentEmailUsesEmailAddressValidation(string email, bool valid)
    {
        var student = new Student { FullName="Test Student", Email=email };
        var results = new List<ValidationResult>();
        Assert.Equal(valid, Validator.TryValidateObject(student, new ValidationContext(student), results, true));
    }
}
