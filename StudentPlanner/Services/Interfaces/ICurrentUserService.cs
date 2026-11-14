namespace StudentPlanner.Services.Interfaces;

public interface ICurrentUserService
{
    Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default);
}
