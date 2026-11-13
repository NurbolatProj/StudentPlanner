using StudentPlanner.Components;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services;
using StudentPlanner.Data;
using StudentPlanner.Services.Business;
using StudentPlanner.Services.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
var sqliteConnectionString = DatabasePath.CreateConnectionString(builder.Configuration["Database:Path"]);
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(sqliteConnectionString));
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<ICourseService, EfCourseService>();
builder.Services.AddScoped<EfAssignmentService>();
builder.Services.AddScoped<IAssignmentService>(services => services.GetRequiredService<EfAssignmentService>());
builder.Services.AddScoped<IAssignmentQueryService>(services => services.GetRequiredService<EfAssignmentService>());
builder.Services.AddScoped<IGradeService, EfGradeService>();
builder.Services.AddScoped<EfScheduleService>();
builder.Services.AddScoped<IScheduleService>(services => services.GetRequiredService<EfScheduleService>());
builder.Services.AddScoped<IScheduleQueryService>(services => services.GetRequiredService<EfScheduleService>());
builder.Services.AddScoped<BrowserSettingsService>();
builder.Services.AddScoped<ISettingsService>(sp => sp.GetRequiredService<BrowserSettingsService>());
builder.Services.AddScoped<ISettingsInitializer>(sp => sp.GetRequiredService<BrowserSettingsService>());
builder.Services.AddScoped<IDashboardService, EfDashboardService>();
var priorityWeights = builder.Configuration.GetSection("AssignmentPriority").Get<AssignmentPriorityWeights>()
    ?? new AssignmentPriorityWeights();
builder.Services.AddSingleton<IAssignmentPriorityService>(new AssignmentPriorityService(priorityWeights));

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DbInitializer>().InitializeAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// The Development HTTP launch profile intentionally serves plain HTTP. Redirecting
// there without an HTTPS endpoint configured produces the "https port" warning.
// Production keeps the HTTPS redirect middleware enabled.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
