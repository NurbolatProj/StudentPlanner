using System.Text.Json;
using Microsoft.JSInterop;
using StudentPlanner.Models;

namespace StudentPlanner.Services;

public sealed class LocalUserPreferencesService(IJSRuntime js) : IUserPreferencesService
{
    private const string StorageKey = "student-planner.preferences.v1";
    public UserPreferences Current { get; private set; } = new();

    public async Task<UserPreferences> LoadAsync()
    {
        try {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            Current = string.IsNullOrWhiteSpace(json)
                ? new UserPreferences()
                : JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
            return Current;
        } catch (InvalidOperationException) {
            return Current;
        } catch (JSException) {
            return Current;
        }
    }

    public async Task SaveAsync(UserPreferences preferences)
    {
        Current = preferences;
        var json = JsonSerializer.Serialize(preferences);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
