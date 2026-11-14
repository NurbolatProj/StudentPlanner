using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using StudentPlanner.Services.Interfaces;

namespace StudentPlanner.Services;

public sealed class CurrentUserService(AuthenticationStateProvider authenticationStateProvider) : ICurrentUserService
{
    public async Task<string> GetRequiredUserIdAsync(CancellationToken cancellationToken = default)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        if (user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Sign in to access planner data.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("The authenticated account has no user identifier.");
    }
}
