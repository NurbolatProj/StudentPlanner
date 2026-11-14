using Microsoft.EntityFrameworkCore;
using StudentPlanner.Models;

namespace StudentPlanner.Data;

public sealed class DbInitializer(
    IDbContextFactory<AppDbContext> contextFactory,
    IWebHostEnvironment environment,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (environment.IsDevelopment())
            await SeedDevelopmentDataAsync(db, cancellationToken);
    }

    private async Task SeedDevelopmentDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Students.AnyAsync(cancellationToken)
            || await db.Courses.AnyAsync(cancellationToken)
            || await db.Assignments.AnyAsync(cancellationToken)
            || await db.Grades.AnyAsync(cancellationToken)
            || await db.ScheduleEvents.AnyAsync(cancellationToken))
            return;

        logger.LogInformation("Seeding sample data into the empty development database.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var courses = new[]
        {
            new Course { Name = "Database Management Systems", Code = "DBMS", Instructor = "Pavel Tsoi", Credits = 5, Room = "C1.2.301", Schedule = "Mon / Wed · 14:00", Color = "purple", Description = "Learn database design, SQL and normalization." },
            new Course { Name = "Object Oriented Programming", Code = "OOP", Instructor = "Alex Johnson", Credits = 5, Room = "B2.1.205", Schedule = "Tue / Thu · 10:00", Color = "blue" },
            new Course { Name = "Algorithms and Data Structures", Code = "ADS", Instructor = "Michael Brown", Credits = 5, Room = "A3.1.102", Schedule = "Mon / Fri · 12:00", Color = "green" },
            new Course { Name = "English Language", Code = "ENG", Instructor = "Sarah Williams", Credits = 3, Room = "C2.3.101", Schedule = "Wed / Fri · 09:00", Color = "orange" },
            new Course { Name = "Web Development", Code = "WEB", Instructor = "David Miller", Credits = 4, Room = "D1.2.201", Schedule = "Tue / Thu · 14:00", Color = "pink" },
            new Course { Name = "Mathematics", Code = "MATH", Instructor = "Robert Wilson", Credits = 4, Room = "A1.1.305", Schedule = "Thu · 16:00", Color = "blue", IsActive = false }
        };
        db.Courses.AddRange(courses);
        db.Students.Add(new Student());
        await db.SaveChangesAsync(cancellationToken);

        var today = DateTime.Today;
        db.Assignments.AddRange(
            new Assignment { CourseId = courses[0].Id, CourseName = "DBMS", Title = "Database Normalization", Description = "Complete 1NF, 2NF and 3NF exercises.", DueDate = today.AddDays(2), Priority = AssignmentPriority.High },
            new Assignment { CourseId = courses[1].Id, CourseName = "OOP", Title = "OOP Project Implementation", Description = "Implement the project.", DueDate = today.AddDays(4), Priority = AssignmentPriority.Medium, Status = AssignmentStatus.InProgress },
            new Assignment { CourseId = courses[3].Id, CourseName = "ENG", Title = "English Presentation", DueDate = today.AddDays(6), Priority = AssignmentPriority.Low, Status = AssignmentStatus.Completed, CompletedAt = today },
            new Assignment { CourseId = courses[2].Id, CourseName = "ADS", Title = "Algorithms Assignment", DueDate = today.AddDays(8), Priority = AssignmentPriority.High },
            new Assignment { CourseId = courses[4].Id, CourseName = "WEB", Title = "Web Development Project", DueDate = today.AddDays(10), Priority = AssignmentPriority.High, Status = AssignmentStatus.InProgress });

        db.Grades.AddRange(
            new Grade { CourseId = courses[0].Id, CourseName = "DBMS", Title = "Midterm Exam", Score = 95, MaxScore = 100, Weight = 100, Credits = 5, Date = today.AddDays(-15) },
            new Grade { CourseId = courses[1].Id, CourseName = "OOP", Title = "OOP Project", Score = 88, MaxScore = 100, Weight = 100, Credits = 5, Date = today.AddDays(-12) },
            new Grade { CourseId = courses[3].Id, CourseName = "ENG", Title = "Final Presentation", Score = 92, MaxScore = 100, Weight = 100, Credits = 3, Date = today.AddDays(-10) },
            new Grade { CourseId = courses[2].Id, CourseName = "ADS", Title = "Algorithms Test", Score = 78, MaxScore = 100, Weight = 100, Credits = 5, Date = today.AddDays(-7) },
            new Grade { CourseId = courses[4].Id, CourseName = "WEB", Title = "Frontend Project", Score = 85, MaxScore = 100, Weight = 100, Credits = 4, Date = today.AddDays(-5) },
            new Grade { CourseId = courses[5].Id, CourseName = "MATH", Title = "Final Exam", Score = 68, MaxScore = 100, Weight = 100, Credits = 4, Date = today.AddDays(-2) });

        var eventTitles = new[] { "Database Lecture", "OOP Workshop", "Database Exam", "English Presentation", "Algorithms Lecture", "Project Deadline", "Study Session" };
        var eventTypes = new[] { ScheduleEventType.Class, ScheduleEventType.Class, ScheduleEventType.Exam, ScheduleEventType.Class, ScheduleEventType.Class, ScheduleEventType.Assignment, ScheduleEventType.Personal };
        for (var day = 0; day < eventTitles.Length; day++)
        {
            var date = today.AddDays(day);
            db.ScheduleEvents.Add(new ScheduleEvent
            {
                CourseId = day == 6 ? null : courses[day % 6].Id,
                CourseName = day == 6 ? "Personal" : courses[day % 6].Code,
                Title = eventTitles[day],
                Description = "Planned study activity.",
                StartTime = date.AddHours(9),
                EndTime = date.AddHours(10),
                Type = eventTypes[day]
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

