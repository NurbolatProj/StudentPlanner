using Xunit;
using StudentPlanner.Services.Insights;

namespace StudentPlanner.Tests.Features;

public sealed class WorkloadPlannerTests
{
    private static WorkItem TaskItem(int id,string title,DateTime due,int minutes,int priority=1)=>new(id,title,"CS",due,minutes,priority);

    [Fact]
    public void Analyze_GroupsDeadlinesAndReportsConcentration()
    {
        var today=new DateTime(2026,11,14);
        var result=WorkloadPlanner.Analyze(today,7,120,
            [TaskItem(1,"Alpha",today.AddDays(2),60),TaskItem(2,"Beta",today.AddDays(2),45)],[]);
        Assert.Equal(2,result.DeadlineConcentration);
        Assert.Contains(result.Risks,r=>r.Code=="deadline-cluster");
        Assert.All(result.Plan,p=>Assert.True(p.Date<=today.AddDays(2)));
    }

    [Fact]
    public void Analyze_OrdersPriorityAndPlansWithinCapacity()
    {
        var today=new DateTime(2026,11,14);
        var items=new[]{TaskItem(1,"Low",today,90,0),TaskItem(2,"High",today,90,2)};
        var first=WorkloadPlanner.Analyze(today,7,120,items,[]);
        var second=WorkloadPlanner.Analyze(today,7,120,items.Reverse(),[]);
        Assert.Equal(first.Plan,second.Plan);
        Assert.Equal("High",first.Plan[0].Title);
        Assert.Equal(first.Plan.Sum(x=>x.Minutes),first.PlannedMinutes);
        Assert.Equal(60,first.UnplannedMinutes);
    }

    [Fact]
    public void Analyze_SubtractsCalendarTimeAndFlagsOverCapacity()
    {
        var today=new DateTime(2026,11,14);
        var result=WorkloadPlanner.Analyze(today,7,120,[TaskItem(1,"Task",today,100)],
            [new CalendarBlock(today,90,"Class")]);
        Assert.Equal(30,result.Days[0].AvailableMinutes);
        Assert.Equal(30,result.Plan.Sum(x=>x.Minutes));
        Assert.Contains(result.Risks,r=>r.Code=="capacity-shortfall");
    }

    [Fact]
    public void Analyze_NeverSchedulesAfterDueDateAndHandlesEmptyData()
    {
        var today=new DateTime(2026,11,14);
        var result=WorkloadPlanner.Analyze(today,7,60,[TaskItem(1,"Today",today,150)],[]);
        Assert.All(result.Plan,p=>Assert.True(p.Date<=today));
        var empty=WorkloadPlanner.Analyze(today,7,60,[],[]);
        Assert.Empty(empty.Plan); Assert.Empty(empty.Risks); Assert.Equal(0,empty.PendingMinutes);
    }
}
