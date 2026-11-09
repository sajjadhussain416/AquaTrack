using System.Security.Claims;
using AquaTrack.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AquaTrack.Infrastructure.Identity;

/// <summary>Adds plant_id and full_name claims to the login cookie, so every request knows its tenant.</summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));

        if (user.PlantId.HasValue)
        {
            identity.AddClaim(new Claim(AppClaims.PlantId, user.PlantId.Value.ToString()));
        }

        return identity;
    }
}
