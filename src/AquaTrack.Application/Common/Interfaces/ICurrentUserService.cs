namespace AquaTrack.Application.Common.Interfaces;

/// <summary>
/// Who is making the current request. Implemented in the Web layer from the login cookie.
/// AppDbContext uses PlantId and IsSuperAdmin for the tenant filter (SRS AUTH-04).
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }

    /// <summary>Null for Super Admin and for anonymous requests.</summary>
    int? PlantId { get; }

    bool IsSuperAdmin { get; }

    bool IsInRole(string role);
}
