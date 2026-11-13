using Microsoft.Data.Sqlite;

namespace StudentPlanner.Data;

public static class DatabasePath
{
    public static string Resolve(string? configuredPath)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
            localData = AppContext.BaseDirectory;

        var applicationDirectory = Path.Combine(localData, "StudentPlanner");
        var path = string.IsNullOrWhiteSpace(configuredPath) ? "studentplanner.db" : configuredPath.Trim();
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(applicationDirectory, path));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        return fullPath;
    }

    public static string CreateConnectionString(string? configuredPath) => new SqliteConnectionStringBuilder
    {
        DataSource = Resolve(configuredPath),
        ForeignKeys = true,
        Pooling = true
    }.ToString();
}
