namespace StudentPlanner.Services.Interfaces;

public interface IAccountProfileNotifier
{
    event Action? Changed;
    void NotifyChanged();
}
