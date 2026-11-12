using StudentPlanner.Models;

namespace StudentPlanner.Services;

public interface IUserPreferencesService
{
    UserPreferences Current { get; }
    Task<UserPreferences> LoadAsync();
    Task SaveAsync(UserPreferences preferences);
}
