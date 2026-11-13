using StudentPlanner.Components;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services.Mock;
using StudentPlanner.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ICourseService, MockCourseService>();
builder.Services.AddScoped<IAssignmentService, MockAssignmentService>();
builder.Services.AddScoped<IGradeService, MockGradeService>();
builder.Services.AddScoped<IScheduleService, MockScheduleService>();
builder.Services.AddScoped<BrowserSettingsService>();
builder.Services.AddScoped<ISettingsService>(sp => sp.GetRequiredService<BrowserSettingsService>());
builder.Services.AddScoped<ISettingsInitializer>(sp => sp.GetRequiredService<BrowserSettingsService>());
builder.Services.AddScoped<IDashboardService, MockDashboardService>();

var app = builder.Build();

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
