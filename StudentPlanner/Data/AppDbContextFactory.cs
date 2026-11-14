using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentPlanner.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var projectDirectory = new[]
            {
                Environment.CurrentDirectory,
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."))
            }
            .FirstOrDefault(directory => File.Exists(Path.Combine(directory, "appsettings.json")));
        var config = new ConfigurationBuilder()
            .AddJsonFile(projectDirectory is null ? "appsettings.json" : Path.Combine(projectDirectory, "appsettings.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = DatabasePath.CreateConnectionString(config["Database:Path"]);
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
    }
}
