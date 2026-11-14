using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StudentPlanner.Data;
using StudentPlanner.Models;
using StudentPlanner.Services.Focus;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services.Insights;

namespace StudentPlanner.Tests.Features;

public sealed class FocusSessionServiceTests
{
    [Fact]
    public async Task FocusSession_PersistsMonotonicDurationAndIsolatesUsers()
    {
        var path=Path.Combine(Path.GetTempPath(),$"planner-focus-{Guid.NewGuid():N}.db");
        try
        {
            var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            var factory=new TestFactory(options);
            await using(var db=await factory.CreateDbContextAsync())
            {
                await db.Database.EnsureCreatedAsync();
                db.Users.AddRange(User("user-a"),User("user-b"));
                db.Courses.AddRange(new Course { OwnerId="user-a",Name="Course A",Code="CAA",Instructor="Faculty" },new Course { OwnerId="user-b",Name="Course B",Code="CBB",Instructor="Faculty" });
                await db.SaveChangesAsync();
                db.Assignments.AddRange(new Assignment { OwnerId="user-a",CourseId=db.Courses.Single(c=>c.OwnerId=="user-a").Id,Title="Assignment A",DueDate=DateTime.Today.AddDays(2) },
                    new Assignment { OwnerId="user-b",CourseId=db.Courses.Single(c=>c.OwnerId=="user-b").Id,Title="Assignment B",DueDate=DateTime.Today.AddDays(2) });
                await db.SaveChangesAsync();
            }
            var serviceA=new FocusSessionService(factory,new TestUser("user-a"));
            Assert.Empty((await serviceA.GetStatisticsAsync(DateTimeOffset.Now)).RecentSessions);
            var ownAssignment=await GetAssignmentId(factory,"user-a");
            var sessionId=await serviceA.StartAsync(ownAssignment,FocusSessionType.Focus,1);
            await Task.Delay(300);
            await serviceA.PauseAsync();
            var paused=serviceA.GetElapsedSeconds();
            await Task.Delay(350);
            Assert.Equal(paused,serviceA.GetElapsedSeconds());
            await serviceA.ResumeAsync();
            await Task.Delay(1100);
            var record=await serviceA.EndAsync(FocusSessionStatus.Completed);
            Assert.NotNull(record); Assert.Equal(sessionId,record!.Id); Assert.InRange(record.ActualDurationSeconds,1,10);
            var statsA=await serviceA.GetStatisticsAsync(DateTimeOffset.Now);
            Assert.Equal(1,statsA.CompletedSessions);
            var insightA=await new StudyInsightsService(factory,new TestUser("user-a")).GetAsync(DateTime.Today,7);
            Assert.DoesNotContain(insightA.Plan,item=>item.Title=="Assignment B");
            var serviceB=new FocusSessionService(factory,new TestUser("user-b"));
            Assert.Empty((await serviceB.GetStatisticsAsync(DateTimeOffset.Now)).RecentSessions);
            var foreignAssignment=await GetAssignmentId(factory,"user-b");
            await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(()=>serviceA.StartAsync(foreignAssignment,FocusSessionType.Focus,25));
        }
        finally { if(File.Exists(path))File.Delete(path); }
    }

    private static ApplicationUser User(string id)=>new(){Id=id,UserName=$"{id}@example.test",Email=$"{id}@example.test",NormalizedUserName=$"{id}@EXAMPLE.TEST",NormalizedEmail=$"{id}@EXAMPLE.TEST",FullName="Test User"};
    private static async Task<int> GetAssignmentId(TestFactory factory,string owner) { await using var db=await factory.CreateDbContextAsync(); return await db.Assignments.Where(a=>a.OwnerId==owner).Select(a=>a.Id).SingleAsync(); }
    private sealed class TestUser(string id):ICurrentUserService { public Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken=default)=>Task.FromResult(id); }
    private sealed class TestFactory(DbContextOptions<AppDbContext> options):IDbContextFactory<AppDbContext>
    { public AppDbContext CreateDbContext()=>new(options); public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(new AppDbContext(options)); }
}
