using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Delivery boys of the logged-in plant. Identity users are not tenant-filtered,
/// so every query here filters by PlantId explicitly.
/// </summary>
public class StaffService : IStaffService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public StaffService(AppDbContext db, UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        _db = db;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    private IQueryable<ApplicationUser> DeliveryBoys()
    {
        var plantId = _currentUser.PlantId;
        var roleId = _db.Roles.Where(r => r.Name == Roles.DeliveryBoy).Select(r => r.Id);

        return _db.Users.Where(u => plantId != null && u.PlantId == plantId
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && roleId.Contains(ur.RoleId)));
    }

    public async Task<IReadOnlyList<StaffListItem>> GetDeliveryStaffAsync(CancellationToken ct = default)
    {
        var users = await DeliveryBoys().AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, Phone = u.UserName, u.IsActive })
            .ToListAsync(ct);

        var areas = await _db.Areas.AsNoTracking()
            .Where(a => a.DeliveryBoyId != null)
            .Select(a => new { a.DeliveryBoyId, a.Name })
            .ToListAsync(ct);

        return users.Select(u => new StaffListItem(
                u.Id, u.FullName, u.Phone ?? "", u.IsActive,
                areas.Where(a => a.DeliveryBoyId == u.Id).Select(a => a.Name).OrderBy(n => n).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<StaffOption>> GetOptionsAsync(CancellationToken ct = default) =>
        await DeliveryBoys().AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new StaffOption(u.Id, u.FullName))
            .ToListAsync(ct);

    public async Task<Result> CreateDeliveryBoyAsync(string fullName, string phone, string password, IReadOnlyList<int> areaIds, CancellationToken ct = default)
    {
        var plantId = _currentUser.PlantId;
        if (plantId is null) return Result.Fail("Only plant users can add staff.");

        // Plan limit (SRS SUB-01)
        var limit = await _db.Plants.Where(p => p.Id == plantId).Select(p => p.Plan!.MaxDeliveryBoys).FirstOrDefaultAsync(ct);
        if (limit.HasValue && await DeliveryBoys().CountAsync(u => u.IsActive, ct) >= limit.Value)
            return Result.Fail("Your plan allows no more delivery staff. Upgrade the plan to add more.");

        var userName = PhoneNumber.Normalize(phone);
        if (await _userManager.FindByNameAsync(userName) is not null)
            return Result.Fail("This mobile number is already registered.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var user = new ApplicationUser
        {
            UserName = userName,
            PhoneNumber = userName,
            FullName = fullName.Trim(),
            PlantId = plantId
        };

        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Result.Fail(created.Errors.Select(IdentityErrors.Map).Distinct().ToArray());
        }

        await _userManager.AddToRoleAsync(user, Roles.DeliveryBoy);

        if (areaIds.Count > 0)
        {
            var areas = await _db.Areas.Where(a => areaIds.Contains(a.Id)).ToListAsync(ct);
            foreach (var area in areas) area.DeliveryBoyId = user.Id;
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetActiveAsync(string userId, bool active, CancellationToken ct = default)
    {
        var user = await DeliveryBoys().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Staff member not found.");

        user.IsActive = active;
        await _userManager.UpdateAsync(user);

        // Invalidates existing login cookies (checked by Identity within 30 minutes);
        // the login page already refuses inactive users.
        await _userManager.UpdateSecurityStampAsync(user);
        return Result.Ok();
    }

    public async Task<Result> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
    {
        var user = await DeliveryBoys().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result.Fail("Staff member not found.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        return result.Succeeded
            ? Result.Ok()
            : Result.Fail(result.Errors.Select(IdentityErrors.Map).Distinct().ToArray());
    }
}

/// <summary>Turns Identity's English messages into keys we translate.</summary>
internal static class IdentityErrors
{
    public static string Map(IdentityError e) => e.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => "Password must be at least 8 characters.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "Password must contain at least one number.",
        nameof(IdentityErrorDescriber.DuplicateUserName) => "This mobile number is already registered.",
        _ => "Could not save the login. Please check the details."
    };
}
