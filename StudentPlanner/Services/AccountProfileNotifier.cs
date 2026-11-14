using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services;

public sealed class AccountProfileNotifier : IAccountProfileNotifier
{
    public event Action? Changed;
    public void NotifyChanged() => Changed?.Invoke();
}
