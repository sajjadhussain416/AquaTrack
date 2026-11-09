using AquaTrack.Application.Common;
using AquaTrack.Application.Plants;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Identity;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>
/// Plant onboarding and management for Super Admin.
/// Author: Attique
/// </summary>
public class PlantService : IPlantService
{
    private const int TrialDays = 30;

    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlantService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<PlatformStats> GetPlatformStatsAsync(CancellationToken ct = default)
    {
        var plants = await _db.Plants
            .Select(p => new { p.Status, Price = p.Plan!.MonthlyPrice })
            .ToListAsync(ct);

        var customers = await _db.Customers.CountAsync(c => c.IsActive, ct);

        return new PlatformStats(
            TotalPlants: plants.Count,
            ActivePlants: plants.Count(p => p.Status == PlantStatus.Active),
            TrialPlants: plants.Count(p => p.Status == PlantStatus.Trial),
            SuspendedPlants: plants.Count(p => p.Status == PlantStatus.Suspended),
            TotalCustomers: customers,
            ExpectedMonthlyRevenue: plants.Where(p => p.Status == PlantStatus.Active).Sum(p => p.Price));
    }

    public async Task<IReadOnlyList<PlantListItem>> GetPlantsAsync(string? search, PlantStatus? status, CancellationToken ct = default)
    {
        var query = _db.Plants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s) || p.OwnerName.Contains(s) || p.Phone.Contains(s) || p.City.Contains(s));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await Project(query.OrderByDescending(p => p.CreatedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PlantListItem>> GetRecentPlantsAsync(int count, CancellationToken ct = default)
    {
        return await Project(_db.Plants.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(count)).ToListAsync(ct);
    }

    private IQueryable<PlantListItem> Project(IQueryable<Plant> query) =>
        query.Select(p => new PlantListItem(
            p.Id,
            p.Name,
            p.OwnerName,
            p.Phone,
            p.City,
            p.Plan!.Name,
            p.Plan!.MonthlyPrice,
            p.Status,
            p.TrialEndsOn,
            p.NextDueDate,
            _db.Customers.Count(c => c.PlantId == p.Id && c.IsActive),
            p.CreatedAt));

    public async Task<IReadOnlyList<PlanOption>> GetPlansAsync(CancellationToken ct = default)
    {
        return await _db.Plans.AsNoTracking()
            .OrderBy(p => p.MonthlyPrice)
            .Select(p => new PlanOption(p.Id, p.Name, p.MonthlyPrice))
            .ToListAsync(ct);
    }

    public async Task<Result<int>> CreatePlantAsync(CreatePlantRequest r, CancellationToken ct = default)
    {
        var ownerPhone = PhoneNumber.Normalize(r.OwnerPhone);

        if (await _userManager.FindByNameAsync(ownerPhone) is not null)
        {
            return Result<int>.Fail("This mobile number is already registered.");
        }

        if (!await _db.Plans.AnyAsync(p => p.Id == r.PlanId, ct))
        {
            return Result<int>.Fail("Please select a plan.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var now = DateTime.UtcNow;
        var plant = new Plant
        {
            Name = r.PlantName.Trim(),
            OwnerName = r.OwnerName.Trim(),
            Phone = PhoneNumber.Normalize(r.PlantPhone),
            City = r.City.Trim(),
            Address = r.Address?.Trim(),
            PlanId = r.PlanId,
            DefaultRate = r.DefaultRate,
            DepositPerBottle = r.DepositPerBottle,
            BillingDay = r.BillingDay,
            Status = PlantStatus.Trial,
            TrialEndsOn = now.AddDays(TrialDays),
            NextDueDate = now.AddDays(TrialDays)
        };

        _db.Plants.Add(plant);
        await _db.SaveChangesAsync(ct);

        _db.BottleStocks.Add(new BottleStock
        {
            PlantId = plant.Id,
            TotalOwned = r.TotalBottles,
            AtPlantEmpty = r.TotalBottles,
            AtPlantFilled = 0
        });
        await _db.SaveChangesAsync(ct);

        var owner = new ApplicationUser
        {
            UserName = ownerPhone,
            PhoneNumber = ownerPhone,
            FullName = r.OwnerName.Trim(),
            PlantId = plant.Id
        };

        var created = await _userManager.CreateAsync(owner, r.OwnerPassword);
        if (!created.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Result<int>.Fail(created.Errors.Select(MapIdentityError).Distinct().ToArray());
        }

        await _userManager.AddToRoleAsync(owner, Roles.PlantOwner);
        await tx.CommitAsync(ct);

        return Result<int>.Ok(plant.Id);
    }

    public async Task<Result> SetStatusAsync(int plantId, PlantStatus status, CancellationToken ct = default)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == plantId, ct);
        if (plant is null) return Result.Fail("Plant not found.");

        plant.Status = status;
        if (status == PlantStatus.Active && (plant.NextDueDate is null || plant.NextDueDate < DateTime.UtcNow))
        {
            plant.NextDueDate = DateTime.UtcNow.AddMonths(1);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<PlantStatus?> GetStatusForLoginAsync(int plantId, CancellationToken ct = default)
    {
        return await _db.Plants.IgnoreQueryFilters()
            .Where(p => p.Id == plantId && !p.IsDeleted)
            .Select(p => (PlantStatus?)p.Status)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Turns Identity's English messages into keys we translate.</summary>
    private static string MapIdentityError(IdentityError e) => e.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) => "Password must be at least 8 characters.",
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) => "Password must contain at least one number.",
        nameof(IdentityErrorDescriber.DuplicateUserName) => "This mobile number is already registered.",
        _ => "Could not create the owner login. Please check the details."
    };
}
