using StudentPlanner.Models;
using StudentPlanner.Services.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace StudentPlanner.Services.Mock;

// These collections are instance scoped by DI, never static, and return detached copies to callers.
public sealed class MockCourseService : ICourseService
{
    private readonly object gate = new();
    private readonly List<Course> data = new() {
        new() { Id=1, Name="Database Management Systems", Code="DBMS", Instructor="Pavel Tsoi", Credits=5, Room="C1.2.301", Schedule="Mon / Wed · 14:00", Color="purple", Description="Learn database design, SQL and normalization." },
        new() { Id=2, Name="Object Oriented Programming", Code="OOP", Instructor="Alex Johnson", Credits=5, Room="B2.1.205", Schedule="Tue / Thu · 10:00", Color="blue" },
        new() { Id=3, Name="Algorithms and Data Structures", Code="ADS", Instructor="Michael Brown", Credits=5, Room="A3.1.102", Schedule="Mon / Fri · 12:00", Color="green" },
        new() { Id=4, Name="English Language", Code="ENG", Instructor="Sarah Williams", Credits=3, Room="C2.3.101", Schedule="Wed / Fri · 09:00", Color="orange" },
        new() { Id=5, Name="Web Development", Code="WEB", Instructor="David Miller", Credits=4, Room="D1.2.201", Schedule="Tue / Thu · 14:00", Color="pink" },
        new() { Id=6, Name="Mathematics", Code="MATH", Instructor="Robert Wilson", Credits=4, Room="A1.1.305", Schedule="Thu · 16:00", Color="blue", IsActive=false }
    };
    public event Action? Changed;
    public Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken ct=default) { ct.ThrowIfCancellationRequested(); lock(gate) return Task.FromResult<IReadOnlyList<Course>>(data.Select(Clone).ToArray()); }
    public Task<Course> CreateCourseAsync(Course value, CancellationToken ct=default) { ct.ThrowIfCancellationRequested(); Validator.ValidateObject(value,new ValidationContext(value),true); lock(gate) { value=Clone(value); value.Id=NextId(); data.Add(value); } Changed?.Invoke(); return Task.FromResult(Clone(value)); }
    public Task<Course?> UpdateCourseAsync(Course value, CancellationToken ct=default) { ct.ThrowIfCancellationRequested(); Validator.ValidateObject(value,new ValidationContext(value),true); lock(gate) { var i=data.FindIndex(x=>x.Id==value.Id); if(i<0)return Task.FromResult<Course?>(null); data[i]=Clone(value); } Changed?.Invoke(); return Task.FromResult<Course?>(Clone(value)); }
    public Task<bool> DeleteCourseAsync(int id, CancellationToken ct=default) { ct.ThrowIfCancellationRequested(); bool removed; lock(gate) { removed=data.RemoveAll(x=>x.Id==id)>0; } if(removed)Changed?.Invoke(); return Task.FromResult(removed); }
    private int NextId()=>data.Count==0?1:data.Max(x=>x.Id)+1;
    private static Course Clone(Course x)=>new(){Id=x.Id,Name=x.Name,Code=x.Code,Instructor=x.Instructor,Credits=x.Credits,Color=x.Color,IsActive=x.IsActive,Description=x.Description,Room=x.Room,Schedule=x.Schedule};
}

public sealed class MockAssignmentService(ICourseService courses) : IAssignmentService
{
    private readonly object gate=new();
    private readonly List<Assignment> data=new(){
        new(){Id=1,CourseId=1,CourseName="DBMS",Title="Database Normalization",Description="Complete 1NF, 2NF and 3NF exercises.",DueDate=DateTime.Today.AddDays(2),Priority=AssignmentPriority.High},
        new(){Id=2,CourseId=2,CourseName="OOP",Title="OOP Project Implementation",Description="Implement the project.",DueDate=DateTime.Today.AddDays(4),Priority=AssignmentPriority.Medium,Status=AssignmentStatus.InProgress},
        new(){Id=3,CourseId=4,CourseName="ENG",Title="English Presentation",DueDate=DateTime.Today.AddDays(6),Priority=AssignmentPriority.Low,Status=AssignmentStatus.Completed,CompletedAt=DateTime.Today},
        new(){Id=4,CourseId=3,CourseName="ADS",Title="Algorithms Assignment",DueDate=DateTime.Today.AddDays(8),Priority=AssignmentPriority.High},
        new(){Id=5,CourseId=5,CourseName="WEB",Title="Web Development Project",DueDate=DateTime.Today.AddDays(10),Priority=AssignmentPriority.High,Status=AssignmentStatus.InProgress}
    };
    public event Action? Changed;
    public Task<IReadOnlyList<Assignment>> GetAssignmentsAsync(CancellationToken ct=default){ct.ThrowIfCancellationRequested(); lock(gate)return Task.FromResult<IReadOnlyList<Assignment>>(data.Select(Clone).ToArray());}
    public async Task<Assignment> CreateAssignmentAsync(Assignment x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);var course=(await courses.GetCoursesAsync(ct)).FirstOrDefault(c=>c.Id==x.CourseId)??throw new ValidationException("A valid course is required.");lock(gate){x=Clone(x);x.CourseName=course.Code;x.Id=data.Count==0?1:data.Max(a=>a.Id)+1;if(x.Status==AssignmentStatus.Completed)x.CompletedAt=DateTime.Now;data.Add(x);}Changed?.Invoke();return Clone(x);}
    public async Task<Assignment?> UpdateAssignmentAsync(Assignment x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);var course=(await courses.GetCoursesAsync(ct)).FirstOrDefault(c=>c.Id==x.CourseId)??throw new ValidationException("A valid course is required.");lock(gate){var i=data.FindIndex(a=>a.Id==x.Id);if(i<0)return null;x=Clone(x);x.CourseName=course.Code;x.CompletedAt=x.Status==AssignmentStatus.Completed?(data[i].CompletedAt??DateTime.Now):null;data[i]=x;}Changed?.Invoke();return Clone(x);}
    public Task<bool> DeleteAssignmentAsync(int id,CancellationToken ct=default){ct.ThrowIfCancellationRequested();bool ok;lock(gate)ok=data.RemoveAll(x=>x.Id==id)>0;if(ok)Changed?.Invoke();return Task.FromResult(ok);}
    private static Assignment Clone(Assignment x)=>new(){Id=x.Id,CourseId=x.CourseId,CourseName=x.CourseName,Title=x.Title,Description=x.Description,DueDate=x.DueDate,Priority=x.Priority,Status=x.Status,EstimatedMinutes=x.EstimatedMinutes,CompletedAt=x.CompletedAt};
}

public sealed class MockGradeService(ICourseService courses) : IGradeService
{
    private readonly object gate=new();
    private readonly List<Grade> data=new(){
        new(){Id=1,CourseId=1,CourseName="DBMS",Title="Midterm Exam",Score=95,MaxScore=100,Weight=100,Credits=5,Date=DateTime.Today.AddDays(-15)},
        new(){Id=2,CourseId=2,CourseName="OOP",Title="OOP Project",Score=88,MaxScore=100,Weight=100,Credits=5,Date=DateTime.Today.AddDays(-12)},
        new(){Id=3,CourseId=4,CourseName="ENG",Title="Final Presentation",Score=92,MaxScore=100,Weight=100,Credits=3,Date=DateTime.Today.AddDays(-10)},
        new(){Id=4,CourseId=3,CourseName="ADS",Title="Algorithms Test",Score=78,MaxScore=100,Weight=100,Credits=5,Date=DateTime.Today.AddDays(-7)},
        new(){Id=5,CourseId=5,CourseName="WEB",Title="Frontend Project",Score=85,MaxScore=100,Weight=100,Credits=4,Date=DateTime.Today.AddDays(-5)},
        new(){Id=6,CourseId=6,CourseName="MATH",Title="Final Exam",Score=68,MaxScore=100,Weight=100,Credits=4,Date=DateTime.Today.AddDays(-2)}
    };
    public event Action? Changed;
    public Task<IReadOnlyList<Grade>> GetGradesAsync(CancellationToken ct=default){ct.ThrowIfCancellationRequested();lock(gate)return Task.FromResult<IReadOnlyList<Grade>>(data.Select(Clone).ToArray());}
    public async Task<Grade> CreateGradeAsync(Grade x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);var course=(await courses.GetCoursesAsync(ct)).FirstOrDefault(c=>c.Id==x.CourseId)??throw new ValidationException("A valid course is required.");lock(gate){x=Clone(x);x.CourseName=course.Code;x.Credits=course.Credits;x.Id=data.Count==0?1:data.Max(g=>g.Id)+1;data.Add(x);}Changed?.Invoke();return Clone(x);}
    public async Task<Grade?> UpdateGradeAsync(Grade x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);var course=(await courses.GetCoursesAsync(ct)).FirstOrDefault(c=>c.Id==x.CourseId)??throw new ValidationException("A valid course is required.");lock(gate){var i=data.FindIndex(g=>g.Id==x.Id);if(i<0)return null;x=Clone(x);x.CourseName=course.Code;x.Credits=course.Credits;data[i]=x;}Changed?.Invoke();return Clone(x);}
    public Task<bool> DeleteGradeAsync(int id,CancellationToken ct=default){ct.ThrowIfCancellationRequested();bool ok;lock(gate)ok=data.RemoveAll(x=>x.Id==id)>0;if(ok)Changed?.Invoke();return Task.FromResult(ok);}
    private static Grade Clone(Grade x)=>new(){Id=x.Id,CourseId=x.CourseId,CourseName=x.CourseName,Title=x.Title,Score=x.Score,MaxScore=x.MaxScore,Weight=x.Weight,Date=x.Date,Credits=x.Credits};
}

public sealed class MockScheduleService(ICourseService? courses = null) : IScheduleService
{
    private readonly object gate=new();
    private readonly List<ScheduleEvent> data=Enumerable.Range(1,7).Select(i=>new ScheduleEvent{Id=i,CourseId=i%6+1,CourseName=new[]{"DBMS","OOP","ADS","ENG","WEB","MATH"}[i%6],Title=new[]{"Database Lecture","OOP Workshop","Database Exam","English Presentation","Algorithms Lecture","Project Deadline","Study Session"}[i-1],Description="Planned study activity.",StartTime=DateTime.Today.AddDays(i==1?0:i).AddHours(9),EndTime=DateTime.Today.AddDays(i==1?0:i).AddHours(10),Type=i==3?ScheduleEventType.Exam:i==6?ScheduleEventType.Assignment:ScheduleEventType.Class}).ToList();
    public event Action? Changed;
    public Task<IReadOnlyList<ScheduleEvent>> GetEventsAsync(CancellationToken ct=default){ct.ThrowIfCancellationRequested();lock(gate)return Task.FromResult<IReadOnlyList<ScheduleEvent>>(data.Select(Clone).ToArray());}
    public async Task<ScheduleEvent> CreateEventAsync(ScheduleEvent x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);if(x.CourseId is int courseId && (courses is null || !(await courses.GetCoursesAsync(ct)).Any(c=>c.Id==courseId)))throw new ValidationException("A valid course is required.");lock(gate){x=Clone(x);x.Id=data.Count==0?1:data.Max(e=>e.Id)+1;data.Add(x);}Changed?.Invoke();return Clone(x);}
    public async Task<ScheduleEvent?> UpdateEventAsync(ScheduleEvent x,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(x,new ValidationContext(x),true);if(x.CourseId is int courseId && (courses is null || !(await courses.GetCoursesAsync(ct)).Any(c=>c.Id==courseId)))throw new ValidationException("A valid course is required.");lock(gate){var i=data.FindIndex(e=>e.Id==x.Id);if(i<0)return null;data[i]=Clone(x);}Changed?.Invoke();return Clone(x);}
    public Task<bool> DeleteEventAsync(int id,CancellationToken ct=default){ct.ThrowIfCancellationRequested();bool ok;lock(gate)ok=data.RemoveAll(x=>x.Id==id)>0;if(ok)Changed?.Invoke();return Task.FromResult(ok);}
    private static ScheduleEvent Clone(ScheduleEvent x)=>new(){Id=x.Id,CourseId=x.CourseId,CourseName=x.CourseName,Title=x.Title,Description=x.Description,StartTime=x.StartTime,EndTime=x.EndTime,Location=x.Location,Type=x.Type};
}

public sealed class MockSettingsService : ISettingsService
{
    private readonly object gate=new(); private StudentSettings value=new(); public event Action? Changed;
    public Task<StudentSettings> GetSettingsAsync(CancellationToken ct=default){ct.ThrowIfCancellationRequested();lock(gate)return Task.FromResult(Clone(value));}
    public Task<StudentSettings> SaveSettingsAsync(StudentSettings settings,CancellationToken ct=default){ct.ThrowIfCancellationRequested();Validator.ValidateObject(settings,new ValidationContext(settings),true);lock(gate)value=Clone(settings);Changed?.Invoke();return Task.FromResult(Clone(value));}
    private static StudentSettings Clone(StudentSettings x)=>new(){FullName=x.FullName,StudentId=x.StudentId,Program=x.Program,Email=x.Email,Theme=x.Theme,Language=x.Language,TimeZone=x.TimeZone,Semester=x.Semester,AcademicYear=x.AcademicYear,GpaScale=x.GpaScale,NotificationPreferences=new(){AssignmentReminders=x.NotificationPreferences.AssignmentReminders,ExamReminders=x.NotificationPreferences.ExamReminders,DeadlineAlerts=x.NotificationPreferences.DeadlineAlerts,WeeklySummary=x.NotificationPreferences.WeeklySummary}};
}

public sealed class MockDashboardService(ICourseService courses, IAssignmentService assignments, IGradeService grades, IScheduleService schedule, ISettingsService settings) : IDashboardService
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken ct=default)
    {
        var cs=await courses.GetCoursesAsync(ct); var tasks=await assignments.GetAssignmentsAsync(ct); var gs=await grades.GetGradesAsync(ct); var events=await schedule.GetEventsAsync(ct); var prefs=await settings.GetSettingsAsync(ct);
        var today=DateTime.Today; var completed=tasks.Count(a=>a.Status==AssignmentStatus.Completed);
        return new(cs.Count,tasks.Count,completed,tasks.Count-completed,tasks.Count(a=>a.Status!=AssignmentStatus.Completed&&a.DueDate.Date<today),tasks.Count==0?0:completed*100m/tasks.Count,
            GpaCalculator.Calculate(gs,cs,prefs.GpaScale),tasks.Where(a=>a.Status!=AssignmentStatus.Completed&&a.DueDate.Date>=today).OrderBy(a=>a.DueDate).Take(5).ToArray(),events.Where(e=>e.StartTime.Date==today).OrderBy(e=>e.StartTime).ToArray());
    }
}
