using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Entities;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Delivery areas of the logged-in plant (tenant filter applies automatically).</summary>
public class AreaService : IAreaService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AreaService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AreaListItem>> GetAreasAsync(CancellationToken ct = default)
    {
        var plantId = _currentUser.PlantId;

        return await _db.Areas.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AreaListItem(
                a.Id,
                a.Name,
                a.DeliveryBoyId,
                _db.Users.Where(u => u.Id == a.DeliveryBoyId && u.PlantId == plantId).Select(u => u.FullName).FirstOrDefault(),
                a.Customers.Count(c => c.IsActive)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AreaOption>> GetOptionsAsync(CancellationToken ct = default) =>
        await _db.Areas.AsNoTracking().OrderBy(a => a.Name).Select(a => new AreaOption(a.Id, a.Name)).ToListAsync(ct);

    public async Task<AreaListItem?> GetAsync(int id, CancellationToken ct = default) =>
        (await GetAreasAsync(ct)).FirstOrDefault(a => a.Id == id);

    public async Task<Result> CreateAsync(string name, string? deliveryBoyId, CancellationToken ct = default)
    {
        name = name.Trim();
        if (await _db.Areas.AnyAsync(a => a.Name == name, ct))
            return Result.Fail("An area with this name already exists.");

        if (!await IsOwnStaffAsync(deliveryBoyId, ct))
            return Result.Fail("Please select a valid delivery staff member.");

        _db.Areas.Add(new Area { Name = name, DeliveryBoyId = NullIfEmpty(deliveryBoyId) });
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> UpdateAsync(int id, string name, string? deliveryBoyId, CancellationToken ct = default)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (area is null) return Result.Fail("Area not found.");

        name = name.Trim();
        if (await _db.Areas.AnyAsync(a => a.Name == name && a.Id != id, ct))
            return Result.Fail("An area with this name already exists.");

        if (!await IsOwnStaffAsync(deliveryBoyId, ct))
            return Result.Fail("Please select a valid delivery staff member.");

        area.Name = name;
        area.DeliveryBoyId = NullIfEmpty(deliveryBoyId);
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var area = await _db.Areas.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (area is null) return Result.Fail("Area not found.");

        if (await _db.Customers.AnyAsync(c => c.AreaId == id && c.IsActive, ct))
            return Result.Fail("Move this area's customers to another area before removing it.");

        _db.Areas.Remove(area); // soft delete
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>A delivery boy id must belong to this plant (users are not tenant-filtered).</summary>
    private async Task<bool> IsOwnStaffAsync(string? userId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(userId)) return true;
        var plantId = _currentUser.PlantId;
        return await _db.Users.AnyAsync(u => u.Id == userId && u.PlantId == plantId, ct);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
