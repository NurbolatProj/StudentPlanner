using Bunit;
using Microsoft.Extensions.DependencyInjection;
using StudentPlanner.Components.Pages;
using StudentPlanner.Components.Common;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services.Mock;
using Xunit;

namespace StudentPlanner.Tests;

public sealed class AssignmentsPageTests
{
    [Fact]
    public void SearchFiltersAssignmentsFromSharedService()
    {
        using var context = new TestContext();
        context.Services.AddScoped<IAssignmentService, AssignmentService>();

        var page = context.RenderComponent<Assignments>();
        page.Find("input[placeholder='Search assignments...']").Input("Programming");

        Assert.Contains("Complete Programming Assignment", page.Markup);
        Assert.DoesNotContain("Database Project", page.Markup);
    }

    [Fact]
    public async Task AddingAssignmentUpdatesTheSharedCollection()
    {
        var planner = new AssignmentService();
        var initialCount = (await planner.GetAllAsync()).Count;

        await planner.CreateAsync(new StudentPlanner.Models.Assignment {
            Title = "New task", Course = "DBMS", DueDate = DateTime.Today.AddDays(1)
        });

        var assignments = await planner.GetAllAsync();
        Assert.Equal(initialCount + 1, assignments.Count);
        Assert.Contains(assignments, item => item.Title == "New task");
    }

    [Fact]
    public void ConfirmActionButtonInvokesCallbackAfterConfirmation()
    {
        using var context = new TestContext();
        context.JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);
        var invoked = false;

        var button = context.RenderComponent<ConfirmActionButton>(parameters => parameters
            .Add(item => item.CssClass, "delete")
            .Add(item => item.AriaLabel, "Delete record")
            .Add(item => item.ConfirmationMessage, "Delete this record?")
            .Add(item => item.OnConfirmed, () => invoked = true)
            .AddChildContent("Delete"));

        button.Find("button").Click();

        Assert.True(invoked);
    }
}
