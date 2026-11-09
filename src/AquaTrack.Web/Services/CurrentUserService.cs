using System.Security.Claims;
using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;

namespace AquaTrack.Web.Services;

/// <summary>Reads the logged-in user from the auth cookie of the current HTTP request.</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? PlantId =>
        int.TryParse(User?.FindFirstValue(AppClaims.PlantId), out var id) ? id : null;

    public bool IsSuperAdmin => User?.IsInRole(Roles.SuperAdmin) ?? false;
}
