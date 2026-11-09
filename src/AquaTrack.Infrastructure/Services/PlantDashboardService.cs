using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Operations;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Numbers on the plant owner's dashboard (SRS RPT-01).</summary>
public class PlantDashboardService : IPlantDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PlantDashboardService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PlantSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = PakistanTime.Today;
        var plantId = _currentUser.PlantId;

        var activeCustomers = await _db.Customers.CountAsync(c => c.IsActive, ct);

        var bottlesOut = await _db.BottleMovements.SumAsync(m =>
            m.Type == BottleMovementType.Issue || m.Type == BottleMovementType.Deliver ? m.Quantity : -m.Quantity, ct);

        var areas = await _db.Areas.CountAsync(ct);

        var deliveryRoleId = await _db.Roles.Where(r => r.Name == Roles.DeliveryBoy).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var staff = await _db.Users.CountAsync(u => u.PlantId == plantId && u.IsActive
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == deliveryRoleId), ct);

        var todayDeliveries = await _db.Deliveries.CountAsync(d => d.Date == today && d.Status == DeliveryStatus.Done, ct);
        var todayCash = await _db.Deliveries.Where(d => d.Date == today).SumAsync(d => d.CashCollected, ct);

        return new PlantSummary(activeCustomers, Math.Max(0, bottlesOut), areas, staff, todayDeliveries, todayCash);
    }
}
