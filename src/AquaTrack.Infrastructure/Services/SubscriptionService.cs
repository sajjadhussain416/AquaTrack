using AquaTrack.Application.Common;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Application.Plants;
using AquaTrack.Domain.Entities;
using AquaTrack.Domain.Enums;
using AquaTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Infrastructure.Services;

/// <summary>Plans, plant subscriptions and subscription payments (SRS SUB-01..04).</summary>
public class SubscriptionService : ISubscriptionService
{
    private const decimal MaxAmount = 10_000_000m;

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SubscriptionService(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ---------------- Plans ----------------

    public async Task<IReadOnlyList<PlanRow>> GetPlansAsync(CancellationToken ct = default) =>
        await _db.Plans.AsNoTracking()
            .OrderBy(p => p.MonthlyPrice)
            .Select(p => new PlanRow(p.Id, p.Name, p.MonthlyPrice, p.MaxCustomers, p.MaxDeliveryBoys, p.IncludesWhatsAppApi,
                _db.Plants.Count(x => x.PlanId == p.Id)))
            .ToListAsync(ct);

    public async Task<PlanInput?> GetPlanAsync(int id, CancellationToken ct = default) =>
        await _db.Plans.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new PlanInput
            {
                Name = p.Name, MonthlyPrice = p.MonthlyPrice, MaxCustomers = p.MaxCustomers,
                MaxDeliveryBoys = p.MaxDeliveryBoys, IncludesWhatsAppApi = p.IncludesWhatsAppApi
            })
            .FirstOrDefaultAsync(ct);

    public async Task<Result> SavePlanAsync(int? id, PlanInput input, CancellationToken ct = default)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 50) return Result.Fail("Enter a plan name (up to 50 letters).");
        if (input.MonthlyPrice is < 0 or > 1_000_000) return Result.Fail("Enter a monthly price between 0 and 1,000,000.");
        if (input.MaxCustomers is < 1 or > 1_000_000) return Result.Fail("Customer limit must be at least 1, or empty for unlimited.");
        if (input.MaxDeliveryBoys is < 1 or > 10_000) return Result.Fail("Delivery staff limit must be at least 1, or empty for unlimited.");

        if (await _db.Plans.AnyAsync(p => p.Name == name && p.Id != (id ?? 0), ct))
            return Result.Fail("A plan with this name already exists.");

        Plan plan;
        if (id is null)
        {
            plan = new Plan();
            _db.Plans.Add(plan);
        }
        else
        {
            plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new KeyNotFoundException();
        }

        plan.Name = name;
        plan.MonthlyPrice = decimal.Round(input.MonthlyPrice, 2);
        plan.MaxCustomers = input.MaxCustomers;
        plan.MaxDeliveryBoys = input.MaxDeliveryBoys;
        plan.IncludesWhatsAppApi = input.IncludesWhatsAppApi;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeletePlanAsync(int id, CancellationToken ct = default)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan is null) return Result.Fail("Plan not found.");
        if (await _db.Plants.AnyAsync(p => p.PlanId == id, ct))
            return Result.Fail("Plants are using this plan. Move them to another plan first.");

        _db.Plans.Remove(plan); // soft delete
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------- One plant ----------------

    public async Task<PlantSubscription?> GetPlantAsync(int plantId, CancellationToken ct = default)
    {
        var p = await _db.Plants.AsNoTracking().Where(x => x.Id == plantId)
            .Select(x => new
            {
                x.Id, x.Name, x.OwnerName, x.Phone, x.City, x.PlanId, PlanName = x.Plan!.Name, x.Plan.MonthlyPrice,
                x.Plan.MaxCustomers, x.Plan.MaxDeliveryBoys, x.Status, x.TrialEndsOn, x.NextDueDate, x.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        var payments = await _db.SubscriptionPayments.AsNoTracking()
            .Where(s => s.PlantId == plantId)
            .OrderByDescending(s => s.PaidOn).ThenByDescending(s => s.Id)
            .Select(s => new SubscriptionPaymentRow(s.Id, s.PaidOn, s.Amount, s.PeriodFrom, s.PeriodTo, s.Note))
            .ToListAsync(ct);

        var (customers, staff) = await CountsAsync(plantId, ct);

        return new PlantSubscription(p.Id, p.Name, p.OwnerName, p.Phone, p.City, p.PlanId, p.PlanName, p.MonthlyPrice,
            p.MaxCustomers, p.MaxDeliveryBoys, p.Status, p.TrialEndsOn, p.NextDueDate, customers, staff, p.CreatedAt, payments);
    }

    public async Task<Result> RecordPaymentAsync(SubscriptionPaymentInput input, CancellationToken ct = default)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == input.PlantId, ct);
        if (plant is null) return Result.Fail("Plant not found.");
        if (input.Amount <= 0 || input.Amount > MaxAmount) return Result.Fail("Enter an amount greater than zero.");
        if (input.Months is < 1 or > 24) return Result.Fail("Choose between 1 and 24 months.");

        var today = PakistanTime.Today;
        if (input.PaidOn > today || input.PaidOn < today.AddYears(-1)) return Result.Fail("Choose a payment date within the last year.");

        // The new period starts where the current one ends (or today, if it has already ended).
        var nowUtc = DateTime.UtcNow;
        var currentEnd = plant.Status == PlantStatus.Trial ? plant.TrialEndsOn : plant.NextDueDate;
        var from = currentEnd is not null && currentEnd > nowUtc ? currentEnd.Value : nowUtc.Date;
        var to = from.AddMonths(input.Months);

        _db.SubscriptionPayments.Add(new SubscriptionPayment
        {
            PlantId = plant.Id,
            Amount = decimal.Round(input.Amount, 2),
            PaidOn = input.PaidOn.ToDateTime(new TimeOnly(7, 0), DateTimeKind.Utc), // noon Pakistan time
            PeriodFrom = from,
            PeriodTo = to,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()[..Math.Min(input.Note.Trim().Length, 300)]
        });

        plant.NextDueDate = to;
        plant.Status = PlantStatus.Active;

        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeletePaymentAsync(int paymentId, CancellationToken ct = default)
    {
        var pay = await _db.SubscriptionPayments.FirstOrDefaultAsync(s => s.Id == paymentId, ct);
        if (pay is null) return Result.Fail("Payment not found.");

        var plant = await _db.Plants.FirstAsync(p => p.Id == pay.PlantId, ct);

        // Only the latest period can be taken back, so due dates stay consistent.
        var latest = await _db.SubscriptionPayments.Where(s => s.PlantId == pay.PlantId)
            .OrderByDescending(s => s.PeriodTo).ThenByDescending(s => s.Id).FirstAsync(ct);
        if (latest.Id != pay.Id) return Result.Fail("Only the latest payment can be removed.");

        plant.NextDueDate = pay.PeriodFrom;
        _db.SubscriptionPayments.Remove(pay); // soft delete
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ChangePlanAsync(int plantId, int planId, CancellationToken ct = default)
    {
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == plantId, ct);
        if (plant is null) return Result.Fail("Plant not found.");
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return Result.Fail("Please select a plan.");

        var (customers, staff) = await CountsAsync(plantId, ct);
        if (plan.MaxCustomers is not null && customers > plan.MaxCustomers)
            return Result.Fail("This plant has more customers than the new plan allows.");
        if (plan.MaxDeliveryBoys is not null && staff > plan.MaxDeliveryBoys)
            return Result.Fail("This plant has more delivery staff than the new plan allows.");

        plant.PlanId = planId;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ExtendTrialAsync(int plantId, int days, CancellationToken ct = default)
    {
        if (days is < 1 or > 90) return Result.Fail("Choose between 1 and 90 days.");
        var plant = await _db.Plants.FirstOrDefaultAsync(p => p.Id == plantId, ct);
        if (plant is null) return Result.Fail("Plant not found.");
        if (plant.Status != PlantStatus.Trial) return Result.Fail("Only a plant on free trial can get more trial days.");

        var start = plant.TrialEndsOn is not null && plant.TrialEndsOn > DateTime.UtcNow ? plant.TrialEndsOn.Value : DateTime.UtcNow;
        plant.TrialEndsOn = start.AddDays(days);
        plant.NextDueDate = plant.TrialEndsOn;
        await _db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------- All plants ----------------

    public async Task<SubscriptionOverview> GetOverviewAsync(string? filter, CancellationToken ct = default)
    {
        var today = PakistanTime.Today;

        var plants = await _db.Plants.AsNoTracking()
            .Select(p => new
            {
                p.Id, p.Name, p.OwnerName, p.Phone, PlanName = p.Plan!.Name, p.Plan.MonthlyPrice,
                p.Status, Due = p.Status == PlantStatus.Trial ? p.TrialEndsOn : p.NextDueDate
            })
            .ToListAsync(ct);

        var all = plants
            .Select(p =>
            {
                int? days = p.Due is null ? null : DateOnly.FromDateTime(PakistanTime.ToLocal(p.Due.Value)).DayNumber - today.DayNumber;
                return new DueRow(p.Id, p.Name, p.OwnerName, p.Phone, p.PlanName, p.MonthlyPrice, p.Status, p.Due, days);
            })
            .OrderBy(r => r.Status == PlantStatus.Suspended)
            .ThenBy(r => r.DaysLeft ?? int.MaxValue)
            .ToList();

        bool Open(DueRow r) => r.Status != PlantStatus.Suspended;
        var overdue = all.Where(r => Open(r) && r.Status == PlantStatus.Active && r.DaysLeft < 0).ToList();
        var week = all.Where(r => Open(r) && r.Status == PlantStatus.Active && r.DaysLeft is >= 0 and <= 7).ToList();
        var trials = all.Where(r => r.Status == PlantStatus.Trial && r.DaysLeft <= 7).ToList();

        var rows = filter switch
        {
            "overdue" => overdue,
            "week" => week,
            "trial" => all.Where(r => r.Status == PlantStatus.Trial).ToList(),
            _ => all
        };

        var monthStartUtc = new DateOnly(today.Year, today.Month, 1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-5);
        var collected = await _db.SubscriptionPayments.Where(s => s.PaidOn >= monthStartUtc).SumAsync(s => s.Amount, ct);

        return new SubscriptionOverview(rows, overdue.Count, week.Count, trials.Count, collected,
            all.Where(r => r.Status == PlantStatus.Active).Sum(r => r.MonthlyPrice));
    }

    // ---------------- Plant owner ----------------

    public async Task<MySubscription?> GetMineAsync(CancellationToken ct = default)
    {
        var plantId = _currentUser.PlantId;
        if (plantId is null || _currentUser.IsSuperAdmin) return null;

        var p = await _db.Plants.AsNoTracking().Where(x => x.Id == plantId)
            .Select(x => new { PlanName = x.Plan!.Name, x.Plan.MonthlyPrice, x.Plan.MaxCustomers, x.Plan.MaxDeliveryBoys, x.Status, x.TrialEndsOn, x.NextDueDate })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        var due = p.Status == PlantStatus.Trial ? p.TrialEndsOn : p.NextDueDate;
        int? days = due is null ? null : DateOnly.FromDateTime(PakistanTime.ToLocal(due.Value)).DayNumber - PakistanTime.Today.DayNumber;
        var (customers, staff) = await CountsAsync(plantId.Value, ct);

        return new MySubscription(p.PlanName, p.MonthlyPrice, p.Status, due, days, p.MaxCustomers, customers, p.MaxDeliveryBoys, staff);
    }

    private async Task<(int Customers, int Staff)> CountsAsync(int plantId, CancellationToken ct)
    {
        var customers = await _db.Customers.IgnoreQueryFilters().CountAsync(c => c.PlantId == plantId && !c.IsDeleted && c.IsActive, ct);
        var roleId = await _db.Roles.Where(r => r.Name == Roles.DeliveryBoy).Select(r => r.Id).FirstOrDefaultAsync(ct);
        var staff = await _db.Users.CountAsync(u => u.PlantId == plantId && u.IsActive
            && _db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == roleId), ct);
        return (customers, staff);
    }
}
