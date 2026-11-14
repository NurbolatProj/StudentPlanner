using StudentPlanner.Components;
using StudentPlanner.Services.Interfaces;
using StudentPlanner.Services;
using StudentPlanner.Data;
using StudentPlanner.Services.Business;
using StudentPlanner.Services.Database;
using StudentPlanner.Services.Focus;
using StudentPlanner.Services.Insights;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();
var sqliteConnectionString = DatabasePath.CreateConnectionString(
    builder.Configuration["Database:Path"], builder.Configuration.GetValue("Database:Pooling", true));
var keyRingPath = DataProtectionKeyPath.Resolve(builder.Configuration["DataProtection:KeysPath"]);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
    .SetApplicationName("StudentPlanner");
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(sqliteConnectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.AllowedForNewUsers = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "StudentPlanner.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
});
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccountProfileNotifier, AccountProfileNotifier>();
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddScoped<ICourseService, EfCourseService>();
builder.Services.AddScoped<EfAssignmentService>();
builder.Services.AddScoped<IAssignmentService>(services => services.GetRequiredService<EfAssignmentService>());
builder.Services.AddScoped<IAssignmentQueryService>(services => services.GetRequiredService<EfAssignmentService>());
builder.Services.AddScoped<IGradeService, EfGradeService>();
builder.Services.AddScoped<EfScheduleService>();
builder.Services.AddScoped<IScheduleService>(services => services.GetRequiredService<EfScheduleService>());
builder.Services.AddScoped<IScheduleQueryService>(services => services.GetRequiredService<EfScheduleService>());
builder.Services.AddScoped<ISettingsService, IdentitySettingsService>();
builder.Services.AddScoped<ISettingsInitializer, AuthenticatedSettingsInitializer>();
builder.Services.AddScoped<IDashboardService, EfDashboardService>();
builder.Services.AddScoped<IFocusSessionService, FocusSessionService>();
builder.Services.AddScoped<IStudyInsightsService, StudyInsightsService>();
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
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// The Development HTTP launch profile intentionally serves plain HTTP. Redirecting
// there without an HTTPS endpoint configured produces the "https port" warning.
// Production keeps the HTTPS redirect middleware enabled.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapRazorPages();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
