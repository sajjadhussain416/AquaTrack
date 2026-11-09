using Microsoft.AspNetCore.Identity;

namespace AquaTrack.Infrastructure.Identity;

/// <summary>Login user. PlantId is null only for Super Admin.</summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public int? PlantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
