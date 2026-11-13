using StudentPlanner.Models;

namespace StudentPlanner.Services.Interfaces;

public interface IChangeNotifier { event Action? Changed; }
public interface ICourseService : IChangeNotifier
{
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default);
    Task<Course> CreateCourseAsync(Course course, CancellationToken cancellationToken = default);
    Task<Course?> UpdateCourseAsync(Course course, CancellationToken cancellationToken = default);
    Task<bool> DeleteCourseAsync(int id, CancellationToken cancellationToken = default);
}
public interface IAssignmentService : IChangeNotifier
{
    Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<Assignment> CreateAssignmentAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task<Assignment?> UpdateAssignmentAsync(Assignment assignment, CancellationToken cancellationToken = default);
    Task<bool> DeleteAssignmentAsync(int id, CancellationToken cancellationToken = default);
}
public interface IGradeService : IChangeNotifier
{
    Task<IReadOnlyList<Grade>> GetGradesAsync(CancellationToken cancellationToken = default);
    Task<Grade> CreateGradeAsync(Grade grade, CancellationToken cancellationToken = default);
    Task<Grade?> UpdateGradeAsync(Grade grade, CancellationToken cancellationToken = default);
    Task<bool> DeleteGradeAsync(int id, CancellationToken cancellationToken = default);
}
public interface IScheduleService : IChangeNotifier
{
    Task<IReadOnlyList<ScheduleEvent>> GetEventsAsync(CancellationToken cancellationToken = default);
    Task<ScheduleEvent> CreateEventAsync(ScheduleEvent item, CancellationToken cancellationToken = default);
    Task<ScheduleEvent?> UpdateEventAsync(ScheduleEvent item, CancellationToken cancellationToken = default);
    Task<bool> DeleteEventAsync(int id, CancellationToken cancellationToken = default);
}
public interface ISettingsService : IChangeNotifier
{
    Task<StudentSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<StudentSettings> SaveSettingsAsync(StudentSettings settings, CancellationToken cancellationToken = default);
}
public interface ISettingsInitializer { Task InitializeAsync(CancellationToken cancellationToken = default); }
public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
