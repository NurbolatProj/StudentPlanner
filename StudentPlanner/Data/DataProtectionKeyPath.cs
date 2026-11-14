namespace StudentPlanner.Data;

public static class DataProtectionKeyPath
{
    public static string Resolve(string? configuredPath)
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData)) localData = AppContext.BaseDirectory;
        var configured = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(localData, "StudentPlanner", "DataProtectionKeys")
            : configuredPath.Trim();
        var fullPath = Path.GetFullPath(configured);
        try
        {
            Directory.CreateDirectory(fullPath);
            var probe = Path.Combine(fullPath, $".write-check-{Guid.NewGuid():N}");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            return fullPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new InvalidOperationException(
                $"ASP.NET Core Data Protection key directory '{fullPath}' could not be created or accessed. " +
                "Configure DataProtection:KeysPath to a stable writable directory. Startup is stopped to avoid ephemeral authentication keys.",
                exception);
        }
    }
}
