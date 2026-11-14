using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services;

public sealed class AuthenticatedSettingsInitializer(ISettingsService settings) : ISettingsInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
        => _ = await settings.GetSettingsAsync(cancellationToken);
}
